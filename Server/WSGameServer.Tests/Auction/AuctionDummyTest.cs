using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 더미 매물(치트 T-168) — 만드는 모양과 원장 SQL. 판매자가 실제 유저면 팔릴 때 대금이 그 유저에게 가고,
/// 지울 때 실제 유저의 거래·우편이 같이 지워지면 원장이 깨진다.
/// </summary>
public class AuctionDummyTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const long RealSeller = 7;

    private static readonly ItemTableRow Carp = new() { ItemTID = 10001, Name = "붕어", ItemType = ItemType.Excavation, GlobalRarity = GlobalRarity.Common, BasePrice = 10 };
    private static readonly ItemTableRow Wood = new() { ItemTID = 40001, Name = "잡목", ItemType = ItemType.Logging, GlobalRarity = GlobalRarity.Uncommon, BasePrice = 0 };

    private readonly SqliteFixture _db = new();

    public AuctionDummyTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    // ─────────────────────── 만들기 ───────────────────────

    [Fact]
    public void 요청한_개수만큼_만들고_판매자는_더미다()
    {
        var listings = AuctionDummy.Build(new[] { Carp }, 30, Now);

        listings.Count.ShouldBe(30);
        listings.ShouldAllBe(l => l.Item.SellerName == AuctionDummy.SellerName && l.Item.Kind == EAuctionKind.Item);
    }

    [Fact]
    public void 종류를_여럿_주면_돌아가며_쓴다()
    {
        var listings = AuctionDummy.Build(new[] { Carp, Wood }, 4, Now);

        listings.Select(l => l.Item.Tid).ShouldBe(new[] { 10001, 40001, 10001, 40001 });
    }

    [Fact]
    public void 단가는_즉시_판매가_아래로_내려가지_않고_흩어진다()
    {
        var listings = AuctionDummy.Build(new[] { Carp }, 50, Now);

        listings.ShouldAllBe(l => l.UnitPrice >= 10);
        listings.Select(l => l.UnitPrice).Distinct().Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void 즉시_판매가가_0인_자원도_단가는_1_이상이다()
    {
        AuctionDummy.Build(new[] { Wood }, 20, Now).ShouldAllBe(l => l.UnitPrice >= 1);
    }

    [Fact]
    public void 만료는_지금보다_뒤이고_등록_기간_안에서_흩어진다()
    {
        var listings = AuctionDummy.Build(new[] { Carp }, 50, Now);

        listings.ShouldAllBe(l => l.ExpiresAt > Now && l.ExpiresAt <= Now + AuctionRules.ListingDuration);
        listings.Select(l => l.ExpiresAt).Distinct().Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void 수량은_한_번에_올리는_상한을_넘지_않는다()
    {
        AuctionDummy.Build(new[] { Carp }, 50, Now).ShouldAllBe(l => l.Item.Count >= 1 && l.Item.Count <= AuctionRules.MaxListingCount);
    }

    // ─────────────────────── 원장 ───────────────────────

    [Fact]
    public async Task 넣으면_더미_판매자의_거래와_등록_메시지가_개수만큼_남는다()
    {
        var count = await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(new[] { Carp }, 5, Now), Now);

        count.ShouldBe(5);
        Scalar($"SELECT COUNT(*) FROM t_auction_trade WHERE seller_id = {AuctionDummy.SellerId} AND state = 1 AND listing_fee = 0").ShouldBe(5);
        Scalar("SELECT COUNT(*) FROM t_auction_outbox WHERE kind = 1 AND sent_at IS NULL").ShouldBe(5);
    }

    [Fact]
    public async Task 지우면_더미의_거래와_우편과_기록만_사라진다()
    {
        await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(new[] { Carp }, 3, Now), Now);
        var real = await AuctionDb.RegisterAsync(Conn, RealSeller, new AuctionItemSnapshot { Kind = EAuctionKind.Item, Tid = 10001, Count = 1 },
            30, 3, new List<ItemChangeInfo>(), gold: 100, Now.AddHours(48), Now);
        _db.Execute($"INSERT INTO t_user_mail (user_id, template_tid) VALUES ({AuctionDummy.SellerId}, 1), ({RealSeller}, 1)");
        _db.Execute($"INSERT INTO t_auction_history (user_id, type, kind, tid, count, unit_price, amount, fee, trade_id, created_at) VALUES ({AuctionDummy.SellerId}, 1, 1, 10001, 1, 10, 10, 0, 1, '2026-01-01 00:00:00')");

        var deleted = await AuctionDb.PurgeDummyAsync(Conn);

        deleted.ShouldBe(3);
        Scalar($"SELECT COUNT(*) FROM t_auction_trade WHERE seller_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE user_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_auction_history WHERE user_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar("SELECT COUNT(*) FROM t_auction_outbox").ShouldBe(1);
        Scalar($"SELECT COUNT(*) FROM t_auction_trade WHERE trade_id = {real}").ShouldBe(1);
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE user_id = {RealSeller}").ShouldBe(1);
    }

    [Fact]
    public async Task 지워도_구매_확정_메시지는_남긴다()
    {
        // 확정이 경매장에 안 가면 구매자가 잡은 예약이 타임아웃까지 매물을 붙든다.
        await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(new[] { Carp }, 1, Now), Now);
        var tradeId = Scalar("SELECT trade_id FROM t_auction_trade");
        _db.Execute($"INSERT INTO t_auction_outbox (trade_id, kind, payload, purchase_id) VALUES ({tradeId}, 2, '{{}}', 99)");

        await AuctionDb.PurgeDummyAsync(Conn);

        Scalar("SELECT COUNT(*) FROM t_auction_outbox WHERE kind = 2").ShouldBe(1);
    }
}
