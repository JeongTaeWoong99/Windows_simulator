using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 창고 — 인벤토리 확장 보관소(T-107). 놓인 자리일 뿐이라 쓰는 데는 인벤토리와 같다(T-164) —
/// 조회(TryGetCharacter·TryGetEquip)와 자원 차감이 양쪽을 본다. 지급만 언제나 인벤토리로 들어온다.
/// </summary>
public partial class User
{
    /// <summary>창고 탭마다의 칸 수 — Constants.xlsx. 클라는 한 줄 5칸으로 그린다.</summary>
    public static int WarehouseCapacity => (int)Constants.WarehouseCapacity;

    private Inventory Warehouse { get; } = new(EContainer.Warehouse);

    private readonly Dictionary<long, Character> _warehouseCharacters = new();

    private readonly Dictionary<long, Equip> _warehouseEquips = new();

    /// <summary>
    /// From 보관함의 칸을 반대 보관함의 칸에 놓는다. 비었으면 이동, 차 있으면 스왑.
    /// 자원은 같은 자원이 받는 쪽에 있으면 합치고, 다른 자원 칸이면 전량일 때만 스왑한다.
    /// </summary>
    public void TransferWarehouse(EContainer from, EStorageTab tab, int fromSlot, int toSlot, int count)
    {
        var to = from == EContainer.Inventory ? EContainer.Warehouse : EContainer.Inventory;
        if (!IsInGrid(from, fromSlot) || !IsInGrid(to, toSlot))
        {
            SendTransfer(EResultCode.StorageSlotOutOfRange, tab, new List<WarehouseChangeInfo>());
            return;
        }

        if (PendingSlots(to, tab).Contains(toSlot))
        {
            SendTransfer(EResultCode.StorageSlotPending, tab, new List<WarehouseChangeInfo>());
            return;
        }

        if (tab == EStorageTab.Resource)
        {
            TransferResource(from, to, fromSlot, toSlot, count);
            return;
        }

        if (tab == EStorageTab.Character)
        {
            TransferEntity(tab, from, to, fromSlot, toSlot, CharactersOf(from), CharactersOf(to),
                c => c.Id, c => c.Slot, (c, slot) => c.Slot = slot);
            return;
        }

        TransferEntity(tab, from, to, fromSlot, toSlot, EquipsOf(from), EquipsOf(to),
            e => e.Id, e => e.SlotPosition, (e, slot) => e.SlotPosition = slot);
    }

    private void TransferResource(EContainer from, EContainer to, int fromSlot, int toSlot, int count)
    {
        var src = ItemsOf(from);
        var dst = ItemsOf(to);
        if (!src.TryGetAt(fromSlot, out var moving))
        {
            SendTransfer(EResultCode.StorageSlotEmpty, EStorageTab.Resource, new List<WarehouseChangeInfo>());
            return;
        }

        if (count <= 0 || count > moving.Count)
        {
            SendTransfer(EResultCode.NotEnoughItem, EStorageTab.Resource, new List<WarehouseChangeInfo>());
            return;
        }

        var (result, changes) = PlanResource(src, dst, moving.Id, moving.Count, fromSlot, toSlot, count);
        if (result != EResultCode.Ok)
        {
            SendTransfer(result, EStorageTab.Resource, new List<WarehouseChangeInfo>());
            return;
        }

        // 검사가 끝난 뒤에만 메모리를 바꾼다 — 거절이면 아무것도 바뀌지 않는다.
        var applied = changes.Select(c => ItemsOf(c.Container).SetItem(c.ItemId, c.Count, c.Slot)).ToList();
        PostDBTask(new WarehouseTransferRepository(this, EStorageTab.Resource, applied, new List<EntityMove>()));
        SendTransfer(EResultCode.Ok, EStorageTab.Resource, applied
            .Select(c => new WarehouseChangeInfo { Container = c.Container, Key = c.ItemId, Slot = c.Slot, Count = c.Count })
            .ToList());
    }

