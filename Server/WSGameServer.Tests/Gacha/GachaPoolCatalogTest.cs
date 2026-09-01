using GameData;

namespace WSGameServer;

/// <summary>
/// <see cref="GachaPoolCatalog"/> 검증. 추첨의 정확성은 <see cref="WeightedPickerTest"/>가 담당하고,
/// 여기서는 <b>Row → GachaEntry 정규화</b>와 <b>GachaId별 풀 분리</b>를 본다.
/// </summary>
public class GachaPoolCatalogTest
{
    /// <summary>실제 GachaItemTable 시트와 같은 모양의 테스트용 Row.</summary>
    private sealed record ItemRow(int GachaItemTID, int GachaId, int ItemTID, int Count, int Weight);

    /// <summary>실제 GachaCharacterTable 시트와 같은 모양의 테스트용 Row.</summary>
    private sealed record CharacterRow(int GachaCharacterTID, int GachaId, int CharacterTID, int Count, int Weight);

    private static readonly ItemRow[] Items =
    {
        new(1001, 1, 100001, 1, 700),
        new(1002, 1, 100002, 3, 300),
        new(2001, 2, 100003, 1, 100),
    };

    private static readonly CharacterRow[] Characters =
    {
        new(3001, 3, 1002, 1, 100),
    };

    private static IEnumerable<GachaEntry> ItemEntries(IEnumerable<ItemRow> rows)
        => GachaPoolCatalog.ToEntries(rows, GachaRewardType.Item,
                                      r => r.GachaId, r => r.ItemTID, r => r.Count, r => r.Weight);

    private static IEnumerable<GachaEntry> CharacterEntries(IEnumerable<CharacterRow> rows)
        => GachaPoolCatalog.ToEntries(rows, GachaRewardType.Character,
                                      r => r.GachaId, r => r.CharacterTID, r => r.Count, r => r.Weight);

    private static GachaPoolCatalog BuildCatalog()
    {
        // Singleton이지만 new()가 가능하므로 테스트마다 독립 인스턴스를 쓴다.
        // Instance를 공유하면 테스트 순서에 따라 등록 상태가 새어 나간다.
        var catalog = new GachaPoolCatalog();
        catalog.Load(ItemEntries(Items).Concat(CharacterEntries(Characters)));
        return catalog;
    }

    /// <summary>지정한 값만 내놓는 난수원.</summary>
    private static Random FixedRoll(int value)
    {
        var random = new Mock<Random>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(value);
        return random.Object;
    }

    [Fact]
    public void GachaId별로_풀이_나뉜다()
    {
        var catalog = BuildCatalog();

        catalog.Count.ShouldBe(3);

        catalog.TryGet(1, out var pool1).ShouldBeTrue();
        catalog.TryGet(2, out var pool2).ShouldBeTrue();

        // 풀 1에는 두 항목(가중치 합 1000), 풀 2에는 한 항목만 들어간다.
        pool1.TotalWeight.ShouldBe(1000);
        pool2.TotalWeight.ShouldBe(100);
        pool2.Pick(FixedRoll(0)).RewardTID.ShouldBe(100003);
    }

    [Fact]
    public void 추첨_결과는_RewardTID와_Count를_그대로_담는다()
    {
        var catalog = BuildCatalog();
        catalog.TryGet(1, out var pool);

        // GachaItemTID는 시트의 키일 뿐 추첨 결과와 무관하다 — 결과는 (RewardTID, Count)다.
        pool.Pick(FixedRoll(0)).ShouldBe(new GachaEntry(1, GachaRewardType.Item, 100001, 1, 700));
        pool.Pick(FixedRoll(699)).RewardTID.ShouldBe(100001);

        var rare = pool.Pick(FixedRoll(700));
        rare.RewardTID.ShouldBe(100002);
        rare.Count.ShouldBe(3);
    }

    [Fact]
    public void 보상_종류는_출처_시트가_정한다()
    {
        var catalog = BuildCatalog();

        // 같은 숫자 대역을 써도 종류가 섞이지 않아야 한다 — 종류는 값이 아니라 어느 시트에서 왔는지가 정한다.
        catalog.TryGet(1, out var itemPool);
        itemPool.Pick(FixedRoll(0)).RewardType.ShouldBe(GachaRewardType.Item);

        catalog.TryGet(3, out var characterPool);
        var character = characterPool.Pick(FixedRoll(0));
        character.RewardType.ShouldBe(GachaRewardType.Character);
        character.RewardTID.ShouldBe(1002);
    }

    [Fact]
    public void 같은_GachaId면_아이템과_캐릭터가_한_풀에_섞인다()
    {
        // 두 시트가 같은 풀 번호를 쓰면 한 추첨기에 함께 들어간다 — 혼합 풀을 만들 수 있는 구조다.
        var catalog = new GachaPoolCatalog();
        catalog.Load(ItemEntries(new[] { new ItemRow(1001, 7, 100001, 1, 100) })
            .Concat(CharacterEntries(new[] { new CharacterRow(3001, 7, 1002, 1, 100) })));

        catalog.Count.ShouldBe(1);
        catalog.TryGet(7, out var pool).ShouldBeTrue();
        pool.TotalWeight.ShouldBe(200);
        pool.Pick(FixedRoll(0)).RewardType.ShouldBe(GachaRewardType.Item);
        pool.Pick(FixedRoll(100)).RewardType.ShouldBe(GachaRewardType.Character);
    }

    [Fact]
    public void 없는_풀은_TryGet이_false다()
    {
        var catalog = BuildCatalog();

        catalog.TryGet(999, out _).ShouldBeFalse();
    }

    [Fact]
    public void 다시_로드하면_기존_풀을_전부_교체한다()
    {
        var catalog = BuildCatalog();

        // 풀 2가 사라진 데이터로 재로드 — 남아 있으면 이전 확률표가 유령으로 산다.
        catalog.Load(ItemEntries(new[] { new ItemRow(1001, 1, 100001, 1, 100) }));

        catalog.Count.ShouldBe(1);
        catalog.TryGet(2, out _).ShouldBeFalse();
    }
}
