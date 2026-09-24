using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>의 거래소 경로 — 최악 금액 hold, 예약 결과 검증, 잡힌 금액만 차감, 목록·가격대 중계.
/// 틀리면 단가 상한보다 비싸게 사거나, 예약을 기다리는 사이 hold 밖의 골드를 쓰게 된다.
/// </summary>
public class UserMarketTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int Carp = 10001;

    private readonly FakeAuctionClient _client = new();

    public UserMarketTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) NewUser()
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };
        var user = b.Build(uid: 8);
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    private static Proto.ReserveReply Reserved(params (long Listing, int Qty, long Unit)[] lines)
    {
        var reply = new Proto.ReserveReply { Result = Proto.ReserveResult.Ok, TotalPrice = lines.Sum(l => l.Qty * l.Unit) };
        reply.Allocations.AddRange(lines.Select(l => new Proto.Allocation { ListingId = l.Listing, SellerId = 7, Quantity = l.Qty, UnitPrice = l.Unit }));
        return reply;
    }

    [Fact]
    public void 기다리는_동안_수량_곱하기_단가_상한만큼_잡아_둔다()
    {
        var (user, _) = NewUser();
        _client.ReserveQuantity = _ => new TaskCompletionSource<Proto.ReserveReply>().Task;

        user.TryBuyMarket(Carp, count: 15, maxUnitPrice: 32, Now);

        // 15 × 32 = 480
        user.AvailableGold.ShouldBe(520);
    }

    [Fact]
    public void 최악_금액을_낼_골드가_없으면_예약하지_않는다()
    {
        var (user, b) = NewUser();

        user.TryBuyMarket(Carp, 40, 26, Now);

        Last<S_MarketBuyResponse>(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        _client.RequestsOf<Proto.ReserveQuantityRequest>().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10000, 10)]
    [InlineData(5, 0)]
    public void 수량과_단가가_범위_밖이면_거절한다(int count, long maxUnitPrice)
    {
        var (user, b) = NewUser();

        user.TryBuyMarket(Carp, count, maxUnitPrice, Now);

        Last<S_MarketBuyResponse>(b).Result.ShouldBe(EResultCode.AuctionInvalidRequest);
    }

    [Fact]
    public void 실제로_잡힌_금액만_빠진다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(Reserved((1, 10, 30), (2, 5, 32)));

        user.TryBuyMarket(Carp, 15, 32, Now);

        // 30 × 10 + 32 × 5 = 460
        (user.Gold, user.HeldGold).ShouldBe((540L, 0L));
        b.DB.PostedOf<SettleMarketRepository>().Single().Allocations.Select(a => (a.TradeId, a.Quantity))
            .ShouldBe(new[] { (1L, 10), (2L, 5) });
    }

    [Fact]
    public void 예약_요청에_구매자_수량_상한이_실린다()
    {
        var (user, _) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.NotEnough });

        user.TryBuyMarket(Carp, 15, 32, Now);

        var sent = _client.RequestsOf<Proto.ReserveQuantityRequest>().Single();
        (sent.BuyerId, sent.Tid, sent.Quantity, sent.MaxUnitPrice).ShouldBe((8L, Carp, 15, 32L));
    }

    [Fact]
    public void 수량을_못_채우면_알리고_골드는_그대로다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.NotEnough });

        user.TryBuyMarket(Carp, 15, 32, Now);

        Last<S_MarketBuyResponse>(b).Result.ShouldBe(EResultCode.MarketNotEnough);
        (user.Gold, user.AvailableGold).ShouldBe((1000L, 1000L));
    }

    [Fact]
    public void 단가_상한보다_비싸게_잡힌_예약은_정산하지_않고_놓는다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(Reserved((1, 15, 33)));

        user.TryBuyMarket(Carp, 15, 32, Now);

        user.Gold.ShouldBe(1000);
        b.DB.PostedOf<SettleMarketRepository>().ShouldBeEmpty();
        b.DB.PostedOf<ReleasePurchaseRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 응답이_끊겨도_경매장이_잡았을_수_있으니_놓아_달라고_보낸다()
    {
        var (user, b) = NewUser();

        user.TryBuyMarket(Carp, 15, 32, Now);

        b.DB.PostedOf<ReleasePurchaseRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 예약_뒤_유저가_나갔으면_예약을_놓는다()
    {
        var recording = new TestUserBuilder();
        var leaving   = recording.Build(uid: 9);
        leaving.Destroy();

        leaving.OnMarketReserved(5, Carp, 15, 32, Reserved((1, 15, 30)), Now);

        recording.DB.PostedOf<ReleasePurchaseRepository>().Single().PurchaseId.ShouldBe(5);
        recording.DB.PostedOf<SettleMarketRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 수량을_못_채운_예약은_놓을_것이_없다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.NotEnough });

        user.TryBuyMarket(Carp, 15, 32, Now);

        b.DB.PostedOf<ReleasePurchaseRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 정산이_예외로_끝나면_뺀_대금을_돌려주고_예약을_놓는다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(Reserved((1, 15, 30)));
        user.TryBuyMarket(Carp, 15, 32, Now);
        var settle = b.DB.PostedOf<SettleMarketRepository>().Single();

        settle.OnFailed(new InvalidOperationException("database is locked"));

        user.Gold.ShouldBe(1000);
        b.DB.PostedOf<SaveCurrencyRepository>().ShouldNotBeEmpty();
        b.DB.PostedOf<ReleasePurchaseRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 목록에_TID를_100개_넘게_실으면_거절한다()
    {
        var (user, b) = NewUser();

        user.TryGetMarketItems(0, Enumerable.Range(1, 101).ToList(), Now);

        Last<S_MarketItemsResponse>(b).Result.ShouldBe(EResultCode.AuctionInvalidRequest);
        _client.RequestsOf<Proto.MarketItemsRequest>().ShouldBeEmpty();
    }

    [Fact]
    public void 수량이_요청과_다른_예약은_정산하지_않는다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(Reserved((1, 14, 30)));

        user.TryBuyMarket(Carp, 15, 32, Now);

        b.DB.PostedOf<SettleMarketRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 경매장에_닿지_않으면_hold를_풀고_알린다()
    {
        var (user, b) = NewUser();

        user.TryBuyMarket(Carp, 15, 32, Now);

        Last<S_MarketBuyResponse>(b).Result.ShouldBe(EResultCode.AuctionUnavailable);
        user.AvailableGold.ShouldBe(1000);
    }

    [Fact]
    public void 정산에_실패하면_뺀_골드를_돌려준다()
    {
        var (user, b) = NewUser();
        _client.ReserveQuantity = _ => Task.FromResult(Reserved((1, 15, 30)));
        user.TryBuyMarket(Carp, 15, 32, Now);

        user.OnMarketSettled(Carp, 15, 450, new AuctionMarketSettleResult(false, null, Array.Empty<(long, UserMailRow, int)>()));

        user.Gold.ShouldBe(1000);
        Last<S_MarketBuyResponse>(b).Result.ShouldBe(EResultCode.MarketNotEnough);
    }

    [Fact]
    public void 정산에_성공하면_산_총액을_알린다()
    {
        var (user, b) = NewUser();
        var mail = new UserMailRow { mail_id = 70, template_tid = AuctionMail.PurchasedTemplateTid, items = "[[10001,15]]", received_at = MailDb.ToDb(Now) };

        user.OnMarketSettled(Carp, 15, 460, new AuctionMarketSettleResult(true, mail, Array.Empty<(long, UserMailRow, int)>()));

        var response = Last<S_MarketBuyResponse>(b);
        (response.Result, response.Count, response.TotalPrice).ShouldBe((EResultCode.Ok, 15, 460L));
        user.TryGetMail(70, out _).ShouldBeTrue();
    }

    [Fact]
    public void 다_팔린_매물_수만큼_판매자의_판매_중_건수가_준다()
    {
        var seller = new TestUserBuilder().Build(uid: 7);
        seller.OnAuctionStateLoaded(3);

        seller.OnMarketSold(new UserMailRow { mail_id = 71, template_tid = AuctionMail.SoldTemplateTid, gold = 285, received_at = MailDb.ToDb(Now) }, closedListings: 2);

        seller.ActiveListingCount.ShouldBe(1);
    }

    [Fact]
    public void 목록을_클라_형식으로_옮긴다()
    {
        var (user, b) = NewUser();
        var reply = new Proto.MarketItemsReply();
        reply.Items.Add(new Proto.MarketItem { Tid = Carp, LowestUnitPrice = 30, Available = 15, RecentUnitPrice = 32, YesterdayAvgPrice = 31 });
        _client.MarketItems = _ => Task.FromResult(reply);

        user.TryGetMarketItems(2, new List<int> { Carp }, Now);

        var item = Last<S_MarketItemsResponse>(b).Items!.Single();
        (item.Tid, item.LowestUnitPrice, item.AvailableCount, item.RecentUnitPrice, item.YesterdayAvgPrice).ShouldBe((Carp, 30L, 15L, 32L, 31L));
        _client.RequestsOf<Proto.MarketItemsRequest>().Single().Category.ShouldBe(2);
    }

    [Fact]
    public void 가격대를_클라_형식으로_옮긴다()
    {
        var (user, b) = NewUser();
        var reply = new Proto.PriceLadderReply();
        reply.Levels.Add(new Proto.PriceLevel { UnitPrice = 30, Quantity = 10 });
        _client.PriceLadder = _ => Task.FromResult(reply);

        user.TryGetMarketPrice(Carp, Now);

        Last<S_MarketPriceResponse>(b).Levels!.Select(l => (l.UnitPrice, l.Count)).ShouldBe(new[] { (30L, 10L) });
    }

    [Fact]
    public void 목록_가격대_검색은_빈도_제한을_함께_쓴다()
    {
        var (user, b) = NewUser();
        _client.MarketItems = _ => Task.FromResult(new Proto.MarketItemsReply());
        _client.PriceLadder = _ => Task.FromResult(new Proto.PriceLadderReply());
        _client.Search      = _ => Task.FromResult(new Proto.SearchReply());

        user.TryGetMarketItems(0, null, Now);
        user.TryGetMarketPrice(Carp, Now);
        user.TrySearchAuction(new C_AuctionSearchRequest(), Now);
        user.TryGetMarketItems(0, null, Now);
        user.TryGetMarketPrice(Carp, Now);
        user.TryGetMarketItems(0, null, Now);

        Last<S_MarketItemsResponse>(b).Result.ShouldBe(EResultCode.AuctionTooManyRequests);
    }
}
