using System;
using System.Collections;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

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
    // 로그인 응답을 이만큼 기다려 본다. 넘기면 경고를 남긴다 — 이유는 StartLoginTimeoutWatch 참조.
    private const float LoginResponseTimeoutSeconds = 5f;

    // ─── 상태 캐시 ───
    private readonly List<ItemInfo>            _inventory        = new List<ItemInfo>();
    private readonly List<WorkStationSlotInfo> _workStationSlots = new List<WorkStationSlotInfo>();
    private readonly List<CharacterInfo>       _characters       = new List<CharacterInfo>();
    private readonly Dictionary<byte, long>    _currencies       = new Dictionary<byte, long>();

    // 마지막으로 보낸 작업슬롯 요청이 배치였는지(true) 해제였는지(false).
    // 실패 응답에 슬롯이 없어 그때만 쓰는 보조값이다 — 성공하면 서버가 준 슬롯 상태를 믿는다.
    private bool _lastRequestWasAssign;

    // 진행 중인 로그인 무응답 감시. 응답이 오거나 다시 로그인하면 취소한다.
    private Coroutine? _loginTimeoutWatch;

    public long   SessionId  { get; private set; }
    public bool   IsLoggedIn { get; private set; }
    public string LoginId    { get; private set; } = ""; // 서버가 닉네임을 주지 않아, 로그인에 쓴 Id를 그대로 표시에 쓴다

    public IReadOnlyList<ItemInfo>            Inventory        => _inventory;
    public IReadOnlyList<WorkStationSlotInfo> WorkStationSlots => _workStationSlots;

    /// <summary>
    /// 내가 가진 캐릭터들.
    ///
    /// <para>
    /// ⚠️ <b><see cref="CharacterInfo.CharacterId"/>는 개체 번호이고, <see cref="CharacterInfo.CharacterTid"/>가
    /// 캐릭터 종류다.</b> 같은 캐릭터를 여러 마리 가질 수 있어서 종류로는 하나를 특정하지 못한다.
    /// <b>슬롯 배치에 넣을 값은 개체 번호(CharacterId)다</b> — 종류(1001 같은 TID)를 보내면
    /// 서버가 <c>CharacterNotOwned</c>로 거절한다. 이름·적성은 TID로 테이블에서 읽는다.
    /// </para>
    /// </summary>
    public IReadOnlyList<CharacterInfo> Characters => _characters;

    /// <summary>
    /// 첫 번째 보유 캐릭터의 <b>개체 번호</b>. 없으면 0(= 서버에선 배치 해제로 읽힌다).
    /// 캐릭터 선택 UI가 생기기 전까지 테스트 버튼들이 쓰는 임시 통로다.
    /// </summary>
    public long FirstCharacterId => _characters.Count > 0 ? _characters[0].CharacterId : 0L;

    /// <summary>
    /// 캐릭터 <b>개체 번호</b>로 표시 이름을 얻는다.
    ///
    /// <para>
    /// 이름은 종류(TID)에 달린 값이라 개체 번호만으로는 못 찾는다 — 보유 목록에서 TID를 거쳐 간다.
    /// <c>GameDataLoader.GetCharacterName</c>에 개체 번호를 그대로 넣으면 <c>?#2</c>가 나온다.
    /// </para>
    /// </summary>
    public string GetCharacterName(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
                return GameDataLoader.GetCharacterName(character.CharacterTid);
        }

        return $"?#{characterId}"; // 아직 목록을 못 받았거나 서버가 모르는 개체
    }

    /// <summary>재화 보유량을 조회한다. 아직 통지받지 못한 종류는 0이다.</summary>
    public long GetCurrency(byte currencyType) => _currencies.TryGetValue(currencyType, out long amount) ? amount : 0L;

    /// <summary>골드 보유량 (GameData.CurrencyType.Gold = 1).</summary>
    public long Gold => GetCurrency((byte)GameData.CurrencyType.Gold);

    // ─── 가공 이벤트 (UI가 구독) ───
    public event Action<bool>?                  LoginCompleted;   // 로그인 완료 (성공 여부)
    public event Action?                        InventoryChanged; // 인벤토리 갱신됨 (스냅샷 반영 후)
    public event Action<List<GachaRewardInfo>>? GachaCompleted;   // 가챠 완료 (뽑힌 보상 목록)

    public event Action?                         CharactersChanged;          // 보유 캐릭터 캐시 갱신됨
    public event Action<bool, bool>?             WorkStationAssignCompleted; // 슬롯 변경 완료 (성공 여부, 배치=true/해제=false)
    public event Action?                         WorkStationSlotsChanged;    // 슬롯 캐시 갱신됨
    public event Action<S_GatherResultResponse>? GatherResultReceived;       // 채취 결과 푸시 도착
    public event Action?                         CurrencyChanged;            // 재화 캐시 갱신됨

    // 수신 이벤트 구독 (Unity 메시지)
    private void OnEnable()
    {
        ServerPacketHandler.LoginResponded           += OnLoginResponded;
        ServerPacketHandler.InventoryReceived        += OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               += OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    += OnCharacterListReceived;
        ServerPacketHandler.WorkStationAssigned      += OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived += OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     += OnGatherResultReceived;
        ServerPacketHandler.CurrencyReceived         += OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              += OnItemUpdated;
    }

    // 수신 이벤트 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        ServerPacketHandler.LoginResponded           -= OnLoginResponded;
        ServerPacketHandler.InventoryReceived        -= OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               -= OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    -= OnCharacterListReceived;
        ServerPacketHandler.WorkStationAssigned      -= OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived -= OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     -= OnGatherResultReceived;
        ServerPacketHandler.CurrencyReceived         -= OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              -= OnItemUpdated;
    }

    #region 요청 (UI가 호출)

    // 로그인 요청 — Id만 넘긴다.
    // ★ 이 한 번의 요청으로 인벤토리·작업슬롯 스냅샷까지 전부 따라온다(클래스 주석의 수신 세트 참조).
    public void Login(string id)
    {
        LoginId = id; // 서버가 닉네임을 돌려주지 않으므로 보낸 Id를 표시용으로 기억한다
        NetworkManager.Instance.Send(new C_LoginRequest { Id = id });

        StartLoginTimeoutWatch();
    }

    // 가챠 요청 — 로그인으로 User가 생성된 뒤에만 서버가 처리한다
    public void DrawGacha(int gachaId, int drawCount)
    {
        if (!CanSend("가챠"))
            return;

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
        if (!CanSend("작업슬롯 배치"))
            return;

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

    // 로그인이 끝났는지 확인한다. 아니면 보내지 않고 이유를 남긴다 (요청 메서드들이 호출)
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 말한다.
    private bool CanSend(string requestName)
    {
        if (IsLoggedIn)
            return true;

        ClientLog.Warn(ClientLog.Send, $"{requestName} 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");
        return false;
    }

    #endregion
    
    #region 응답 처리 (ServerPacketHandler 구독)

    // 로그인 응답 — 세션 상태 갱신 후 이벤트 발행
    private void OnLoginResponded(S_LoginResponse res)
    {
        StopLoginTimeoutWatch(); // 응답이 왔으니 무응답 감시는 끝

        IsLoggedIn = res.Result == EResultCode.Ok;
        SessionId  = res.SessionId;

        if (!IsLoggedIn)
            ClientLog.Warn(ClientLog.Recv, $"로그인 실패 — 결과={res.Result}");

        LoginCompleted?.Invoke(IsLoggedIn);
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

    // 가챠 응답 — 인벤토리 반영 후 보상 이벤트 발행
    // ★ 한 패킷에 두 가지가 실려 온다. 용도가 다르니 섞어 쓰지 않는다.
    //   ItemChangeInfos = 갱신 후 누적 총량 → 인벤토리 반영 (다른 경로와 같은 규칙)
    //   Rewards         = 이번에 뽑힌 개별 항목(델타) → 연출 전용
    private void OnGachaDrawn(S_GachaDrawResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLog.Warn(ClientLog.Recv, $"가챠 실패 — 결과={res.Result}");
            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        GachaCompleted?.Invoke(res.Rewards ?? new List<GachaRewardInfo>());
    }

    // 보유 캐릭터 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 여기 실린 CharacterId(개체 번호)가 슬롯 배치에 넣을 값이다.
    private void OnCharacterListReceived(S_CharacterListResponse res)
    {
        _characters.Clear();
        if (res.Characters != null)
            _characters.AddRange(res.Characters);

        if (_characters.Count == 0)
            ClientLog.Warn(ClientLog.Recv, "보유 캐릭터가 0마리다 — 작업슬롯 배치가 전부 거절된다(서버 지급 로직 확인)");

        CharactersChanged?.Invoke();
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
        bool success = res.Result == EResultCode.Ok;

        // 실패 사유는 결과 코드에만 들어 있다(미보유 캐릭터·적성 0·없는 슬롯…).
        // 여기서 남기지 않으면 UI는 "실패했다"까지만 알고 왜인지는 서버 콘솔을 봐야 안다.
        if (!success)
            ClientLog.Warn(ClientLog.Recv, $"작업슬롯 변경 실패 — 결과={res.Result}");

        if (success && changed != null)
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

        WorkStationAssignCompleted?.Invoke(success, wasAssign);
    }

    // 채취 결과 푸시 — 요청 없이 주기적으로 도착한다.
    // ★ 로그인 시에도 1회 올 수 있다(오프라인 누적분. 수확이 없으면 오지 않는다).
    //   로그인 인벤토리 스냅샷은 이 정산 "전" 값이라, 여기서 반영해야 실제 수량과 맞는다.
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        ApplyItemChanges(res.ItemChanges);
        GatherResultReceived?.Invoke(res);
    }

    // 아이템 증감 푸시 (가챠·즉시 지급 등)
    private void OnItemUpdated(S_UpdateItemResponse res)
    {
        ApplyItemChanges(res.ItemChangeInfos);
    }

    // 재화 통지 — 스냅샷과 변경이 같은 패킷이라 종류별로 덮어쓰기만 하면 된다.
    private void OnCurrencyReceived(S_CurrencyResponse res)
    {
        if (res.Currencies == null)
            return;

        foreach (var currency in res.Currencies)
            _currencies[currency.CurrencyType] = currency.Amount;

        CurrencyChanged?.Invoke();
    }

    #endregion

    #region 로그인 무응답 감시

    /// <summary>
    /// 로그인 응답이 제때 오는지 지켜본다.
    ///
    /// <para>
    /// ⚠️ <b>서버는 실패해도 응답을 안 보내는 경우가 있다.</b> 같은 Id가 이미 접속 중이면
    /// (끊겼는데 서버가 아직 모르는 좀비 세션 포함) 응답도 로그도 없이 요청을 버린다
    /// — 깃허브 이슈 #10, <c>UserManager.CreateUser</c>의 pid 중복 분기.
    /// 그러면 클라 화면에서는 <b>아무 일도 일어나지 않은 것</b>과 구분되지 않는다.
    /// 원인을 짚어 주는 로그라도 남긴다.
    /// </para>
    /// </summary>
    private void StartLoginTimeoutWatch()
    {
        StopLoginTimeoutWatch();
        _loginTimeoutWatch = StartCoroutine(WatchLoginResponse());
    }

    // 감시 중단 (응답 도착·재요청 시 호출)
    private void StopLoginTimeoutWatch()
    {
        if (_loginTimeoutWatch == null)
            return;

        StopCoroutine(_loginTimeoutWatch);
        _loginTimeoutWatch = null;
    }

    // 제한 시간까지 응답이 없으면 경고를 남긴다 (StartLoginTimeoutWatch가 시작)
    private IEnumerator WatchLoginResponse()
    {
        yield return new WaitForSecondsRealtime(LoginResponseTimeoutSeconds);

        _loginTimeoutWatch = null;

        ClientLog.Warn(ClientLog.Network,
            $"로그인 응답이 {LoginResponseTimeoutSeconds:F0}초 동안 없다 (Id={LoginId}). " +
            $"서버가 안 떠 있거나, 같은 Id가 이미 접속 중일 수 있다(서버 pid 중복 — 이슈 #10).");
    }

    #endregion

    #region 인벤토리 반영

    /// <summary>
    /// 아이템 변경분을 인벤토리 캐시에 반영하고 <see cref="InventoryChanged"/>를 발행한다.
    /// <b>Count는 델타가 아니라 갱신 후 누적 총량이다</b> — 더하지 말고 덮어쓴다
    /// (<c>PacketInfo.ItemChangeInfo</c> 주석).
    ///
    /// <para>
    /// <b>아이템이 늘어나는 모든 경로가 이 하나를 쓴다</b>(채취·즉시 지급·가챠).
    /// 수량은 전부 서버가 정한 값이고 클라는 계산하지 않는다 — 경로가 늘어도 규칙은 그대로다.
    /// </para>
    /// </summary>
    private void ApplyItemChanges(List<ItemChangeInfo>? changes)
    {
        if (changes == null || changes.Count == 0)
            return;

        foreach (var change in changes)
        {
            int index = _inventory.FindIndex(item => item.ItemId == change.ItemId);

            if (index >= 0)
                _inventory[index].Count = change.Count;
            else
                _inventory.Add(new ItemInfo { ItemId = change.ItemId, Count = change.Count });
        }

        InventoryChanged?.Invoke();
    }

    #endregion
}
