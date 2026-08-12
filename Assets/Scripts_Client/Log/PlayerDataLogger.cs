using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MikaProtocol;

/// <summary>
/// 서버가 밀어준 변경을 사람이 읽을 문장으로 풀어 콘솔에 남기는 관찰자.
/// 'PlayerDataModel'의 이벤트만 구독하고, 출력은 'ClientLogger'에 맡긴다.
/// </summary>
/// <remarks>
/// ⏸ 임시 발판이다 — 가챠 결과 팝업 · 실패 토스트 · 위젯 수확 표시가 생기면 지운다.
/// ⚠️ 수신만 맡는다 — 송신은 'ClientLogger'가 훅으로 자동 기록하므로
/// 여기서 또 찍으면 같은 줄이 두 번 나온다.
/// 분담과 이 클래스가 'UI/'가 아닌 이유는 'Log 규칙.md' 참조.
/// </remarks>
public class PlayerDataLogger : MonoBehaviour
{
    private PlayerDataModel _data = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 순서로 진행한다 (클라 공통 규약)
    // ⚠️ 서비스 조회는 반드시 Start — OnEnable 시점엔 아직 등록 전일 수 있다 (Managers 규칙.md 3장)
    private void Start()
    {
        _data = Services.Get<PlayerDataModel>();
        Subscribe();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 결과 로그용 이벤트 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed = true;

        _data.LoginCompleted             += OnLoginCompleted;
        _data.InventoryChanged           += OnInventoryChanged;
        _data.GachaCompleted             += OnGachaCompleted;
        _data.WorkStationAssignCompleted += OnWorkStationAssignCompleted;
        _data.WorkStationSlotsChanged    += OnWorkStationSlotsChanged;
        _data.GatherResultReceived       += OnGatherResultReceived;
    }

    // 결과 로그용 이벤트 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed = false;

        _data.LoginCompleted             -= OnLoginCompleted;
        _data.InventoryChanged           -= OnInventoryChanged;
        _data.GachaCompleted             -= OnGachaCompleted;
        _data.WorkStationAssignCompleted -= OnWorkStationAssignCompleted;
        _data.WorkStationSlotsChanged    -= OnWorkStationSlotsChanged;
        _data.GatherResultReceived       -= OnGatherResultReceived;
    }

    #endregion

    #region 결과 로그 (PlayerDataModel 이벤트 구독)

    // 로그인 결과 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        if (!success)
        {
            ClientLogger.Warn(ClientLogger.UI, "로그인 실패 — 이후 가챠·작업슬롯 요청은 서버가 처리하지 않는다");
            return;
        }

        ClientLogger.Info(ClientLogger.UI, $"로그인 성공 — 세션ID={_data.SessionId}");
    }

    // 인벤토리 갱신 (InventoryChanged 구독)
    private void OnInventoryChanged()
    {
        var inventory = _data.Inventory;
        if (inventory.Count == 0)
        {
            ClientLogger.Info(ClientLogger.UI, "인벤토리 비어 있음");
            return;
        }

        var lines = new StringBuilder($"인벤토리 {inventory.Count}종");
        foreach (var item in inventory)
            lines.Append($"\n    {GameDataLoader.GetItemName(item.ItemId)}(#{item.ItemId}) × {item.Count}");

        ClientLogger.Info(ClientLogger.UI, lines.ToString());
    }

    // 가챠 결과 (GachaCompleted 구독)
    // ※ 여기 오는 Rewards는 연출용(이번에 뽑힌 것)이다. 인벤토리 수량은 PlayerDataModel가
    //   같은 패킷의 ItemChangeInfos(누적 총량)로 이미 반영했다 — 이 값을 더하면 두 배가 된다.
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        if (rewards.Count == 0)
        {
            ClientLogger.Warn(ClientLogger.UI, "가챠 성공 응답인데 보상이 비어 있다 — 서버 가챠 풀을 확인할 것");
            return;
        }

        var lines = new StringBuilder($"가챠 결과 {rewards.Count}개");
        foreach (var reward in rewards)
            lines.Append($"\n    [{reward.Rarity}] {GameDataLoader.GetItemName(reward.ItemId)}(#{reward.ItemId}) × {reward.Count}");

        ClientLogger.Info(ClientLogger.UI, lines.ToString());
    }

    // 작업슬롯 변경 결과 (WorkStationAssignCompleted 구독)
    // ※ 배치였는지 해제였는지는 수신만으로 알 수 없다(실패 응답에 슬롯이 없다). 그건 버튼이 알고
    //   자기 로그에 남긴다 — 여기서는 서버가 받아들였는지만 본다.
    private void OnWorkStationAssignCompleted(bool success)
    {
        if (!success)
        {
            // 실패 사유(결과 코드)는 PlayerDataModel가 수신 시점에 이미 남긴다.
            ClientLogger.Warn(ClientLogger.UI, "작업슬롯 변경 실패");
            return;
        }

        ClientLogger.Info(ClientLogger.UI, "작업슬롯 변경 성공");
    }

    // 작업슬롯 갱신 (WorkStationSlotsChanged 구독)
    private void OnWorkStationSlotsChanged()
    {
        var slots = _data.WorkStationSlots;
        if (slots.Count == 0)
        {
            ClientLogger.Info(ClientLogger.UI, "작업슬롯 없음");
            return;
        }

        var lines = new StringBuilder($"작업슬롯 {slots.Count}칸");
        foreach (var slot in slots)
        {
            // 빈 슬롯은 캐릭터가 0이다. 이름을 조회하면 ?#0이 나오므로 "비어 있음"으로 적는다.
            string character = slot.CharacterId != 0
                ? $"{_data.GetCharacterName(slot.CharacterId)}(개체 {slot.CharacterId})"
                : "없음";

            lines.Append($"\n    {slot.SlotIndex}번 — 산업={slot.Industry}, " +
                         $"캐릭터={character}, 마지막판정={slot.LastTickAtUnixMs}");
        }

        ClientLogger.Info(ClientLogger.UI, lines.ToString());
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

        ClientLogger.Info(ClientLogger.UI, lines.ToString());
    }

    #endregion
}
