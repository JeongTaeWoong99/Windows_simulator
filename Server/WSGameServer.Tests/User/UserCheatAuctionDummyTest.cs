using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 경매 더미 매물 치트(<c>SeedAuctionListings</c>, T-168). 치트를 쓴 유저의 골드·판매 중 건수가 움직이면
/// 실제 등록 경로를 탄 것이고, 지울 때 경매장 매물을 남기면 검색에 주인 없는 매물이 남는다.
/// </summary>
public class UserCheatAuctionDummyTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int Carp = 10001;

    private readonly FakeAuctionClient _client = new();

    public UserCheatAuctionDummyTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) Admin(bool withAuction = true)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        if (withAuction)
        {
            b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };
        }

        var user = b.Build();
        user.AdminLevel = 1;
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static C_CheatRequest Seed(long count, long itemTid = 0)
        => new() { Command = ECheatCommand.SeedAuctionListings, Arg1 = count, Arg2 = itemTid };

    private static EResultCode LastResult(TestUserBuilder b) => b.Channel.SentOf<S_CheatResponse>().Last().Result;

    [Theory]
    [InlineData(-1)]
    [InlineData(AuctionDummy.MaxCount + 1)]
    public void 개수가_범위_밖이면_거절한다(long count)
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Seed(count), Now);

        LastResult(b).ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 없는_ItemTID면_거절한다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Seed(10, 99_999_999), Now);

        LastResult(b).ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 경매장이_없으면_거절한다()
    {
        var (user, b) = Admin(withAuction: false);

        user.ExecuteCheat(Seed(10), Now);

        LastResult(b).ShouldBe(EResultCode.AuctionUnavailable);
    }

    [Fact]
    public void 지정한_자원으로_개수만큼_더미_매물을_저장_요청한다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Seed(25, Carp), Now);

        var listings = b.DB.PostedOf<SeedDummyAuctionRepository>().ShouldHaveSingleItem().Listings;
        listings.Count.ShouldBe(25);
        listings.ShouldAllBe(l => l.Item.Tid == Carp);
    }

    [Fact]
    public void ItemTID가_0이면_여러_자원을_섞는다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Seed(25), Now);

        b.DB.PostedOf<SeedDummyAuctionRepository>().Single().Listings.Select(l => l.Item.Tid).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void 치트를_쓴_유저의_골드와_판매_중_건수는_그대로다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Seed(25, Carp), Now);

        user.Gold.ShouldBe(1000);
        user.ActiveListingCount.ShouldBe(0);
    }

    [Fact]
    public void 개수_0이면_원장을_지운_뒤_경매장의_더미_매물을_전부_취소한다()
    {
        var (user, b) = Admin();
        _client.SellerListings = _ => Task.FromResult(new Proto.SellerListingsReply
        {
            Listings = { new Proto.ListingView { ListingId = 11 }, new Proto.ListingView { ListingId = 12 } },
        });
        _client.Cancel = _ => Task.FromResult(new Proto.CancelReply { Result = Proto.CancelResult.Ok });

        user.ExecuteCheat(Seed(0), Now);
        b.DB.PostedOf<PurgeDummyAuctionRepository>().ShouldHaveSingleItem().Apply();

        _client.RequestsOf<Proto.SellerListingsRequest>().ShouldHaveSingleItem().SellerId.ShouldBe(AuctionDummy.SellerId);
        var cancels = _client.RequestsOf<Proto.CancelRequest>();
        cancels.Select(c => c.ListingId).ShouldBe(new long[] { 11, 12 });
        cancels.ShouldAllBe(c => c.SellerId == AuctionDummy.SellerId);
    }

    [Fact]
    public void 구매_중이라_취소하지_못한_매물은_남은_수로_알린다()
    {
        var (user, b) = Admin();
        _client.SellerListings = _ => Task.FromResult(new Proto.SellerListingsReply
        {
            Listings = { new Proto.ListingView { ListingId = 11 }, new Proto.ListingView { ListingId = 12 } },
        });
        _client.Cancel = r => Task.FromResult(new Proto.CancelReply
        {
            Result = r.ListingId == 11 ? Proto.CancelResult.Ok : Proto.CancelResult.InProgress,
        });

        user.ExecuteCheat(Seed(0), Now);
        b.DB.PostedOf<PurgeDummyAuctionRepository>().Single().Apply();

        var last = b.Channel.SentOf<S_CheatResponse>().Last();
        last.Result.ShouldBe(EResultCode.Ok);
        last.Message.ShouldContain("남음 1");
    }
}
