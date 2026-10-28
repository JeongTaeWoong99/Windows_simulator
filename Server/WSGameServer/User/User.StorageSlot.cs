using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>같은 격자 안에서 칸을 옮긴다. 목적지가 비었으면 이동, 차 있으면 교환.</summary>
    public void MoveStorageSlot(EContainer container, EStorageTab tab, int fromSlot, int toSlot)
    {
        if (!IsInGrid(container, fromSlot) || !IsInGrid(container, toSlot) || !HasTab(container, tab))
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        if (IsSharedEquipGrid(container, tab))
        {
            MoveEquipGrid(fromSlot, toSlot);
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

        if (IsStackTab(tab))
        {
            MoveResource(container, tab, fromSlot, toSlot);
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
        if (!HasTab(container, tab))
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        if (key == EStorageSortKey.Count && !IsStackTab(tab))
        {
            SendSlots(EResultCode.InvalidStorageSortKey, container, tab, new List<SlotChange>());
            return;
        }

        if (IsSharedEquipGrid(container, tab))
        {
            SortEquipGrid((StorageSortKey)key, order == EStorageSortOrder.Ascending);
            return;
        }

        if (IsStackTab(tab))
        {
            SortResource(container, tab, (StorageSortKey)key, order == EStorageSortOrder.Ascending);
            return;
        }

        var ordered = StorageSort.Order(SortEntries(container, tab), (StorageSortKey)key, order == EStorageSortOrder.Ascending);
        var changes = StorageSlots.Renumber(ordered, PendingSlots(container, tab));

        ApplySlotChanges(container, tab, changes);
        PostDBTask(new SaveStorageSlotsRepository(this, container, tab, changes));
        SendSlots(EResultCode.Ok, container, tab, changes);
    }

    /// <summary>한 보관함 안의 같은 자원(아티팩트) 칸을 앞 칸부터 합친다(T-175 · T-181). 정렬과 달리 칸 순서를 바꾸지 않는다.</summary>
    public void MergeStorage(EContainer container, EStorageTab tab = EStorageTab.Resource)
    {
        if (!HasTab(container, tab) || !IsStackTab(tab))
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        var changes = StacksOf(container, tab).Merge(StackLimitOf(tab));
        if (changes.Count > 0)
        {
            SaveStacks(tab, changes);
        }

        SendResourceSlots(container, tab, changes, changes.Where(c => c.Count > 0));
    }

    private void MoveResource(EContainer container, EStorageTab tab, int fromSlot, int toSlot)
    {
        var box     = StacksOf(container, tab);
        var changes = box.Rewrite(MoveStacks(box, fromSlot, toSlot, StackLimitOf(tab)));
        SaveStacks(tab, changes);
        SendResourceSlots(container, tab, changes, changes.Where(c => c.Count > 0));
    }

    // 같은 종류 칸에 놓으면 MaxStack까지 합치고 남는 것은 원래 칸에 둔다(T-160). 그 밖에는 이동·교환이다.
    // 한쪽 칸이 비었으면 그쪽은 빈 채로 간다 — 장비와 맞바꾸는 아티팩트 칸(T-188).
    private static List<Item> MoveStacks(Inventory box, int fromSlot, int toSlot, Func<int, int> maxStack)
    {
        box.TryGetAt(fromSlot, out var moving);
        box.TryGetAt(toSlot, out var target);

        var next = box.Items.Where(i => i.Slot != fromSlot && i.Slot != toSlot).ToList();
        if (moving is not null && target is not null && target.Id == moving.Id && target.Count < maxStack(moving.Id))
        {
            var merged = Math.Min(moving.Count, maxStack(moving.Id) - target.Count);
            next.Add(new Item(moving.Id, target.Count + merged, toSlot));
            if (moving.Count > merged)
            {
                next.Add(new Item(moving.Id, moving.Count - merged, fromSlot));
            }

            return next;
        }

        if (moving is not null)
        {
            next.Add(new Item(moving.Id, moving.Count, toSlot));
        }
        if (target is not null)
        {
            next.Add(new Item(target.Id, target.Count, fromSlot));
        }

        return next;
    }

    // 인벤토리 장비 탭 — 장비와 아티팩트 묶음이 한 격자에 선다(T-188). 장비끼리·아티팩트끼리·서로 맞바꾸기가 다 된다.
    private void MoveEquipGrid(int fromSlot, int toSlot)
    {
        var equips       = OccupiedSlots(EContainer.Inventory, EStorageTab.Equip);
        var fromArtifact = Artifacts.TryGetAt(fromSlot, out _);
        var hasArtifacts = fromArtifact || Artifacts.TryGetAt(toSlot, out _);
        if (!equips.ContainsKey(fromSlot) && !fromArtifact)
        {
            SendSlots(EResultCode.StorageSlotEmpty, EContainer.Inventory, EStorageTab.Equip, new List<SlotChange>());
            return;
        }

        if (fromSlot == toSlot)
        {
            SendSlots(EResultCode.Ok, EContainer.Inventory, EStorageTab.Equip, new List<SlotChange>());
            return;
        }

        if (PendingSlots(EContainer.Inventory, EStorageTab.Equip).Contains(toSlot))
        {
            SendSlots(EResultCode.StorageSlotPending, EContainer.Inventory, EStorageTab.Equip, new List<SlotChange>());
            return;
        }

        var equipChanges = new List<SlotChange>();
        if (equips.TryGetValue(fromSlot, out var moving))
        {
            equipChanges.Add(new SlotChange(moving, toSlot));
        }
        if (equips.TryGetValue(toSlot, out var other))
        {
            equipChanges.Add(new SlotChange(other, fromSlot));
        }

        SaveEquipSlots(equipChanges);
        if (hasArtifacts)
        {
            SaveStacks(EStorageTab.Artifact, Artifacts.Rewrite(MoveStacks(Artifacts, fromSlot, toSlot, StackLimitOf(EStorageTab.Artifact))));
        }

        SendSlots(EResultCode.Ok, EContainer.Inventory, EStorageTab.Equip, equipChanges);
        if (hasArtifacts)
        {
            SendArtifactInventory();
        }
    }

    // 장비를 지금 기준으로 앞에 세우고, 아티팩트는 같은 기준으로 그 뒤에 잇는다. 지급 대기 칸은 둘 다 건너뛴다.
    private void SortEquipGrid(StorageSortKey key, bool ascending)
    {
        var pending      = PendingSlots(EContainer.Inventory, EStorageTab.Equip);
        var equipOrder   = StorageSort.Order(SortEntries(EContainer.Inventory, EStorageTab.Equip), key, ascending);
        var equipChanges = StorageSlots.Renumber(equipOrder, pending);
        SaveEquipSlots(equipChanges);

        var stacks = StorageSort.CompactStacks(Artifacts.Items, StackLimitOf(EStorageTab.Artifact));
        var order  = StorageSort.Order(stacks.Select((stack, index) => ArtifactEntry(stack, index)).ToList(), key, ascending);
        var placed = StorageSlots.Renumber(order, equipChanges.Select(c => c.Slot).Concat(pending));
        var next   = placed.Select(p => new Item(stacks[(int)p.Key].Id, stacks[(int)p.Key].Count, p.Slot)).ToList();
        SaveStacks(EStorageTab.Artifact, Artifacts.Rewrite(next));

        SendSlots(EResultCode.Ok, EContainer.Inventory, EStorageTab.Equip, equipChanges);
        SendArtifactInventory();
    }

    private void SaveEquipSlots(List<SlotChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        ApplySlotChanges(EContainer.Inventory, EStorageTab.Equip, changes);
        PostDBTask(new SaveStorageSlotsRepository(this, EContainer.Inventory, EStorageTab.Equip, changes));
    }

    // 같은 자원의 덜 찬 칸을 먼저 합친 뒤 줄 세워 0부터 다시 놓는다. 자원은 지급 대기 칸이 없다.
    private void SortResource(EContainer container, EStorageTab tab, StorageSortKey key, bool ascending)
    {
        var box     = StacksOf(container, tab);
        var stacks  = StorageSort.CompactStacks(box.Items, StackLimitOf(tab));
        var entries = stacks.Select((stack, index) => tab == EStorageTab.Artifact ? ArtifactEntry(stack, index) : ResourceEntry(stack, index)).ToList();
        var ordered = StorageSort.Order(entries, key, ascending);

        var next    = ordered.Select((index, slot) => new Item(stacks[(int)index].Id, stacks[(int)index].Count, slot)).ToList();
        var changes = box.Rewrite(next);
        SaveStacks(tab, changes);
        SendResourceSlots(container, tab, changes, box.Snapshot().Select(i => new ItemChangeInfo { ItemId = i.ItemId, Slot = i.Slot }));
    }

    // 아티팩트 칸 — 표에 없는 번호는 적재 때 버려 여기 오지 않는다. 같은 등급 안에서는 번호순이다.
    private static StorageSortEntry ArtifactEntry((int Id, int Count) stack, int index)
    {
        GameTable.ArtifactTable.TryGet(stack.Id, out var row);
        return new StorageSortEntry(index, false, row?.Name ?? "", stack.Count,
            StorageSort.ResourceTieBreak((int)(row?.GlobalRarity ?? 0), 0, stack.Id, index));
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

    // 아티팩트 탭은 인벤토리에만 있다(T-181).
    private static bool HasTab(EContainer container, EStorageTab tab)
        => IsContainer(container) && (tab != EStorageTab.Artifact || container == EContainer.Inventory);

    // 인벤토리 장비 탭은 아티팩트 칸과 한 격자다(T-188) — 아티팩트 탭으로 온 이동·정렬도 여기로 간다.
    private static bool IsSharedEquipGrid(EContainer container, EStorageTab tab)
        => container == EContainer.Inventory && tab is EStorageTab.Equip or EStorageTab.Artifact;

    // 칸이 개체가 아니라 (종류, 수량) 묶음인 탭.
    private static bool IsStackTab(EStorageTab tab) => tab is EStorageTab.Resource or EStorageTab.Artifact;

    private Inventory ItemsOf(EContainer container)
        => container == EContainer.Warehouse ? Warehouse : Inventory;

    private Inventory StacksOf(EContainer container, EStorageTab tab)
        => tab == EStorageTab.Artifact ? Artifacts : ItemsOf(container);

    private static Func<int, int> StackLimitOf(EStorageTab tab)
        => tab == EStorageTab.Artifact ? _ => ArtifactMaxStack : MaxStackOf;

    private void SaveStacks(EStorageTab tab, List<ItemChangeInfo> changes)
    {
        if (tab == EStorageTab.Artifact)
        {
            PostDBTask(new SaveArtifactStacksRepository(this, changes));
            return;
        }

        PostDBTask(new SaveItemChangesRepository(this, changes));
    }

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
        if (IsStackTab(tab))
        {
            return StacksOf(container, tab).Items.Select(i => (i.Slot, (long)i.Id));
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
    private void SendResourceSlots(EContainer container, EStorageTab tab, List<ItemChangeInfo> changes, IEnumerable<ItemChangeInfo> legacy)
    {
        Send(new S_StorageSlotsResponse
        {
            Result    = EResultCode.Ok,
            Container = container,
            Tab       = tab,
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
