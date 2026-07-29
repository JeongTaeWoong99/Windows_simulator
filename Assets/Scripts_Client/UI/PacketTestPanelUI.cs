using System.Collections.Generic;
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

    [SerializeField, Tooltip("배치할 캐릭터 Id. 캐릭터 시스템 전이라 서버는 0인지 아닌지만 본다")]
    private long _characterId = 1;

    // 세션 매니저(서비스 로케이터로 획득) — OnEnable에서 한 번 확보해 캐시한다.
    // 모든 MonoService의 Awake 등록이 끝난 뒤 OnEnable이 돌므로 이 시점엔 조회가 안전하다.
    private SessionManager _session = null!;

    // 결과 로그용 이벤트 구독 (Unity 메시지)
    private void OnEnable()
    {
        _session = Services.Get<SessionManager>();
        
        _session.LoginCompleted             += OnLoginCompleted;
        _session.InventoryChanged           += OnInventoryChanged;
        _session.GachaCompleted             += OnGachaCompleted;
        _session.WorkStationAssignCompleted += OnWorkStationAssignCompleted;
        _session.WorkStationSlotsChanged    += OnWorkStationSlotsChanged;
        _session.GatherResultReceived       += OnGatherResultReceived;
    }

    // 결과 로그용 이벤트 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
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
        Debug.Log($"[Client] Send Login: id={_loginId}");
    }

    // 단차(1회) 가챠 요청 (GachaSingleBtn OnClick에 할당)
    public void SendGachaSingle()
    {
        _session.DrawGacha(_gachaId, 1);
        Debug.Log($"[Client] Send Gacha: gachaId={_gachaId}, drawCount=1");
    }

    // 10연차 가챠 요청 (GachaTenBtn OnClick에 할당)
    public void SendGachaTen()
    {
        _session.DrawGacha(_gachaId, 10);
        Debug.Log($"[Client] Send Gacha: gachaId={_gachaId}, drawCount=10");
    }

    // 작업슬롯 배치 요청 (WorkStationAssignBtn OnClick에 할당)
    // 이걸 눌러야 서버가 해당 슬롯을 판정 대상으로 잡고 30초마다 채취 결과를 밀어 준다.
    public void SendWorkStationAssign()
    {
        _session.AssignWorkStation(_slotIndex, (byte)_industry, _characterId);
        Debug.Log($"[Client] Send WorkStationAssign: slotIndex={_slotIndex}, industry={_industry}, characterId={_characterId}");
    }

    // 작업슬롯 해제 요청 (WorkStationClearBtn OnClick에 할당)
    // 배치와 같은 패킷이고, 산업·캐릭터를 0으로 보내면 해제다. 이후 채취 푸시가 멈춘다.
    public void SendWorkStationClear()
    {
        _session.AssignWorkStation(_slotIndex, 0, 0);
        Debug.Log($"[Client] Send WorkStationClear: slotIndex={_slotIndex}");
    }

    #endregion

    #region 결과 로그 (SessionManager 이벤트 구독)

    // 로그인 결과 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        Debug.Log($"[Client] 로그인 {(success ? "성공" : "실패")} — sessionId={_session.SessionId}");
    }

    // 인벤토리 갱신 (InventoryChanged 구독)
    private void OnInventoryChanged()
    {
        var inventory = _session.Inventory;
        if (inventory.Count == 0)
        {
            Debug.Log("[Client] 인벤토리 비어있음");
            return;
        }

        Debug.Log($"[Client] 인벤토리 {inventory.Count}종:");
        foreach (var item in inventory)
            Debug.Log($"    itemId={item.ItemId}, count={item.Count}");
    }

    // 가챠 결과 (GachaCompleted 구독)
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        Debug.Log($"[Client] 가챠 결과 {rewards.Count}개:");
        foreach (var reward in rewards)
            Debug.Log($"    itemId={reward.ItemId}, count={reward.Count}, rarity={reward.Rarity}");
    }

    // 작업슬롯 배치 결과 (WorkStationAssignCompleted 구독)
    private void OnWorkStationAssignCompleted(bool success)
    {
        Debug.Log($"[Client] 작업슬롯 배치 {(success ? "성공" : "실패")}");
    }

    // 작업슬롯 갱신 (WorkStationSlotsChanged 구독)
    private void OnWorkStationSlotsChanged()
    {
        var slots = _session.WorkStationSlots;
        if (slots.Count == 0)
        {
            Debug.Log("[Client] 작업슬롯 없음");
            return;
        }

        Debug.Log($"[Client] 작업슬롯 {slots.Count}칸:");
        foreach (var slot in slots)
            Debug.Log($"    slotIndex={slot.SlotIndex}, industry={(GameData.ItemType)slot.Industry}, characterId={slot.CharacterId}, lastTickAtUnix={slot.LastTickAtUnix}");
    }

    // 채취 결과 푸시 (GatherResultReceived 구독)
    // 30초 주기가 실제로 도는지는 로그의 시각 간격으로 확인한다.
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        var changes = res.ItemChanges;
        Debug.Log($"[Client] [{System.DateTime.Now:HH:mm:ss}] 채취 결과 — slotIndex={res.SlotIndex}, 판정 {res.JudgeCount}회, 변경 {changes?.Count ?? 0}건:");

        if (changes == null)
            return;

        foreach (var change in changes)
            Debug.Log($"    itemId={change.ItemId}, count={change.Count}, kind={change.Kind}");
    }

    #endregion
}
