using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MikaProtocol;

/// <summary>
/// 로그인·가챠·작업슬롯 패킷 송신 테스트용 UI.
/// - 버튼 OnClick에 SendXxx() 메서드를 연결한다.
/// - 실제 요청·상태·응답은 SessionManager가 담당하고, 여기선 버튼 트리거 + 결과 로그만 본다.
/// - 서버(10050)에 접속된 상태여야 동작하며, 가챠·작업슬롯은 로그인 먼저 해야 처리된다.
/// - 채취 결과는 서버가 30초 주기로 밀어 준다. 다만 슬롯을 배치해야 판정이 시작되므로
///   로그인만 해서는 아무것도 오지 않는다 — SendWorkStationAssign()을 먼저 눌러야 한다.
/// </summary>
public class PacketTestPanelUI : MonoBehaviour
{
    [CenterHeader("로그인")]
    [SerializeField, Tooltip("로그인에 사용할 계정 Id")]
    private string _loginId = "test";

    [CenterHeader("가챠")]
    [SerializeField, Tooltip("뽑을 가챠 풀 Id")]
    private int _gachaId = 1;

    [CenterHeader("작업슬롯")]
    [SerializeField, Tooltip("배치할 슬롯 번호 (0부터)")]
    private int _slotIndex = 0;

    [SerializeField, Tooltip("배치할 산업. 현재 드롭 테이블이 있는 산업은 낚시(Fishing)뿐이다")]
    private GameData.ItemType _industry = GameData.ItemType.Fishing;

    // ⚠️ 배치에 넣는 캐릭터 값은 인스펙터에 적을 수 없다 — 서버가 발급한 개체 번호라 계정마다 다르다.
    //    로그인 때 받은 보유 목록에서 꺼낸다(SessionManager.FirstCharacterId).

    // 세션 매니저(서비스 로케이터로 획득) — Start에서 한 번 확보해 캐시한다.
    //
    // ⚠️ OnEnable에서 Get 하면 안 된다.
    //   Unity는 씬을 열 때 오브젝트마다 Awake → OnEnable 을 <b>이어서</b> 부른다. 모든 Awake가
    //   먼저 끝나는 게 아니다. 이 패널이 SessionManager보다 먼저 초기화되면 아직 Register 전이라
    //   Services.Get이 KeyNotFoundException을 던지고, _session이 null로 남아 버튼을 누를 때
    //   NullReferenceException으로 다시 터진다.
    //   모든 Awake 등록이 끝난 것이 보장되는 시점은 Start다 — MonoService 주석의 규칙 그대로다.
    private SessionManager _session = null!;

    // 구독 상태 — Start와 OnEnable 양쪽에서 구독을 시도하므로 중복 구독을 막는다.
    private bool _isSubscribed;

    // 서비스 확보 후 최초 구독 (Unity 메시지)
    private void Start()
    {
        _session = Services.Get<SessionManager>();
        Subscribe();
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        // Start 전이면 _session이 아직 없다. 최초 구독은 Start가 맡는다.
        if (_session != null)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 결과 로그용 이벤트 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed = true;

        _session.LoginCompleted             += OnLoginCompleted;
        _session.InventoryChanged           += OnInventoryChanged;
        _session.GachaCompleted             += OnGachaCompleted;
        _session.WorkStationAssignCompleted += OnWorkStationAssignCompleted;
        _session.WorkStationSlotsChanged    += OnWorkStationSlotsChanged;
        _session.GatherResultReceived       += OnGatherResultReceived;
    }

    // 결과 로그용 이벤트 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed = false;

