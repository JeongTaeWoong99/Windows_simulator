using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    public void SetSlotAutoUse(int slotIndex, int itemTid, bool on, DateTime now)
    {
        if (!WorkStation.TryGet(slotIndex, out var slot))
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.InvalidUseTarget, "없는 슬롯");
            return;
        }

        if (AutoUseEffectCount(itemTid) == 0)
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.NotAutoUsable, "자동 사용 대상 아님");
            return;
        }

        // ON/OFF가 지난 구간의 판정·소비에 소급되지 않게 정산부터 한다.
        SettleWorkStation(now);
        slot.SetAutoUse(itemTid, on);
        var before = slot.Buffs.ToDictionary();
        var items = new List<ItemChangeInfo>();
        slot.RefillAutoUse(tid => ConsumeAutoUseItem(tid, items), ShouldPreloadAutoUse);
        slot.ApplyWorkSpeed(ResolveSlotSpeed(slot));

        PostDBTask(new SaveSlotAutoUseRepository(this,
            new[] { new SlotAutoUseChange(slotIndex, itemTid, on) }, ChangedBuffs(slot, before), items));
        Send(new S_WorkStationSlotSyncResponse { Slot = slot.ToInfo() });
        Send(new S_SlotAutoUseResponse
        {
            Result = EResultCode.Ok, SlotIndex = slotIndex, ItemTID = itemTid, ItemChangeInfos = items,
        });
    }

    private void RejectAutoUse(int slotIndex, int itemTid, EResultCode code, string reason)
    {
        ServerLog.Warn("자동사용", $"거절 — {code} {reason}. Uid={Uid} Slot={slotIndex} ItemTID={itemTid}");
        Send(new S_SlotAutoUseResponse { Result = code, SlotIndex = slotIndex, ItemTID = itemTid });
    }

    private int AutoUseEffectCount(int itemTid)
        => _itemUses.TryGet(itemTid, out var use) && ItemUseCatalog.IsAutoUsable(use.UseEffect)
            ? use.EffectCount : 0;

    // 저장은 버프와 한 트랜잭션으로 호출자가 한다. 재고가 없어도 ON 설정은 남긴다.
    private int ConsumeAutoUseItem(int itemTid, List<ItemChangeInfo> changes)
    {
        var effectCount = AutoUseEffectCount(itemTid);
        if (effectCount <= 0 || !TryRemoveItems(new Dictionary<int, int> { [itemTid] = 1 }, out var consumed))
        {
            return 0;
        }

        changes.AddRange(consumed);
        return effectCount;
    }

    private bool ShouldPreloadAutoUse(int itemTid)
    {
        return _itemUses.TryGet(itemTid, out var use) &&
            (use.UseEffect == ItemUseEffect.SlotWorkSpeed || use.EffectCount > 1);
    }

    private void LoadSlotAutoUse(IReadOnlyList<SlotAutoUseRow> rows)
    {
        foreach (var row in rows)
        {
            if (WorkStation.TryGet(row.slot_index, out var slot) && AutoUseEffectCount(row.item_tid) > 0)
            {
                slot.SetAutoUse(row.item_tid, true);
            }
        }
    }

    private void RefillAutoUseAndSave(WorkStationSlot slot, bool notify = true)
    {
        var before = slot.Buffs.ToDictionary();
        var items = new List<ItemChangeInfo>();
        if (!slot.RefillAutoUse(tid => ConsumeAutoUseItem(tid, items), ShouldPreloadAutoUse))
        {
            return;
        }

        PostDBTask(new SaveSlotAutoUseRepository(this, Array.Empty<SlotAutoUseChange>(), ChangedBuffs(slot, before), items));
        if (notify)
        {
            Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = items });
        }
    }

    private static List<SlotBuffChange> ChangedBuffs(WorkStationSlot slot, IReadOnlyDictionary<int, int> before)
        => slot.Buffs.Keys.Union(before.Keys)
            .Where(tid => slot.Buffs.GetValueOrDefault(tid) != before.GetValueOrDefault(tid))
            .Select(tid => new SlotBuffChange(slot.SlotIndex, tid, slot.Buffs.GetValueOrDefault(tid))).ToList();

    // 같은 정산의 채취 보상이 이 칸에 쌓일 수 있으므로 마지막 확정값을 저장·통지한다.
    private List<ItemChangeInfo> ItemChangeSnapshot(IEnumerable<ItemChangeInfo> changes)
        => changes.DistinctBy(c => (c.Container, c.Slot)).Select(change =>
        {
            var storage = change.Container == EContainer.Warehouse ? Warehouse : Inventory;
            var found = storage.TryGetAt(change.Slot, out var item);
            return new ItemChangeInfo
            {
                Container = change.Container, Slot = change.Slot,
                ItemId = found ? item.Id : change.ItemId, Count = found ? item.Count : 0,
                Kind = found ? EItemChangeKind.Update : EItemChangeKind.Remove,
            };
        }).ToList();
}
