using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 골드 상점 구매(#78) — 가격은 <c>ShopTable.PriceGold</c>에서 나오고,
/// <b>골드 차감과 아이템 지급은 함께 성공하거나 함께 실패한다.</b>
/// </summary>
public class ShopBuyTest
{
    /// <summary>상점 1번 = 캐릭터 뽑기권 A · 1,000골드.</summary>
    private const int TicketAShopTid = 1;

    private const int TicketATid = 100071;

    public ShopBuyTest() => GameTableFixture.EnsureLoaded();

    /// <summary>골드를 쥐여 준 유저. 지급으로 쌓인 패킷·DB 작업은 지운다 — 구매만 세기 위해.</summary>
    private static (User User, TestUserBuilder B) UserWithGold(long gold)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        if (gold > 0)
        {
            user.GainGold(gold);
        }

        b.DB.Posted.Clear();
        b.Channel.Sent.Clear();
        return (user, b);
    }

    [Fact]
    public void 사면_가격_곱하기_개수만큼_골드가_빠지고_아이템이_들어온다()
    {
        var (user, b) = UserWithGold(5000);

        ShopService.Instance.Buy(user, TicketAShopTid, 3);

        // 1,000 × 3 = 3,000
        user.Gold.ShouldBe(2000);
        user.GetItemCount(TicketATid).ShouldBe(3);

        var res = b.Channel.SentOf<S_ShopBuyResponse>().Single();
        res.Result.ShouldBe(EResultCode.Ok);
        res.ShopTID.ShouldBe(TicketAShopTid);
        res.ItemChangeInfos!.Single().Count.ShouldBe(3);
        b.Channel.SentOf<S_CurrencyResponse>().Last().Gold.ShouldBe(2000);
    }

    [Fact]
    public void 골드가_모자라면_거절되고_아무것도_바뀌지_않는다()
    {
        var (user, b) = UserWithGold(2999);

        ShopService.Instance.Buy(user, TicketAShopTid, 3);

        b.Channel.SentOf<S_ShopBuyResponse>().Single().Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(2999);
        user.GetItemCount(TicketATid).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 없는_품목과_범위_밖_개수는_거절된다()
    {
        var (user, b) = UserWithGold(1_000_000);

        ShopService.Instance.Buy(user, 999, 1);
        ShopService.Instance.Buy(user, TicketAShopTid, 0);
        ShopService.Instance.Buy(user, TicketAShopTid, GachaService.MaxOpenCount + 1);

        b.Channel.SentOf<S_ShopBuyResponse>().ShouldAllBe(r => r.Result == EResultCode.InvalidShopRequest);
        user.Gold.ShouldBe(1_000_000);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 자원_칸이_가득하면_거절되고_골드가_그대로다()
    {
        var (user, b) = UserWithGold(5000);

        // 서로 다른 자원으로 인벤토리 칸을 전부 채운다 — 뽑기권이 들어갈 새 칸이 없다.
        var fillers = GameTable.ItemTable.All.Where(r => r.ItemTID != TicketATid).Take((int)Constants.StorageCapacity);
        foreach (var row in fillers)
        {
            user.GainItem(row.ItemTID, 1);
        }

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();

        ShopService.Instance.Buy(user, TicketAShopTid, 1);

        b.Channel.SentOf<S_ShopBuyResponse>().Single().Result.ShouldBe(EResultCode.StorageFull);
        user.Gold.ShouldBe(5000);
        user.GetItemCount(TicketATid).ShouldBe(0);
    }
}
