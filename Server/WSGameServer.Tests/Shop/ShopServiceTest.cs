using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 즉시 판매의 조립부 검증 — 대금은 <c>ItemTable.BasePrice</c>에서 나오고,
/// <b>인벤토리 차감과 골드 지급은 함께 성공하거나 함께 실패한다.</b>
///
/// <para>
/// 골드가 처음 생기는 경로라 여기가 새면 곧바로 경제가 무너진다:
/// 차감만 되면 아이템이 증발하고, 지급만 되면 골드가 공짜로 나온다.
/// </para>
/// </summary>
public class ShopServiceTest
{
    /// <summary>붕어 — 낚시 Lv1 Common. BasePrice 10.</summary>
    private const int CarpTid = 10001;

    /// <summary>전설의 잉어 — 낚시 Lv1 Mythic. BasePrice 1000.</summary>
    private const int LegendTid = 10006;

    public ShopServiceTest() => GameTableFixture.EnsureLoaded();

    /// <summary>아이템을 미리 쥐여 준 유저를 만든다. 지급으로 쌓인 DB 작업은 지운다 — 판매만 세기 위해.</summary>
    private static (User User, TestUserBuilder B) UserWith(params (int Tid, int Count)[] items)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        foreach (var (tid, count) in items)
        {
            user.GainItem(tid, count);
        }

        b.DB.Posted.Clear();
        return (user, b);
    }

    private static List<ItemInfo> Request(params (int Tid, int Count)[] items)
        => items.Select(i => new ItemInfo { ItemId = i.Tid, Count = i.Count }).ToList();

    [Fact]
    public void 판매하면_BasePrice_곱하기_수량만큼_골드가_오른다()
    {
        var (user, b) = UserWith((CarpTid, 5));

        ShopService.Instance.Sell(user, Request((CarpTid, 5)));

        // 붕어 10골드 × 5개 = 50 (즉시 판매가는 BasePrice의 100%)
        user.GetCurrency(CurrencyType.Gold).ShouldBe(50);
        b.Channel.SentOf<S_ItemSellResponse>().Single().GainedGold.ShouldBe(50);
    }

    [Fact]
    public void 여러_종류를_한_번에_팔면_합계가_지급된다()
    {
        var (user, b) = UserWith((CarpTid, 5), (LegendTid, 2));

        ShopService.Instance.Sell(user, Request((CarpTid, 5), (LegendTid, 2)));

        // 10 × 5 + 1000 × 2 = 2050
        user.GetCurrency(CurrencyType.Gold).ShouldBe(2050);
    }

    [Fact]
    public void 갱신된_잔액이_재화_패킷으로_내려간다()
    {
        var (user, b) = UserWith((CarpTid, 5));

        ShopService.Instance.Sell(user, Request((CarpTid, 5)));

        var currency = b.Channel.SentOf<S_CurrencyResponse>().Last().Currencies!.Single();
        currency.CurrencyType.ShouldBe((byte)CurrencyType.Gold);
        currency.Amount.ShouldBe(50);
    }

    [Fact]
    public void 전부_팔면_변경은_Remove로_내려간다()
    {
        var (user, b) = UserWith((CarpTid, 5));

        ShopService.Instance.Sell(user, Request((CarpTid, 5)));

        var change = b.Channel.SentOf<S_ItemSellResponse>().Single().ItemChangeInfos!.Single();
        change.Kind.ShouldBe(EItemChangeKind.Remove);
        change.Count.ShouldBe(0);
    }

    [Fact]
    public void 보유량을_넘겨_팔면_거절되고_인벤토리와_골드가_그대로다()
    {
        var (user, b) = UserWith((CarpTid, 3));

        ShopService.Instance.Sell(user, Request((CarpTid, 4)));

        b.Channel.SentOf<S_ItemSellResponse>().Single().Result.ShouldBe(EResultCode.NotEnoughItem);
        user.GetCurrency(CurrencyType.Gold).ShouldBe(0);

        user.SendInventory();
        b.Channel.SentOf<S_InventoryResponse>().Single().Items!.Single().Count.ShouldBe(3);
    }

    [Fact]
    public void 한_종류라도_모자라면_나머지도_팔리지_않는다()
    {
        var (user, b) = UserWith((CarpTid, 5), (LegendTid, 1));

        // 붕어는 충분하지만 전설의 잉어가 1개뿐이다 — 둘 다 거절돼야 한다.
        ShopService.Instance.Sell(user, Request((CarpTid, 5), (LegendTid, 2)));

        b.Channel.SentOf<S_ItemSellResponse>().Single().Result.ShouldBe(EResultCode.NotEnoughItem);
        user.GetCurrency(CurrencyType.Gold).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 빈_목록과_존재하지_않는_아이템은_거절된다()
    {
        var (user, b) = UserWith((CarpTid, 3));

        ShopService.Instance.Sell(user, new List<ItemInfo>());
        ShopService.Instance.Sell(user, Request((999999, 1)));
        ShopService.Instance.Sell(user, Request((CarpTid, 0)));

        b.Channel.SentOf<S_ItemSellResponse>()
            .ShouldAllBe(r => r.Result == EResultCode.InvalidSellRequest);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 판매는_DB작업을_하나만_예약한다()
    {
        var (user, b) = UserWith((CarpTid, 5));

        ShopService.Instance.Sell(user, Request((CarpTid, 5)));

        // 인벤 차감과 골드 지급이 갈라지면 2가 된다 — 차감만 커밋되고 지급이 실패하는 창이 열린다.
        b.DB.Posted.Count.ShouldBe(1);
        b.DB.PostedOf<SellItemsRepository>().ShouldHaveSingleItem();
    }
}
