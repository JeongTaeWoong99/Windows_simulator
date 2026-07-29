using System;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;

/// <summary>
/// 클라이언트 세션/요청 매니저 (서비스 로케이터 등록).
/// - 저수준 소켓(MikaNetwork.NetworkManager)과 수신 진입점(ServerPacketHandler) 위에
///   게임 로직용 요청 API·상태 캐시·가공 이벤트를 얹는 파사드.
/// - UI는 서버 폴더의 ServerPacketHandler가 아니라 이 매니저만 바라본다(서버 의존을 한곳에 격리).
///
/// <para>
/// <b>[로그인 1회 수신 세트]</b> — <see cref="Login"/> 한 번이면 서버가 아래를 연달아 밀어준다.
/// 인벤토리·작업슬롯을 따로 요청할 API는 없다(서버에 조회 패킷 자체가 없다).
/// <code>
/// LoginCompleted            ← S_LoginResponse
/// InventoryChanged          ← S_InventoryResponse         (인벤토리 스냅샷)
/// GatherResultReceived      ← S_GatherResultResponse      (오프라인 누적분, 있을 때만)
/// WorkStationSlotsChanged   ← S_WorkStationSlotsResponse  (슬롯 스냅샷)
/// </code>
/// </para>
/// </summary>
public class SessionManager : MonoService<SessionManager>
{
    // ─── 상태 캐시 ───
    private readonly List<ItemInfo>            _inventory        = new List<ItemInfo>();
    private readonly List<WorkStationSlotInfo> _workStationSlots = new List<WorkStationSlotInfo>();

    // 마지막으로 보낸 작업슬롯 요청이 배치였는지(true) 해제였는지(false).
    // 실패 응답에 슬롯이 없어 그때만 쓰는 보조값이다 — 성공하면 서버가 준 슬롯 상태를 믿는다.
    private bool _lastRequestWasAssign;

    public long SessionId  { get; private set; }
    public bool IsLoggedIn { get; private set; }
    public IReadOnlyList<ItemInfo>            Inventory        => _inventory;
    public IReadOnlyList<WorkStationSlotInfo> WorkStationSlots => _workStationSlots;

    // ─── 가공 이벤트 (UI가 구독) ───
    public event Action<bool>?                  LoginCompleted;   // 로그인 완료 (성공 여부)
    public event Action?                        InventoryChanged; // 인벤토리 갱신됨 (스냅샷 반영 후)
    public event Action<List<GachaRewardInfo>>? GachaCompleted;   // 가챠 완료 (뽑힌 보상 목록)

    public event Action<bool, bool>?             WorkStationAssignCompleted; // 슬롯 변경 완료 (성공 여부, 배치=true/해제=false)
    public event Action?                         WorkStationSlotsChanged;    // 슬롯 캐시 갱신됨
    public event Action<S_GatherResultResponse>? GatherResultReceived;       // 채취 결과 푸시 도착

    // 수신 이벤트 구독 (Unity 메시지)
    private void OnEnable()
    {
        ServerPacketHandler.LoginResponded           += OnLoginResponded;
        ServerPacketHandler.InventoryReceived        += OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               += OnGachaDrawn;
        ServerPacketHandler.WorkStationAssigned      += OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived += OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     += OnGatherResultReceived;
    }

