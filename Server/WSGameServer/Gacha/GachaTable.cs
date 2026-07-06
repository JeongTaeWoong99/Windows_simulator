using MikaProtocol;

namespace WSGameServer.Gacha;

/// <summary>
/// 가챠 풀(확률표)을 코드 상수로 정의하고, 가중치 기반 추첨을 수행하는 정적 설정.
/// MVP 단계라 Data 테이블 없이 여기에 풀을 하드코딩한다(후속 확장 시 Data로 이관).
/// </summary>
public static class GachaTable
{
    // 풀 안의 항목 1개: 이 아이템을 Weight 비중으로 뽑고, 뽑히면 Count개 지급한다.
    public sealed record GachaEntry(int ItemId, int Count, EItemRarity Rarity, int Weight);

    // GachaId -> 풀(항목 목록). MVP는 기본 풀(GachaId=1) 하나만 정의한다.
    private static readonly Dictionary<int, IReadOnlyList<GachaEntry>> Pools = new()
    {
        [1] = new List<GachaEntry>
        {
            // Weight 합 = 1000 (Common 79% / Rare 15% / Epic 5% / Legendary 1%)
            new GachaEntry(ItemId: 1001, Count: 1, Rarity: EItemRarity.Common,    Weight: 790),
            new GachaEntry(ItemId: 2001, Count: 1, Rarity: EItemRarity.Rare,      Weight: 150),
            new GachaEntry(ItemId: 3001, Count: 1, Rarity: EItemRarity.Epic,      Weight:  50),
            new GachaEntry(ItemId: 4001, Count: 1, Rarity: EItemRarity.Legendary, Weight:  10),
        },
    };

    // 풀 존재 여부 검증. 없으면 false.
    public static bool TryGetPool(int gachaId, out IReadOnlyList<GachaEntry> pool)
        => Pools.TryGetValue(gachaId, out pool!);

    /// <summary>
    /// 지정한 풀에서 drawCount회 독립 추첨한다(누적 가중치 방식). 뽑힌 순서대로 반환.
    /// 호출 전에 <see cref="TryGetPool"/>로 유효성을 확인했다고 가정한다.
    /// </summary>
    public static List<GachaEntry> Draw(int gachaId, int drawCount)
    {
        var pool = Pools[gachaId];
        int totalWeight = 0;
        foreach (var entry in pool)
            totalWeight += entry.Weight;

        var results = new List<GachaEntry>(drawCount);
        for (int i = 0; i < drawCount; i++)
            results.Add(PickOne(pool, totalWeight));

        return results;
    }

    // 누적 가중치 구간에서 난수 하나로 항목 1개 선택.
    private static GachaEntry PickOne(IReadOnlyList<GachaEntry> pool, int totalWeight)
    {
        // Random.Shared는 스레드 안전하다(.NET 6+).
        int roll = Random.Shared.Next(totalWeight);
        int cumulative = 0;
        foreach (var entry in pool)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry;
        }

        // 가중치 합이 맞으면 도달하지 않지만, 방어적으로 마지막 항목 반환.
        return pool[^1];
    }
}