    // 바꿀 칸의 최종 상태(수량 0 = 칸 비움)를 계산만 한다.
    private (EResultCode Result, List<ItemChangeInfo> Changes) PlanResource(
        Inventory src, Inventory dst, int itemId, int held, int fromSlot, int toSlot, int count)
    {
        var plan = new List<ItemChangeInfo>();

        // 같은 자원이 받는 쪽에 있으면 놓은 칸과 무관하게 그 칸에 합친다 — 보관함마다 같은 자원은 한 칸이다.
        if (dst.TryGet(itemId, out var same))
        {
            if (same.Count + count > MaxStackOf(itemId))
            {
                return (EResultCode.StorageOverMaxStack, plan);
            }

            plan.Add(Change(src, itemId, held - count, fromSlot));
            plan.Add(Change(dst, itemId, same.Count + count, same.Slot));
            return (EResultCode.Ok, plan);
        }

        if (!dst.TryGetAt(toSlot, out var other))
        {
            plan.Add(Change(src, itemId, held - count, fromSlot));
            plan.Add(Change(dst, itemId, count, toSlot));
            return (EResultCode.Ok, plan);
        }

        if (count != held)
        {
            return (EResultCode.WarehousePartialSwap, plan);
        }

        // 스왑으로 돌아오는 자원이 보내는 쪽에 이미 있으면 그 칸에 합치고, 비운 칸은 빈 채로 둔다.
        plan.Add(Change(src, itemId, 0, fromSlot));
        if (src.TryGet(other.Id, out var back))
        {
            if (back.Count + other.Count > MaxStackOf(other.Id))
            {
                return (EResultCode.StorageOverMaxStack, new List<ItemChangeInfo>());
            }

            plan.Add(Change(src, other.Id, back.Count + other.Count, back.Slot));
        }
        else
        {
            plan.Add(Change(src, other.Id, other.Count, fromSlot));
        }

        plan.Add(Change(dst, other.Id, 0, toSlot));
        plan.Add(Change(dst, itemId, held, toSlot));
        return (EResultCode.Ok, plan);
    }

    private static ItemChangeInfo Change(Inventory box, int itemId, int count, int slot)
        => new() { Container = box.Container, ItemId = itemId, Count = count, Slot = slot };

    // 표에 없는 TID는 상한 없이 둔다 — ClampToMaxStack과 같은 정책.
    private static int MaxStackOf(int itemId)
    {
        if (!GameTable.ItemTable.TryGet(itemId, out var row))
        {
            return int.MaxValue;
        }

        return row.MaxStack;
    }

    // 캐릭터·장비. 배치·착용 중이어도 옮긴다 — 슬롯·착용 매핑은 개체 id를 가리키므로 보관함이 바뀌어도 그대로다.
    private void TransferEntity<T>(EStorageTab tab, EContainer from, EContainer to, int fromSlot, int toSlot,
        Dictionary<long, T> src, Dictionary<long, T> dst,
        Func<T, long> idOf, Func<T, int> slotOf, Action<T, int> setSlot) where T : class
    {
        var moving = src.Values.FirstOrDefault(e => slotOf(e) == fromSlot);
        if (moving is null)
        {
            SendTransfer(EResultCode.StorageSlotEmpty, tab, new List<WarehouseChangeInfo>());
            return;
        }

        var other = dst.Values.FirstOrDefault(e => slotOf(e) == toSlot);

        var moves = new List<EntityMove> { new(idOf(moving), to, toSlot) };
        src.Remove(idOf(moving));
        if (other is not null)
        {
            dst.Remove(idOf(other));
            setSlot(other, fromSlot);
            src[idOf(other)] = other;
            moves.Add(new EntityMove(idOf(other), from, fromSlot));
        }

        setSlot(moving, toSlot);
        dst[idOf(moving)] = moving;

        PostDBTask(new WarehouseTransferRepository(this, tab, new List<ItemChangeInfo>(), moves));
        SendTransfer(EResultCode.Ok, tab, moves
            .Select(m => new WarehouseChangeInfo { Container = m.Container, Key = m.Id, Slot = m.Slot, Count = 1 })
            .ToList());
    }

    private void SendTransfer(EResultCode result, EStorageTab tab, List<WarehouseChangeInfo> changes)
    {
        if (result != EResultCode.Ok)
        {
            ServerLog.Warn("창고", $"이동 거절 {result} Uid={Uid} 탭={tab}");
        }

        Send(new S_WarehouseTransferResponse { Result = result, Tab = tab, Changes = changes });
    }
}
