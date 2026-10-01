namespace WSGameServer;

/// <summary>칸 하나의 새 자리. Key는 자원이면 ItemId, 개체면 개체 PK다.</summary>
public readonly record struct SlotChange(long Key, int Slot);

/// <summary>인벤토리·창고 격자의 칸 계산. 상태를 갖지 않는다 — 칸 번호는 개체가 든다.</summary>
public static class StorageSlots
{
    public static int FirstFree(IEnumerable<int> used)
    {
        var taken = new HashSet<int>(used);
        var slot  = 0;
        while (taken.Contains(slot))
        {
            slot++;
        }

        return slot;
    }

    // occupied는 칸 → 키. 목적지가 비었으면 옮기고, 차 있으면 교환한다. 출발 칸이 비면 바꿀 것이 없다.
    public static List<SlotChange> Move(IReadOnlyDictionary<int, long> occupied, int from, int to)
    {
        if (!occupied.TryGetValue(from, out var moving))
        {
            return new List<SlotChange>();
        }

        var changes = new List<SlotChange> { new(moving, to) };
        if (occupied.TryGetValue(to, out var other))
        {
            changes.Add(new SlotChange(other, from));
        }

        return changes;
    }

    public static List<SlotChange> Renumber(IReadOnlyList<long> keysInOrder)
    {
        var changes = new List<SlotChange>(keysInOrder.Count);
        for (var i = 0; i < keysInOrder.Count; i++)
        {
            changes.Add(new SlotChange(keysInOrder[i], i));
        }

        return changes;
    }
}
