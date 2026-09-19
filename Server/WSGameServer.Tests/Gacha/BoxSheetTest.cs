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
    public void 상자는_세_등급이고_최대_99개다()
    {
        Boxes.Select(r => r.GlobalRarity).OrderBy(r => r)
            .ShouldBe(new[] { GlobalRarity.Common, GlobalRarity.Uncommon, GlobalRarity.Rare });
        Boxes.ShouldAllBe(r => r.ItemType == ItemType.Special && r.MaxStack == 99);
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
    public void 공통_보상은_상자만_준다()
    {
        var boxTids = Boxes.Select(r => r.ItemTID).ToHashSet();

        GameTable.CommonRewardTable.All.ShouldNotBeEmpty();
        GameTable.CommonRewardTable.All.ShouldAllBe(r => boxTids.Contains(r.ItemTID));
    }
}
