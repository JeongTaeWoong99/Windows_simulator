using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>같은 격자 안에서 칸을 옮긴다. 목적지가 비었으면 이동, 차 있으면 교환.</summary>
    public void MoveStorageSlot(EContainer container, EStorageTab tab, int fromSlot, int toSlot)
    {
        if (!IsInGrid(container, fromSlot) || !IsInGrid(container, toSlot))
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        var occupied = OccupiedSlots(container, tab);
        if (!occupied.ContainsKey(fromSlot))
        {
            SendSlots(EResultCode.StorageSlotEmpty, container, tab, new List<SlotChange>());
            return;
        }

        if (fromSlot == toSlot)
        {
            SendSlots(EResultCode.Ok, container, tab, new List<SlotChange>());
            return;
        }

        if (PendingSlots(container, tab).Contains(toSlot))
        {
            SendSlots(EResultCode.StorageSlotPending, container, tab, new List<SlotChange>());
            return;
        }

        if (tab == EStorageTab.Resource)
        {
            MoveResource(container, fromSlot, toSlot);
            return;
        }

        var changes = StorageSlots.Move(occupied, fromSlot, toSlot);
        ApplySlotChanges(container, tab, changes);
        PostDBTask(new SaveStorageSlotsRepository(this, container, tab, changes));
        SendSlots(EResultCode.Ok, container, tab, changes);
    }

    /// <summary>격자 전체를 정렬해 0부터 다시 매긴다. 응답에는 격자 전체를 싣는다.</summary>
    public void SortStorage(EContainer container, EStorageTab tab, EStorageSortKey key, EStorageSortOrder order)
    {
        if (!IsContainer(container))
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        if (key == EStorageSortKey.Count && tab != EStorageTab.Resource)
        {
            SendSlots(EResultCode.InvalidStorageSortKey, container, tab, new List<SlotChange>());
            return;
        }

        if (tab == EStorageTab.Resource)
        {
            SortResource(container, (StorageSortKey)key, order == EStorageSortOrder.Ascending);
            return;
        }

        var ordered = StorageSort.Order(SortEntries(container, tab), (StorageSortKey)key, order == EStorageSortOrder.Ascending);
        var changes = StorageSlots.Renumber(ordered, PendingSlots(container, tab));

        ApplySlotChanges(container, tab, changes);
        PostDBTask(new SaveStorageSlotsRepository(this, container, tab, changes));
        SendSlots(EResultCode.Ok, container, tab, changes);
    }

    // 같은 자원 칸에 놓으면 MaxStack까지 합치고 남는 것은 원래 칸에 둔다(T-160). 그 밖에는 이동·교환이다.
    private void MoveResource(EContainer container, int fromSlot, int toSlot)
    {
        var box = ItemsOf(container);
        box.TryGetAt(fromSlot, out var moving);

        var next = box.Items.Where(i => i.Slot != fromSlot && i.Slot != toSlot).ToList();
        if (box.TryGetAt(toSlot, out var target) && target.Id == moving.Id && target.Count < MaxStackOf(moving.Id))
        {
            var merged = Math.Min(moving.Count, MaxStackOf(moving.Id) - target.Count);
            next.Add(new Item(moving.Id, target.Count + merged, toSlot));
            if (moving.Count > merged)
            {
                next.Add(new Item(moving.Id, moving.Count - merged, fromSlot));
            }
        }
        else
        {
            next.Add(new Item(moving.Id, moving.Count, toSlot));
            if (target is not null)
            {
                next.Add(new Item(target.Id, target.Count, fromSlot));
            }
        }

        var changes = box.Rewrite(next);
        PostDBTask(new SaveItemChangesRepository(this, changes));
        SendResourceSlots(container, changes, changes.Where(c => c.Count > 0));
    }

    // 같은 자원의 덜 찬 칸을 먼저 합친 뒤 줄 세워 0부터 다시 놓는다. 자원은 지급 대기 칸이 없다.
    private void SortResource(EContainer container, StorageSortKey key, bool ascending)
    {
        var box     = ItemsOf(container);
        var stacks  = StorageSort.CompactStacks(box.Items, MaxStackOf);
        var entries = stacks.Select((stack, index) => ResourceEntry(stack, index)).ToList();
        var ordered = StorageSort.Order(entries, key, ascending);

        var next    = ordered.Select((index, slot) => new Item(stacks[(int)index].Id, stacks[(int)index].Count, slot)).ToList();
        var changes = box.Rewrite(next);
        PostDBTask(new SaveItemChangesRepository(this, changes));
        SendResourceSlots(container, changes, box.Snapshot().Select(i => new ItemChangeInfo { ItemId = i.ItemId, Slot = i.Slot }));
    }

    // 키는 묶음 순번이다 — 같은 자원이 여러 칸이라 TID로는 가를 수 없다. 표에 없는 TID는 등급·산업 0, 이름 ""이다.
    private static StorageSortEntry ResourceEntry((int Id, int Count) stack, int index)
    {
        if (!GameTable.ItemTable.TryGet(stack.Id, out var row))
        {
            return new StorageSortEntry(index, false, "", stack.Count, StorageSort.ResourceTieBreak(0, 0, stack.Id, index));
        }

        return new StorageSortEntry(index, false, row.Name, stack.Count,
            StorageSort.ResourceTieBreak((int)row.GlobalRarity, (int)row.ItemType, stack.Id, index));
    }

    // 표에 없는 TID는 등급·산업 0, 이름 ""이다 — 클라도 같은 칸을 '?#'으로 그리고 뒤로 민다.
    private List<StorageSortEntry> SortEntries(EContainer container, EStorageTab tab)
    {
        if (tab == EStorageTab.Character)
        {
            var placed = WorkStation.Slots.Select(s => s.CharacterId).ToHashSet();
            return CharactersOf(container).Values
                .Select(c => new StorageSortEntry(c.Id, placed.Contains(c.Id), c.Name, 1,
                    StorageSort.CharacterTieBreak((int)c.Row.GlobalRarity, c.Tid, c.Id)))
                .ToList();
        }

        return EquipsOf(container).Values
            .Select(e => new StorageSortEntry(e.Id, e.IsEquipped, e.Row.Name, 1,
                StorageSort.EquipTieBreak((int)e.Row.GlobalRarity, (int)e.Kind, (int)e.Industry, e.Tid, e.Id)))
            .ToList();
    }

    private static bool IsContainer(EContainer container)
        => container is EContainer.Inventory or EContainer.Warehouse;

    private static int CapacityOf(EContainer container)
        => container == EContainer.Warehouse ? WarehouseCapacity : StorageCapacity;

    private static bool IsInGrid(EContainer container, int slot)
        => IsContainer(container) && slot >= 0 && slot < CapacityOf(container);

    private Inventory ItemsOf(EContainer container)
        => container == EContainer.Warehouse ? Warehouse : Inventory;

    private Dictionary<long, Character> CharactersOf(EContainer container)
        => container == EContainer.Warehouse ? _warehouseCharacters : _characters;

    private Dictionary<long, Equip> EquipsOf(EContainer container)
        => container == EContainer.Warehouse ? _warehouseEquips : _equips;

    // 칸 → 키. 장착·배치 중인 개체도 칸을 차지한다(2026-09-25 결정).
    // 칸이 겹친 데이터가 있어도 예외로 요청을 삼키지 않는다 — 먼저 온 것만 잡고, 정렬하면 풀린다.
    private Dictionary<int, long> OccupiedSlots(EContainer container, EStorageTab tab)
    {
        var occupied = new Dictionary<int, long>();
        foreach (var (slot, key) in SlotsOf(container, tab))
        {
            occupied.TryAdd(slot, key);
        }

        return occupied;
    }

    private IEnumerable<(int Slot, long Key)> SlotsOf(EContainer container, EStorageTab tab)
    {
        if (tab == EStorageTab.Resource)
        {
            return ItemsOf(container).Items.Select(i => (i.Slot, (long)i.Id));
        }

        if (tab == EStorageTab.Character)
        {
            return CharactersOf(container).Values.Select(c => (c.Slot, c.Id));
        }

        return EquipsOf(container).Values.Select(e => (e.SlotPosition, e.Id));
    }

    // 지급 대기 칸 — PK가 오기 전이라 개체는 없지만 칸은 이미 주인이 있다. 자원은 지급이 즉시라 없다.
    // 지급은 언제나 인벤토리로 들어오므로 창고에는 대기 칸이 없다.
    private IReadOnlyCollection<int> PendingSlots(EContainer container, EStorageTab tab)
    {
        if (container != EContainer.Inventory)
        {
            return Array.Empty<int>();
        }

        if (tab == EStorageTab.Character)
        {
            return _pendingCharacterSlots;
        }

        if (tab == EStorageTab.Equip)
        {
            return _pendingEquipPositions;
        }

        return Array.Empty<int>();
    }

    private void ApplySlotChanges(EContainer container, EStorageTab tab, IReadOnlyList<SlotChange> changes)
    {
        foreach (var change in changes)
        {
            if (tab == EStorageTab.Character && CharactersOf(container).TryGetValue(change.Key, out var character))
            {
                character.Slot = change.Slot;
            }
            else if (tab == EStorageTab.Equip && EquipsOf(container).TryGetValue(change.Key, out var equip))
            {
                equip.SlotPosition = change.Slot;
            }
        }
    }

    // Items가 정본이다. Slots(Key = ItemId)는 다중 칸 표시(T-161) 전 클라를 위한 옛 형식이다.
    private void SendResourceSlots(EContainer container, List<ItemChangeInfo> changes, IEnumerable<ItemChangeInfo> legacy)
    {
        Send(new S_StorageSlotsResponse
        {
            Result    = EResultCode.Ok,
            Container = container,
            Tab       = EStorageTab.Resource,
            Slots     = legacy.Select(c => new StorageSlotInfo { Key = c.ItemId, Slot = c.Slot }).ToList(),
            Items     = changes,
        });
    }

    private void SendSlots(EResultCode result, EContainer container, EStorageTab tab, IReadOnlyList<SlotChange> changes)
    {
        Send(new S_StorageSlotsResponse
        {
            Result    = result,
            Container = container,
            Tab       = tab,
            Slots     = changes.Select(c => new StorageSlotInfo { Key = c.Key, Slot = c.Slot }).ToList(),
        });
    }
}
