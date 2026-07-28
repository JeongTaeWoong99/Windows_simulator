using GameData;
using MikaProtocol;
using WSGameServer.Repository;

namespace WSGameServer.User;

public partial class User
{
    /// <summary>플레이어의 작업슬롯 전체. 채취는 여기서 시작된다.</summary>
    public WorkStation.WorkStation WorkStation { get; } = new();

    /// <summary>슬롯 전체 스냅샷을 보낸다(로그인 직후).</summary>
    public void SendWorkStationSlots()
    {
        Send(new S_WorkStationSlotsResponse { Slots = WorkStation.Snapshot() });
    }

    /// <summary>
    /// 모든 슬롯을 정산하고, 수확이 있으면 인벤토리에 넣은 뒤 <b>슬롯별로 결과를 밀어 준다.</b>
    ///
    /// <para>
    /// 로그인 · 슬롯 변경 · 30초 주기 푸시가 전부 이 함수를 부른다.
    /// 밀린 구간이 얼마든(오프라인 며칠이어도) 같은 경로로 처리된다.
    /// </para>
    /// </summary>
    /// <returns>정산된 슬롯 수.</returns>
    public int SettleWorkStation(DateTime now)
    {
        var harvests = WorkStation.Settle(now);
        if (harvests.Count == 0)
            return 0;

        foreach (var harvest in harvests)
        {
            // 아이템별로 한 번씩만 인벤토리를 갱신한다(판정 횟수만큼 UPSERT하지 않는다).
            var changes = new List<ItemChangeInfo>(harvest.Gained.Count);
            foreach (var (itemTid, count) in harvest.Gained)
                changes.Add(GainItem(itemTid, count));

            Send(new S_GatherResultResponse
            {
                SlotIndex   = harvest.SlotIndex,
                JudgeCount  = harvest.JudgeCount,
                ItemChanges = changes,
            });
        }

        // 정산으로 LastTickAt이 전진했으므로 저장한다. 이 값이 진행도의 단일 원본이라
        // 여기서 빠뜨리면 재시작 시 같은 구간을 다시 정산해 재화가 복제된다.
        SaveWorkStationSlots(harvests.Select(h => h.SlotIndex));
        return harvests.Count;
    }

    /// <summary>
    /// 슬롯에 산업과 캐릭터를 배치한다.
    /// <b>바꾸기 전에 먼저 정산한다</b> — 이전 구간은 이전 설정으로 계산돼야 한다.
    /// </summary>
    public void AssignWorkStation(int slotIndex, ItemType industry, long characterId)
    {
        var now = DateTime.UtcNow;

        if (!WorkStation.TryGet(slotIndex, out var slot))
        {
            Send(new S_WorkStationAssignResponse { Success = false });
            return;
        }

        // 배치 변경 전 구간 정산 (해당 슬롯뿐 아니라 전체를 정리해 둔다)
        SettleWorkStation(now);

        slot.Assign(industry, characterId, now);
        SaveWorkStationSlots(new[] { slotIndex });

        Send(new S_WorkStationAssignResponse { Success = true, Slot = slot.ToInfo() });
    }

    /// <summary>지정한 슬롯들을 DB에 반영한다.</summary>
    private void SaveWorkStationSlots(IEnumerable<int> slotIndexes)
    {
        var targets = slotIndexes
            .Select(i => WorkStation.TryGet(i, out var s) ? s : null)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        if (targets.Count > 0)
            PostDBTask(new SaveWorkStationSlotRepository(this, targets));
    }
}
