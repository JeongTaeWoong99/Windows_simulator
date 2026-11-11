using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 작업슬롯 자동 사용 칸(이슈 #113) — 영약을 칸에 넣어 두면 같은 영약의 버프가 끝날 때 1개씩 꺼내 쓴다.
/// 넣은 것은 인벤토리·창고에서 칸으로 실제로 옮겨 가고, 언제든 꺼낼 수 있다. 실패하면 아무것도 바뀌지 않는다.
/// </summary>
public partial class User
{
    /// <summary>칸에 넣는다. 인벤토리 먼저, 모자란 만큼 창고에서 빠진다. 칸 한도는 그 아이템의 MaxStack이다.</summary>
    public void PutSlotAutoUse(int slotIndex, int itemTid, int count, DateTime now)
    {
        if (!TryResolveAutoUse(slotIndex, itemTid, count, out var slot, out var use))
        {
            return;
        }

        var inCell = slot.AutoUse.GetValueOrDefault(itemTid);
        if ((long)inCell + count > MaxStackOf(itemTid))
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.AutoUseSlotFull, $"칸 {inCell} + {count} > {MaxStackOf(itemTid)}");
            return;
        }

        if (!TryRemoveItems(new Dictionary<int, int> { [itemTid] = count }, out var itemChanges))
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.NotEnoughItem, $"보유 {GetItemCount(itemTid)} < {count}");
            return;
        }

        // 바로 꺼내 쓰면 속도가 바뀐다 — 정산을 먼저 해야 새 속도가 지난 구간에 소급되지 않는다.
        var isSpeed = use.UseEffect == ItemUseEffect.SlotWorkSpeed;
        if (isSpeed)
        {
            SettleWorkStation(now);
        }

        slot.PutAutoUse(itemTid, count);
        if (slot.RefillAutoUse(AutoUseEffectCount) && isSpeed)
        {
            slot.ApplyWorkSpeed(ResolveSlotSpeed(slot));
        }

        ReplyAutoUse(slot, itemTid, itemChanges);
    }

    /// <summary>칸에서 꺼낸다. 다른 획득과 같은 길로 돌아가고, 다 들어갈 자리가 없으면 거절한다.</summary>
    public void TakeSlotAutoUse(int slotIndex, int itemTid, int count)
    {
        if (!TryResolveAutoUse(slotIndex, itemTid, count, out var slot, out _))
        {
            return;
        }

        var inCell = slot.AutoUse.GetValueOrDefault(itemTid);
        if (count > inCell)
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.NotEnoughItem, $"칸 {inCell} < {count}");
            return;
        }

        // 사본으로 같은 순서를 밟아 다 들어가는지 먼저 본다 — 일부만 들어가면 나머지가 사라진다.
        Deposit(Warehouse.Clone(), Inventory.Clone(), itemTid, count, out var fits);
        if (fits < count)
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.StorageFull, $"자리 {fits} < {count}");
            return;
        }

        slot.TryTakeAutoUse(itemTid, count);
        var itemChanges = Deposit(Warehouse, Inventory, itemTid, count, out _);

        ReplyAutoUse(slot, itemTid, itemChanges);
    }

    // 슬롯·아이템·개수 검사. 거절했으면 이미 답했다.
    private bool TryResolveAutoUse(int slotIndex, int itemTid, int count, out WorkStationSlot slot, out ItemUseTableRow use)
    {
        use = null!;
        if (!WorkStation.TryGet(slotIndex, out slot))
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.InvalidUseTarget, "없는 슬롯");
            return false;
        }

        if (!_itemUses.TryGet(itemTid, out use) || !ItemUseCatalog.IsAutoUsable(use.UseEffect))
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.NotAutoUsable, "자동 사용 대상 아님");
            return false;
        }

        if (count < 1)
        {
            RejectAutoUse(slotIndex, itemTid, EResultCode.InvalidUseCount, $"개수 {count}");
            return false;
        }

        return true;
    }

    private void ReplyAutoUse(WorkStationSlot slot, int itemTid, List<ItemChangeInfo> itemChanges)
    {
        var cell = new SlotAutoUseChange(slot.SlotIndex, itemTid, slot.AutoUse.GetValueOrDefault(itemTid));
        var buff = new SlotBuffChange(slot.SlotIndex, itemTid, slot.Buffs.GetValueOrDefault(itemTid));
        PostDBTask(new SaveSlotAutoUseRepository(this, new[] { cell }, new[] { buff }, itemChanges));

        ServerLog.Info("자동사용", $"칸 Uid={Uid} Slot={slot.SlotIndex} ItemTID={itemTid} 칸={cell.Count} 잔여={buff.Remaining}");
        Send(new S_WorkStationSlotSyncResponse { Slot = slot.ToInfo() });
        Send(new S_SlotAutoUseResponse
        {
            Result          = EResultCode.Ok,
            SlotIndex       = slot.SlotIndex,
            ItemTID         = itemTid,
            ItemChangeInfos = new List<ItemChangeInfo>(itemChanges),
        });
    }

    private void RejectAutoUse(int slotIndex, int itemTid, EResultCode code, string reason)
    {
        ServerLog.Warn("자동사용", $"거절 — {code} {reason}. Uid={Uid} Slot={slotIndex} ItemTID={itemTid}");
        Send(new S_SlotAutoUseResponse { Result = code, SlotIndex = slotIndex, ItemTID = itemTid });
    }

    // 칸의 아이템 1개가 거는 횟수. 자동 사용 대상이 아니면 0 — 칸에서 꺼내지 않는다.
    private int AutoUseEffectCount(int itemTid)
    {
        if (!_itemUses.TryGet(itemTid, out var use) || !ItemUseCatalog.IsAutoUsable(use.UseEffect))
        {
            return 0;
        }

        return use.EffectCount;
    }

    /// <summary>DB의 칸 행을 슬롯에 얹는다(로그인 시 1회, 버프 다음). 없는 칸·대상이 아닌 아이템은 경고 후 버린다.</summary>
    private void LoadSlotAutoUse(IReadOnlyList<SlotAutoUseRow> rows)
    {
        foreach (var row in rows)
        {
            if (!WorkStation.TryGet(row.slot_index, out var slot) || AutoUseEffectCount(row.item_tid) == 0)
            {
                ServerLog.Warn("자동사용", $"쓸 수 없는 칸 행 — 버린다. Uid={Uid} Slot={row.slot_index} ItemTID={row.item_tid}");
                continue;
            }

            slot.PutAutoUse(row.item_tid, row.count);
        }
    }

    // 돌기 시작한 슬롯은 비어 있는 버프를 칸에서 바로 채운다. 속도는 호출자가 다시 매긴다.
    private void RefillAutoUseAndSave(WorkStationSlot slot)
    {
        var cellsBefore = slot.AutoUse.ToDictionary();
        if (!slot.RefillAutoUse(AutoUseEffectCount))
        {
            return;
        }

        var cells = AutoUseChanges(slot, cellsBefore).ToList();
        var buffs = cells.Select(c => new SlotBuffChange(slot.SlotIndex, c.ItemTid, slot.Buffs.GetValueOrDefault(c.ItemTid))).ToList();
        PostDBTask(new SaveSlotAutoUseRepository(this, cells, buffs));
    }

    // 이번에 바뀐 칸의 확정값. before는 바뀌기 전 칸이다.
    private static IEnumerable<SlotAutoUseChange> AutoUseChanges(WorkStationSlot slot, IReadOnlyDictionary<int, int> before)
    {
        foreach (var itemTid in before.Keys.Union(slot.AutoUse.Keys))
        {
            var count = slot.AutoUse.GetValueOrDefault(itemTid);
            if (count != before.GetValueOrDefault(itemTid))
            {
                yield return new SlotAutoUseChange(slot.SlotIndex, itemTid, count);
            }
        }
    }
}
