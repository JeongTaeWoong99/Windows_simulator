namespace WSGameServer;

/// <summary>서버 내부 정렬 기준. 프로토콜 EStorageSortKey와 값이 같다.</summary>
public enum StorageSortKey
{
    Rarity = 0,
    Name   = 1,
    Count  = 2,
}

// TieBreak은 탭 규칙을 사전식 오름차순으로 비교할 값 목록이다. 마지막은 키라 동점이 남지 않는다.
public sealed record StorageSortEntry(long Key, bool IsAway, string Name, long Count, long[] TieBreak);

/// <summary>인벤토리 정렬 순서. 클라 InventorySlotSource.CompareByRule과 같은 규칙이어야 한다.</summary>
public static class StorageSort
{
    // 등급은 높은 것이 먼저라 부호를 뒤집어 넣는다.
    public static long[] ResourceTieBreak(int rarity, int itemType, int tid)
        => new long[] { -rarity, itemType, tid };

    public static long[] CharacterTieBreak(int rarity, int tid, long id)
        => new long[] { -rarity, tid, id };

    public static long[] EquipTieBreak(int rarity, int kind, int industry, int tid, long id)
        => new long[] { -rarity, kind, industry, tid, id };

    public static List<long> Order(IReadOnlyList<StorageSortEntry> entries, StorageSortKey key, bool ascending)
    {
        var sorted = entries.ToList();
        sorted.Sort((a, b) => Compare(a, b, key, ascending));
        return sorted.Select(e => e.Key).ToList();
    }

    // 나가 있는 것은 덩어리 가르기라 방향을 타지 않는다 — 뒤집으면 손댈 수 없는 것이 맨 위로 온다.
    private static int Compare(StorageSortEntry a, StorageSortEntry b, StorageSortKey key, bool ascending)
    {
        if (a.IsAway != b.IsAway)
        {
            return a.IsAway ? 1 : -1;
        }

        var compared = CompareByKey(a, b, key);
        return ascending ? -compared : compared;
    }

    private static int CompareByKey(StorageSortEntry a, StorageSortEntry b, StorageSortKey key)
    {
        var byKey = 0;
        if (key == StorageSortKey.Name)
        {
            byKey = string.CompareOrdinal(a.Name, b.Name);
        }
        else if (key == StorageSortKey.Count)
        {
            byKey = b.Count.CompareTo(a.Count);
        }

        if (byKey != 0)
        {
            return byKey;
        }

        return CompareTieBreak(a.TieBreak, b.TieBreak);
    }

    private static int CompareTieBreak(long[] a, long[] b)
    {
        for (var i = 0; i < a.Length && i < b.Length; i++)
        {
            var c = a[i].CompareTo(b[i]);
            if (c != 0)
            {
                return c;
            }
        }

        return a.Length.CompareTo(b.Length);
    }
}
