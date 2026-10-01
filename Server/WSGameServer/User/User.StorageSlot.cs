using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>같은 격자 안에서 칸을 옮긴다. 목적지가 비었으면 이동, 차 있으면 교환.</summary>
    public void MoveStorageSlot(EContainer container, EStorageTab tab, int fromSlot, int toSlot)
    {
        if (container != EContainer.Inventory || !IsInGrid(fromSlot) || !IsInGrid(toSlot))
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        var occupied = OccupiedSlots(tab);
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

        var changes = StorageSlots.Move(occupied, fromSlot, toSlot);
        ApplySlotChanges(tab, changes);
        PostDBTask(new SaveStorageSlotsRepository(this, tab, changes));
        SendSlots(EResultCode.Ok, container, tab, changes);
    }

    /// <summary>격자 전체를 정렬해 0부터 다시 매긴다. 응답에는 격자 전체를 싣는다.</summary>
    public void SortStorage(EContainer container, EStorageTab tab, EStorageSortKey key, EStorageSortOrder order)
    {
        if (container != EContainer.Inventory)
        {
            SendSlots(EResultCode.StorageSlotOutOfRange, container, tab, new List<SlotChange>());
            return;
        }

        if (key == EStorageSortKey.Count && tab != EStorageTab.Resource)
        {
            SendSlots(EResultCode.InvalidStorageSortKey, container, tab, new List<SlotChange>());
            return;
        }

        var ordered = StorageSort.Order(SortEntries(tab), (StorageSortKey)key, order == EStorageSortOrder.Ascending);
        var changes = StorageSlots.Renumber(ordered);

        ApplySlotChanges(tab, changes);
        PostDBTask(new SaveStorageSlotsRepository(this, tab, changes));
        SendSlots(EResultCode.Ok, container, tab, changes);
    }

    // 표에 없는 TID는 등급·산업 0, 이름 ""이다 — 클라도 같은 칸을 '?#'으로 그리고 뒤로 민다.
    private List<StorageSortEntry> SortEntries(EStorageTab tab)
    {
        if (tab == EStorageTab.Resource)
        {
            return Inventory.Items.Select(ResourceEntry).ToList();
        }

        if (tab == EStorageTab.Character)
        {
            var placed = WorkStation.Slots.Select(s => s.CharacterId).ToHashSet();
            return _characters.Values
                .Select(c => new StorageSortEntry(c.Id, placed.Contains(c.Id), c.Name, 1,
                    StorageSort.CharacterTieBreak((int)c.Row.GlobalRarity, c.Tid, c.Id)))
                .ToList();
        }

        return _equips.Values
            .Select(e => new StorageSortEntry(e.Id, e.IsEquipped, e.Row.Name, 1,
                StorageSort.EquipTieBreak((int)e.Row.GlobalRarity, (int)e.Kind, (int)e.Industry, e.Tid, e.Id)))
            .ToList();
    }

    private static StorageSortEntry ResourceEntry(Item item)
    {
        if (!GameTable.ItemTable.TryGet(item.Id, out var row))
        {
            return new StorageSortEntry(item.Id, false, "", item.Count, StorageSort.ResourceTieBreak(0, 0, item.Id));
        }

        return new StorageSortEntry(item.Id, false, row.Name, item.Count,
            StorageSort.ResourceTieBreak((int)row.GlobalRarity, (int)row.ItemType, item.Id));
    }

    private static bool IsInGrid(int slot) => slot >= 0 && slot < StorageCapacity;

    // 칸 → 키. 장착·배치 중인 개체도 칸을 차지한다(2026-09-25 결정).
    private Dictionary<int, long> OccupiedSlots(EStorageTab tab)
    {
        if (tab == EStorageTab.Resource)
        {
            return Inventory.Items.ToDictionary(i => i.Slot, i => (long)i.Id);
        }

        if (tab == EStorageTab.Character)
        {
            return _characters.Values.ToDictionary(c => c.Slot, c => c.Id);
        }

        return _equips.Values.ToDictionary(e => e.SlotPosition, e => e.Id);
    }

    private void ApplySlotChanges(EStorageTab tab, IReadOnlyList<SlotChange> changes)
    {
        foreach (var change in changes)
        {
            if (tab == EStorageTab.Resource && Inventory.TryGet((int)change.Key, out var item))
            {
                item.Slot = change.Slot;
            }
            else if (tab == EStorageTab.Character && _characters.TryGetValue(change.Key, out var character))
            {
                character.Slot = change.Slot;
            }
            else if (tab == EStorageTab.Equip && _equips.TryGetValue(change.Key, out var equip))
            {
                equip.SlotPosition = change.Slot;
            }
        }
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
