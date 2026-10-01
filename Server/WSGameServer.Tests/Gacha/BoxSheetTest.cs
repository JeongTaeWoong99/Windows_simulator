using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 상자 데이터 검증 — <c>Item.xlsx</c>의 상자(<c>OpenGachaId</c>)와 <c>Gacha.xlsx</c>의 상자 풀이 맞물리는지 본다.
/// 깨지면: 열 수 없는 상자가 생기거나, 상자에서 상자가 나와 무한히 까지거나, 상자 풀을 골드로 사게 된다.
/// </summary>
public class BoxSheetTest
{
    public BoxSheetTest() => GameTableFixture.EnsureLoaded();

    private static IEnumerable<ItemTableRow> Boxes => GameTable.ItemTable.All.Where(r => r.OpenGachaId != 0);

    [Fact]
    public void 상자는_산업_레벨마다_세_등급이고_최대_50개다()
    {
        // 나무·은·황금 × Lv1~5 = 15종.
        Boxes.Count().ShouldBe(15);
        Boxes.GroupBy(r => r.GlobalRarity).Select(g => (g.Key, g.Count())).OrderBy(g => g.Key)
            .ShouldBe(new[] { (GlobalRarity.Common, 5), (GlobalRarity.Uncommon, 5), (GlobalRarity.Rare, 5) });
        Boxes.ShouldAllBe(r => r.ItemType == ItemType.Special && r.MaxStack == 50);
    }

    [Fact]
    public void 상자_풀은_비용_재화가_None이다()
    {
        // None이면 GachaService.Draw가 거절한다 — 상자를 거치지 않고 내용물을 사는 길을 막는다.
        foreach (var box in Boxes)
        {
            GameTable.GachaInfoTable.TryGet(box.OpenGachaId, out var info).ShouldBeTrue();
            info.CostCurrency.ShouldBe(CurrencyType.None);
        }
    }

    [Fact]
    public void 상자_풀에서_상자가_나오지_않는다()
    {
        var boxTids  = Boxes.Select(r => r.ItemTID).ToHashSet();
        var boxPools = Boxes.Select(r => r.OpenGachaId).ToHashSet();

        var looped = GameTable.GachaItemTable.All
            .Where(r => boxPools.Contains(r.GachaId) && boxTids.Contains(r.ItemTID))
            .Select(r => r.GachaItemTID);

        looped.ShouldBeEmpty();
    }

    [Fact]
    public void 상자_풀에서_캐릭터가_나오지_않는다()
    {
        var boxPools = Boxes.Select(r => r.OpenGachaId).ToHashSet();

        GameTable.GachaCharacterTable.All.Where(r => boxPools.Contains(r.GachaId)).ShouldBeEmpty();
    }

    [Fact]
    public void 산업_레벨마다_상자가_세_종류씩_나온다()
    {
        // 상자는 산업 드롭 테이블의 한 줄이다 — 빠진 레벨이 있으면 그 레벨에서는 상자를 얻을 길이 없다.
        var boxTids = Boxes.Select(r => r.ItemTID).ToHashSet();
        var drops = GameTable.FarmingBasicTable.All.Select(r => (r.IndustryLevel, r.ItemTID))
            .Concat(GameTable.FishingBasicTable.All.Select(r => (r.IndustryLevel, r.ItemTID)))
            .Concat(GameTable.MiningBasicTable.All.Select(r => (r.IndustryLevel, r.ItemTID)))
            .Concat(GameTable.LoggingBasicTable.All.Select(r => (r.IndustryLevel, r.ItemTID)))
            .Concat(GameTable.HuntingBasicTable.All.Select(r => (r.IndustryLevel, r.ItemTID)))
            .Where(d => boxTids.Contains(d.ItemTID))
            .ToList();

        // 5개 산업 × 5레벨 × 3종. 레벨마다 같은 상자 3종을 5개 산업이 나눠 쓴다.
        drops.Count.ShouldBe(75);
        drops.GroupBy(d => d.IndustryLevel).ShouldAllBe(g => g.Select(d => d.ItemTID).Distinct().Count() == 3);
        drops.Select(d => d.ItemTID).Distinct().Count().ShouldBe(15);
    }
}
