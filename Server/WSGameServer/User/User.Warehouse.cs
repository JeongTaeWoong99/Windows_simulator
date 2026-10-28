using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 창고 — 인벤토리 확장 보관소(T-107). 놓인 자리일 뿐이라 쓰는 데는 인벤토리와 같다(T-164) —
/// 조회(TryGetCharacter·TryGetEquip)와 자원 차감이 양쪽을 본다. 자원 지급은 창고의 같은 자원 칸부터(T-175), 개체는 인벤토리로 들어온다.
/// </summary>
public partial class User
{
    /// <summary>창고 탭마다의 칸 수 — Constants.xlsx. 클라는 한 줄 5칸으로 그린다.</summary>
    public static int WarehouseCapacity => (int)Constants.WarehouseCapacity;

    private Inventory Warehouse { get; } = new(EContainer.Warehouse, () => WarehouseCapacity);

    private readonly Dictionary<long, Character> _warehouseCharacters = new();

    private readonly Dictionary<long, Equip> _warehouseEquips = new();

    /// <summary>
    /// From 보관함의 칸을 반대 보관함의 칸에 놓는다. 비었으면 이동, 차 있으면 스왑.
    /// 자원은 같은 자원 칸이면 MaxStack까지 합치고, 다른 자원 칸이면 전량일 때만 스왑한다.
    /// </summary>
    public void TransferWarehouse(EContainer from, EStorageTab tab, int fromSlot, int toSlot, int count)
    {
        var to = from == EContainer.Inventory ? EContainer.Warehouse : EContainer.Inventory;
        // 아티팩트 탭은 창고에 없다(T-181).
        if (!IsInGrid(from, fromSlot) || !IsInGrid(to, toSlot) || tab == EStorageTab.Artifact)
        {
            SendTransfer(EResultCode.StorageSlotOutOfRange, tab, new List<WarehouseChangeInfo>());
            return;
        }

        if (PendingSlots(to, tab).Contains(toSlot))
        {
            SendTransfer(EResultCode.StorageSlotPending, tab, new List<WarehouseChangeInfo>());
            return;
        }

        // 인벤토리 장비 칸에 선 아티팩트는 창고로 못 가 맞바꿀 수 없다(T-188).
        if (tab == EStorageTab.Equip && to == EContainer.Inventory && Artifacts.TryGetAt(toSlot, out _))
        {
            SendTransfer(EResultCode.StorageSlotArtifact, tab, new List<WarehouseChangeInfo>());
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

    /// <summary>
    /// 인벤토리의 자원을 창고의 같은 자원 칸으로 MaxStack까지 보낸다(T-175). 창고에 없는 자원은 옮기지 않고,
    /// 창고 칸이 차서 남는 몫은 인벤토리에 그대로 둔다.
    /// </summary>
    public void StackToWarehouse()
    {
        var changes = new List<ItemChangeInfo>();
        foreach (var itemId in Inventory.Items.Select(i => i.Id).Distinct().ToList())
        {
            var stored = Warehouse.TopUp(itemId, Inventory.GetCount(itemId), MaxStackOf(itemId), out var moved);
            if (moved == 0)
            {
                continue;
            }

            Inventory.TryRemoveItems(new Dictionary<int, int> { [itemId] = moved }, out var taken);
            changes.AddRange(taken);
            changes.AddRange(stored);
        }

        if (changes.Count > 0)
        {
            PostDBTask(new WarehouseTransferRepository(this, EStorageTab.Resource, changes, new List<EntityMove>()));
        }

        SendTransfer(EResultCode.Ok, EStorageTab.Resource, changes
            .Select(c => new WarehouseChangeInfo { Container = c.Container, Key = c.ItemId, Slot = c.Slot, Count = c.Count })
            .ToList());
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

        var (result, plan) = PlanResource(src, dst, moving, toSlot, count);
        if (result != EResultCode.Ok)
        {
            SendTransfer(result, EStorageTab.Resource, new List<WarehouseChangeInfo>());
            return;
        }

        // 검사가 끝난 뒤에만 메모리를 바꾼다 — 거절이면 아무것도 바뀌지 않는다.
        var applied = plan.Select(p => ItemsOf(p.Container).SetSlot(p.Slot, p.ItemId, p.Count)).ToList();
        PostDBTask(new WarehouseTransferRepository(this, EStorageTab.Resource, applied, new List<EntityMove>()));
        SendTransfer(EResultCode.Ok, EStorageTab.Resource, applied
            .Select(c => new WarehouseChangeInfo { Container = c.Container, Key = c.ItemId, Slot = c.Slot, Count = c.Count })
            .ToList());
    }

    // 바꿀 두 칸의 최종 상태(수량 0 = 칸 비움)를 계산만 한다. 칸마다 묶음 하나라 놓은 칸만 본다(T-160).
    private static (EResultCode Result, List<ItemChangeInfo> Plan) PlanResource(
        Inventory src, Inventory dst, Item moving, int toSlot, int count)
    {
        var plan = new List<ItemChangeInfo>();
        if (!dst.TryGetAt(toSlot, out var target))
        {
            plan.Add(Change(src, moving.Id, moving.Count - count, moving.Slot));
            plan.Add(Change(dst, moving.Id, count, toSlot));
            return (EResultCode.Ok, plan);
        }

        // 같은 자원 칸이면 MaxStack까지만 합치고 남는 것은 원래 칸에 둔다.
        if (target.Id == moving.Id)
        {
            var merged = Math.Min(count, MaxStackOf(moving.Id) - target.Count);
            if (merged <= 0)
            {
                return (EResultCode.StorageOverMaxStack, plan);
            }

            plan.Add(Change(src, moving.Id, moving.Count - merged, moving.Slot));
            plan.Add(Change(dst, moving.Id, target.Count + merged, toSlot));
            return (EResultCode.Ok, plan);
        }

        if (count != moving.Count)
        {
            return (EResultCode.WarehousePartialSwap, plan);
        }

        plan.Add(Change(src, target.Id, target.Count, moving.Slot));
        plan.Add(Change(dst, moving.Id, moving.Count, toSlot));
        return (EResultCode.Ok, plan);
    }

    private static ItemChangeInfo Change(Inventory box, int itemId, int count, int slot)
        => new() { Container = box.Container, ItemId = itemId, Count = count, Slot = slot };

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