        _session.LoginCompleted             -= OnLoginCompleted;
        _session.InventoryChanged           -= OnInventoryChanged;
        _session.GachaCompleted             -= OnGachaCompleted;
        _session.WorkStationAssignCompleted -= OnWorkStationAssignCompleted;
        _session.WorkStationSlotsChanged    -= OnWorkStationSlotsChanged;
        _session.GatherResultReceived       -= OnGatherResultReceived;
    }

    #region 송신 (버튼 OnClick)

    // 로그인 요청 (LoginBtn OnClick에 할당)
    public void SendLogin()
    {
        _session.Login(_loginId);
        ClientLog.Info(ClientLog.Send, $"로그인 요청 — Id={_loginId}");
    }

    // 단차(1회) 가챠 요청 (GachaSingleBtn OnClick에 할당)
    public void SendGachaSingle()
    {
        _session.DrawGacha(_gachaId, 1);
        ClientLog.Info(ClientLog.Send, $"가챠 요청 — 풀={_gachaId}, 1회");
    }

    // 10연차 가챠 요청 (GachaTenBtn OnClick에 할당)
    public void SendGachaTen()
    {
        _session.DrawGacha(_gachaId, 10);
        ClientLog.Info(ClientLog.Send, $"가챠 요청 — 풀={_gachaId}, 10회");
    }

    // 작업슬롯 배치 요청 (WorkStationAssignBtn OnClick에 할당)
    // 이걸 눌러야 서버가 해당 슬롯을 판정 대상으로 잡고 30초마다 채취 결과를 밀어 준다.
    public void SendWorkStationAssign()
    {
        // 서버는 캐릭터 종류(TID)가 아니라 개체 번호를 받는다. 보유 목록에서 꺼낸다.
        long characterId = _session.FirstCharacterId;
        if (characterId == 0)
        {
            ClientLog.Error(ClientLog.UI, "보유 캐릭터가 없어 배치할 수 없다 — 로그인부터 할 것.", this);
            return;
        }

        _session.AssignWorkStation(_slotIndex, (byte)_industry, characterId);
        ClientLog.Info(ClientLog.Send, $"작업슬롯 배치 요청 — 슬롯={_slotIndex}, 산업={_industry}, 캐릭터개체={characterId}");
    }

    // 작업슬롯 해제 요청 (WorkStationClearBtn OnClick에 할당)
    // 배치와 같은 패킷이고, 산업·캐릭터를 0으로 보내면 해제다. 이후 채취 푸시가 멈춘다.
    public void SendWorkStationClear()
    {
        _session.AssignWorkStation(_slotIndex, 0, 0);
        ClientLog.Info(ClientLog.Send, $"작업슬롯 해제 요청 — 슬롯={_slotIndex}");
    }

    #endregion

    #region 결과 로그 (SessionManager 이벤트 구독)

    // 로그인 결과 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        if (!success)
        {
            ClientLog.Warn(ClientLog.UI, "로그인 실패 — 이후 가챠·작업슬롯 요청은 서버가 처리하지 않는다");
            return;
        }

        ClientLog.Info(ClientLog.UI, $"로그인 성공 — 세션ID={_session.SessionId}");
    }

    // 인벤토리 갱신 (InventoryChanged 구독)
    private void OnInventoryChanged()
    {
        var inventory = _session.Inventory;
        if (inventory.Count == 0)
        {
            ClientLog.Info(ClientLog.UI, "인벤토리 비어 있음");
            return;
        }

        var lines = new StringBuilder($"인벤토리 {inventory.Count}종");
        foreach (var item in inventory)
            lines.Append($"\n    {GameDataLoader.GetItemName(item.ItemId)}(#{item.ItemId}) × {item.Count}");

        ClientLog.Info(ClientLog.UI, lines.ToString());
    }

    // 가챠 결과 (GachaCompleted 구독)
    // ※ 여기 오는 Rewards는 연출용(이번에 뽑힌 것)이다. 인벤토리 수량은 SessionManager가
    //   같은 패킷의 ItemChangeInfos(누적 총량)로 이미 반영했다 — 이 값을 더하면 두 배가 된다.
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        if (rewards.Count == 0)
        {
            ClientLog.Warn(ClientLog.UI, "가챠 성공 응답인데 보상이 비어 있다 — 서버 가챠 풀을 확인할 것");
            return;
        }

        var lines = new StringBuilder($"가챠 결과 {rewards.Count}개");
        foreach (var reward in rewards)
            lines.Append($"\n    [{reward.Rarity}] {GameDataLoader.GetItemName(reward.ItemId)}(#{reward.ItemId}) × {reward.Count}");

        ClientLog.Info(ClientLog.UI, lines.ToString());
    }

    // 작업슬롯 변경 결과 (WorkStationAssignCompleted 구독)
    // 배치와 해제가 같은 패킷이라 "성공"만 찍으면 둘을 구분할 수 없다.
    private void OnWorkStationAssignCompleted(bool success, bool wasAssign)
    {
        string action = wasAssign ? "배치" : "해제";

        if (!success)
        {
            // 실패 사유(결과 코드)는 SessionManager가 수신 시점에 이미 남긴다. 여기선 무엇을 하려 했는지만.
            ClientLog.Warn(ClientLog.UI, $"작업슬롯 {action} 실패");
            return;
        }

        ClientLog.Info(ClientLog.UI, $"작업슬롯 {action} 성공");
    }

    // 작업슬롯 갱신 (WorkStationSlotsChanged 구독)
    private void OnWorkStationSlotsChanged()
    {
        var slots = _session.WorkStationSlots;
        if (slots.Count == 0)
        {
            ClientLog.Info(ClientLog.UI, "작업슬롯 없음");
            return;
        }

        var lines = new StringBuilder($"작업슬롯 {slots.Count}칸");
        foreach (var slot in slots)
        {
            // 빈 슬롯은 캐릭터가 0이다. 이름을 조회하면 ?#0이 나오므로 "비어 있음"으로 적는다.
            string character = slot.CharacterId != 0
                ? $"{_session.GetCharacterName(slot.CharacterId)}(개체 {slot.CharacterId})"
                : "없음";

            lines.Append($"\n    {slot.SlotIndex}번 — 산업={(GameData.ItemType)slot.Industry}, " +
                         $"캐릭터={character}, 마지막판정={slot.LastTickAtUnix}");
        }

        ClientLog.Info(ClientLog.UI, lines.ToString());
    }

    // 채취 결과 푸시 (GatherResultReceived 구독)
    // 주기가 실제로 도는지는 로그의 시각 간격으로 확인한다.
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        var changes = res.ItemChanges;

        var lines = new StringBuilder(
            $"[{System.DateTime.Now:HH:mm:ss}] 채취 결과 — 슬롯 {res.SlotIndex}, 판정 {res.JudgeCount}회, 변경 {changes?.Count ?? 0}건");

        if (changes != null)
        {
            foreach (var change in changes)
                lines.Append($"\n    {GameDataLoader.GetItemName(change.ItemId)}(#{change.ItemId}) → 총 {change.Count} ({change.Kind})");
        }

        ClientLog.Info(ClientLog.UI, lines.ToString());
    }

    #endregion
}