    // 수신 이벤트 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        ServerPacketHandler.LoginResponded           -= OnLoginResponded;
        ServerPacketHandler.InventoryReceived        -= OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               -= OnGachaDrawn;
        ServerPacketHandler.WorkStationAssigned      -= OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived -= OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     -= OnGatherResultReceived;
    }

    #region 요청 (UI가 호출)

    // 로그인 요청 — Id만 넘긴다.
    // ★ 이 한 번의 요청으로 인벤토리·작업슬롯 스냅샷까지 전부 따라온다(클래스 주석의 수신 세트 참조).
    public void Login(string id)
    {
        NetworkManager.Instance.Send(new C_LoginRequest { Id = id });
    }

    // 가챠 요청 — 로그인으로 User가 생성된 뒤에만 서버가 처리한다
    public void DrawGacha(int gachaId, int drawCount)
    {
        NetworkManager.Instance.Send(new C_GachaDrawRequest
        {
            GachaId   = gachaId,
            DrawCount = drawCount
        });
    }

    // 작업슬롯 배치 요청 — industry·characterId를 0으로 주면 해제다.
    // 배치된 슬롯만 채취 판정을 받으므로, 이 요청을 보내야 서버의 채취 결과 푸시가 시작된다.
    public void AssignWorkStation(int slotIndex, byte industry, long characterId)
    {
        // 실패 응답에는 슬롯이 실려 오지 않아(Slot=null) 무엇을 시도했는지 알 수 없다.
        // 배치/해제를 구분해 알리려면 보낸 쪽에서 기억해 두는 수밖에 없다.
        _lastRequestWasAssign = industry != 0 && characterId != 0;

        NetworkManager.Instance.Send(new C_WorkStationAssignRequest
        {
            SlotIndex   = slotIndex,
            Industry    = industry,
            CharacterId = characterId
        });
    }

    #endregion
    
    #region 응답 처리 (ServerPacketHandler 구독)

    // 로그인 응답 — 세션 상태 갱신 후 이벤트 발행
    private void OnLoginResponded(S_LoginResponse res)
    {
        IsLoggedIn = res.Success;
        SessionId  = res.SessionId;
        LoginCompleted?.Invoke(res.Success);
    }

    // 인벤토리 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 단 오프라인 정산 "전" 값이므로,
    //   뒤따라오는 채취 결과(ItemChanges)를 반영해야 실제 수량과 맞는다.
    private void OnInventoryReceived(S_InventoryResponse res)
    {
        _inventory.Clear();
        if (res.Items != null)
            _inventory.AddRange(res.Items);

        InventoryChanged?.Invoke();
    }

    // 가챠 응답 — 실패 시 무시, 성공 시 보상 이벤트 발행
    private void OnGachaDrawn(S_GachaDrawResponse res)
    {
        if (!res.Success)
            return;

        GachaCompleted?.Invoke(res.Rewards ?? new List<GachaRewardInfo>());
    }

    // 작업슬롯 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 슬롯 조회 요청 패킷이 없어 전체 스냅샷은 이때뿐이고,
    //   이후에는 배치 응답으로 한 칸씩만 갱신된다.
    private void OnWorkStationSlotsReceived(S_WorkStationSlotsResponse res)
    {
        _workStationSlots.Clear();
        if (res.Slots != null)
            _workStationSlots.AddRange(res.Slots);

        WorkStationSlotsChanged?.Invoke();
    }

    // 슬롯 배치 응답 — 변경된 슬롯 하나만 오므로 캐시에 병합한다.
    // 스냅샷을 다시 받지 않으므로 여기서 반영하지 않으면 캐시가 서버 상태와 어긋난다.
    private void OnWorkStationAssigned(S_WorkStationAssignResponse res)
    {
        var changed = res.Slot;
        if (res.Success && changed != null)
        {
            int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == changed.SlotIndex);
            if (index >= 0)
                _workStationSlots[index] = changed;
            else
                _workStationSlots.Add(changed);

            WorkStationSlotsChanged?.Invoke();
        }

        // 성공했으면 서버가 돌려준 슬롯 상태가 진실이다(산업·캐릭터가 둘 다 차 있으면 배치).
        // 실패해서 슬롯이 없을 때만 보낸 요청을 근거로 삼는다.
        bool wasAssign = changed != null
            ? changed.Industry != 0 && changed.CharacterId != 0 // 조건으로 true or false 판단
            : _lastRequestWasAssign;                            // 이전 기록으로 판단

        WorkStationAssignCompleted?.Invoke(res.Success, wasAssign);
    }

    // 채취 결과 푸시 — 요청 없이 30초 주기로 도착한다. 지금은 가공 없이 그대로 전달
    // ★ 로그인 시에도 1회 올 수 있다(오프라인 누적분. 수확이 없으면 오지 않는다).
    // TODO: 인벤토리 UI를 붙이면 여기서 _inventory에 ItemChanges를 반영해야 한다.
    //       Count는 델타가 아니라 갱신 후 누적 총량이므로 더하지 말고 덮어쓸 것.
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        GatherResultReceived?.Invoke(res);
    }

    #endregion
}
