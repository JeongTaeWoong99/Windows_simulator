using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 드롭 시트 5종(<c>&lt;산업&gt;BasicTable</c>)을 <see cref="DropTableCatalog.LoadAll"/>로 적재해 검증한다.
/// 추첨기 자체의 정확성은 <see cref="WeightedPickerTest"/>가 담당하고, 여기서는
/// <b>(산업, 레벨)마다 6등급을 전부 낼 수 있는지</b>와
/// <b>실제 롤 경로의 실측 분포가 그 레벨 시트 가중치와 일치하는지</b>를 본다.
/// 깨지면: 엑셀에서 등급이 빠졌거나, 레벨 분리·등록 경로가 어긋나 다른 레벨 아이템이 섞이는 것이다.
/// </summary>
public class DropSheetTest
{
    /// <summary>실데이터 카탈로그. 적재 후 읽기만 하므로 테스트끼리 공유해도 안전하다.</summary>
    private static readonly Lazy<DropTableCatalog> Catalog = new(() =>
    {
        GameTableFixture.EnsureLoaded();
        var catalog = new DropTableCatalog();
        catalog.LoadAll();
        return catalog;
    });

    /// <summary>산업의 실제 드롭 시트 행. Row 타입이 산업마다 달라 공통 형태로 정규화한다.</summary>
    private static IEnumerable<(int ItemTID, int Weight, int Level)> SheetRows(IndustryType industry)
    {
        return industry switch
        {
            IndustryType.Farming => GameTable.FarmingBasicTable.All.Select(r => (r.ItemTID, r.Weight, r.IndustryLevel)),
            IndustryType.Fishing => GameTable.FishingBasicTable.All.Select(r => (r.ItemTID, r.Weight, r.IndustryLevel)),
            IndustryType.Logging => GameTable.LoggingBasicTable.All.Select(r => (r.ItemTID, r.Weight, r.IndustryLevel)),
            IndustryType.Mining  => GameTable.MiningBasicTable.All.Select(r => (r.ItemTID, r.Weight, r.IndustryLevel)),
            IndustryType.Hunting => GameTable.HuntingBasicTable.All.Select(r => (r.ItemTID, r.Weight, r.IndustryLevel)),
            _ => throw new ArgumentOutOfRangeException(nameof(industry), industry, "드롭 시트가 없는 산업입니다."),
        };
    }

    /// <summary>산업 5종 × 레벨 5개 = 25개 조합.</summary>
    public static TheoryData<IndustryType, int> AllIndustryLevels()
    {
        var data = new TheoryData<IndustryType, int>();
        foreach (var industry in new[]
                 {
                     IndustryType.Farming, IndustryType.Fishing, IndustryType.Logging, IndustryType.Mining, IndustryType.Hunting,
                 })
        {
            for (var level = 1; level <= 5; level++)
            {
                data.Add(industry, level);
            }
        }

        return data;
    }

    [Fact]
    public void 산업_5종이_레벨_5개씩_테이블_25개로_등록된다()
    {
        // 시트가 레벨로 갈리지 않고 통째로 등록되면 5개가 되고, 이 테스트가 그 회귀를 잡는다.
        Catalog.Value.Count.ShouldBe(25);
    }

    [Theory]
    [MemberData(nameof(AllIndustryLevels))]
    public void 레벨_시트는_6등급_아이템을_전부_낼_수_있다(IndustryType industry, int level)
    {
        _ = Catalog.Value;   // GameTable 적재 보장

        // 가중치 0인 행은 추첨 후보에서 빠지므로 "낼 수 있다"에 들지 않는다.
        var rarities = SheetRows(industry)
            .Where(x => x.Level == level && x.Weight > 0)
            .Select(x => GameTable.ItemTable[x.ItemTID].GlobalRarity)
            .ToHashSet();

        // 한 등급이라도 빠지면 그 (산업, 레벨)에서는 해당 등급이 영원히 드롭되지 않는다.
        // Lv2~5의 하위 혼입(Common·Uncommon)은 자기 레벨 6종과 등급이 겹치므로 집합은 그대로 6종이다.
        rarities.ShouldBe(new[]
        {
            GlobalRarity.Common, GlobalRarity.Uncommon, GlobalRarity.Rare,
            GlobalRarity.Epic, GlobalRarity.Legendary, GlobalRarity.Mythic,
        }, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(AllIndustryLevels))]
    public void 실측_분포가_그_레벨_시트의_가중치와_일치한다(IndustryType industry, int level)
    {
        const int rolls = 1_000_000;

        _ = Catalog.Value;   // GameTable 적재 보장 — SheetRows가 먼저 열거되면 테이블이 null이다

        // 기대 확률은 시트(입력 데이터)에서 계산한다 — 추첨기 내부를 재구현하는 것이 아니다.
        // 하위 혼입으로 같은 ItemTID가 있을 수 있어 가중치를 아이템 단위로 합친다.
        var weights = SheetRows(industry)
            .Where(x => x.Level == level && x.Weight > 0)
            .GroupBy(x => x.ItemTID)
            .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.Weight));
        var total = (double)weights.Values.Sum();

        var gained = Catalog.Value.Get(industry, level).RollMany(rolls, new Random(42));

        // 다른 레벨의 아이템이 나오면 레벨 분리가 어긋난 것이다.
        gained.Keys.ShouldBeSubsetOf(weights.Keys);

        foreach (var (itemTid, weight) in weights)
        {
            var probability = weight / total;
            var expected    = rolls * probability;
            var observed    = gained.GetValueOrDefault(itemTid);

            // 이항분포 5σ 허용 — 시드가 고정이라 결과는 결정적이지만,
            // 가중치를 조정해도 통계적으로 유효한 경계로 남도록 폭을 식으로 둔다.
            var tolerance = 5 * Math.Sqrt(expected * (1 - probability));

            Math.Abs(observed - expected).ShouldBeLessThanOrEqualTo(tolerance,
                $"[{industry} Lv{level}] ItemTID {itemTid}: 실측 {observed}회, 기대 {expected:F0}회 (±{tolerance:F0})");
        }
    }
}
