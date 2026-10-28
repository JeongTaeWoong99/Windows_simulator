using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 캐릭터 뽑기권 시트 검증(#78). 뽑기권은 <b>최고 등급이 고정</b>되고, <b>상점 가격이 기대 판매가보다 비싸다.</b>
/// 깨지면: 엑셀에서 뽑기권 풀에 등급을 넘는 캐릭터가 들어갔거나, 가격을 기대 판매가 아래로 내려 사서 팔면 골드가 남는 것이다.
/// </summary>
public class CharacterTicketSheetTest
{
    public CharacterTicketSheetTest() => GameTableFixture.EnsureLoaded();

    // 상점에서 파는 아이템 중 캐릭터 풀을 여는 것 = 캐릭터 뽑기권
    private static List<(ShopTableRow Shop, ItemTableRow Item, List<GachaCharacterTableRow> Pool)> Tickets()
        => GameTable.ShopTable.All
            .Select(s => (Shop: s, Item: GameTable.ItemTable[s.ItemTID]))
            .Select(t => (t.Shop, t.Item, Pool: GameTable.GachaCharacterTable.All.Where(r => r.GachaId == t.Item.OpenGachaId).ToList()))
            .Where(t => t.Pool.Count > 0)
            .ToList();

    [Fact]
    public void 뽑기권은_네_장이고_최고_등급이_희귀_영웅_전설_신화다()
    {
        Tickets().Select(t => t.Item.GlobalRarity)
            .ShouldBe(new[] { GlobalRarity.Rare, GlobalRarity.Epic, GlobalRarity.Legendary, GlobalRarity.Mythic });
    }

    [Fact]
    public void 뽑기권_풀은_뽑기권_등급을_넘는_캐릭터를_담지_않고_그_등급은_담는다()
    {
        foreach (var (_, item, pool) in Tickets())
        {
            var rarities = pool.Select(r => GameTable.CharacterTable[r.CharacterTID].GlobalRarity).ToList();
            rarities.Max().ShouldBe(item.GlobalRarity, $"뽑기권 {item.ItemTID}");
        }
    }

    [Fact]
    public void 뽑기권_풀은_상자_전용이라_골드로_직접_뽑을_수_없다()
    {
        foreach (var (_, item, _) in Tickets())
        {
            GameTable.GachaInfoTable[item.OpenGachaId].CostCurrency.ShouldBe(CurrencyType.None);
        }
    }

    [Fact]
    public void 캐릭터를_주는_풀은_전부_골드로_직접_뽑을_수_없다()
    {
        // 캐릭터는 상점 뽑기권으로만 얻는다(T-184) — 골드 직접 풀이 남으면 뽑기권 가격이 우회된다.
        foreach (var gachaId in GameTable.GachaCharacterTable.All.Select(r => r.GachaId).Distinct())
        {
            GameTable.GachaInfoTable[gachaId].CostCurrency.ShouldBe(CurrencyType.None, $"캐릭터 풀 {gachaId}");
        }
    }

    [Fact]
    public void 상점_가격은_기대_판매가와_되팔기_값보다_비싸다()
    {
        foreach (var (shop, item, pool) in Tickets())
        {
            var weight = pool.Sum(r => (long)r.Weight);
            var expectedSell = pool.Sum(r => (long)r.Weight * GameTable.CharacterTable[r.CharacterTID].BasePrice) / weight;

            shop.PriceGold.ShouldBeGreaterThan(expectedSell, $"뽑기권 {item.ItemTID}");
            shop.PriceGold.ShouldBeGreaterThan(item.BasePrice, $"뽑기권 {item.ItemTID}");
        }
    }
}
