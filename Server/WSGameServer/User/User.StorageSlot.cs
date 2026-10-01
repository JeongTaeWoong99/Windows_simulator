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
