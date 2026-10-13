using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 거래 기록(T-156 · 경매장 완료 탭) — 정산·반환 트랜잭션이 판매자·구매자 기록을 함께 남기고, 조회가 최신순·구분·기간·새 기록 수를 맞게 준다.
/// 틀리면 우편은 왔는데 기록이 없거나, 이미 본 기록에 배지가 다시 뜬다.
/// </summary>
public class AuctionHistoryDbTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const long Seller  = 7;
    private const long Buyer   = 8;
    private const long SellerB = 9;
    private const int  Wood    = 40001;

    private readonly SqliteFixture _db = new();

    public AuctionHistoryDbTest()
    {
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private DbConnection Conn => new(_db.Connection);

    private Task<long> ListWood(long seller = Seller, int count = 10, long unitPrice = 30, long fee = 3)
    {
        var item = new AuctionItemSnapshot { Kind = EAuctionKind.Item, Tid = Wood, Category = 4, Rarity = 1, Count = count };
        return AuctionDb.RegisterAsync(Conn, seller, item, unitPrice, fee, new List<ItemChangeInfo>(), gold: 1000, Now.AddHours(48), Now);
    }

    private Task<long> ListSword()
    {
        _db.Execute($"INSERT INTO t_user_equip (equip_id, user_id, equip_tid, enchant_grade) VALUES (55, {Seller}, 1001, 4)");
        var item = new AuctionItemSnapshot { Kind = EAuctionKind.Equip, Tid = 1001, Category = 1, Rarity = 3, Count = 1, EquipId = 55, EnchantGrade = 4 };
        return AuctionDb.RegisterAsync(Conn, Seller, item, 500, 5, new List<ItemChangeInfo>(), gold: 995, Now.AddHours(48), Now);
    }

    private Task<AuctionHistoryPage> Query(long userId, EAuctionHistoryType type = EAuctionHistoryType.None, int days = 0,
                                           long cursor = 0, int pageSize = 20, bool markSeen = false, DateTime? now = null)
        => AuctionHistoryDb.QueryAsync(Conn, userId, type, days, cursor, pageSize, markSeen, now ?? Now);

    private static (EAuctionHistoryType, int, long, long, long) Line(AuctionHistoryRow r)
        => ((EAuctionHistoryType)r.type, r.count, r.unit_price, r.amount, r.fee);

    // ── 남기기 ──

    [Fact]
    public async Task 경매장_정산은_판매자와_구매자_기록을_남긴다()
    {
        var tradeId = await ListSword();

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, purchaseId: 900, totalPrice: 500, buyerGold: 500, Now);

        // 판매 대금 500 − 수수료 5% 25 = 475
        var sold = (await Query(Seller)).Entries.Single();
        (Line(sold), sold.enchant_grade, sold.kind, sold.tid).ShouldBe(((EAuctionHistoryType.Sold, 1, 500L, 475L, 25L), 4, (int)EAuctionKind.Equip, 1001));
        Line((await Query(Buyer)).Entries.Single()).ShouldBe((EAuctionHistoryType.Bought, 1, 500L, 500L, 0L));
    }

    [Fact]
    public async Task 거래소_정산은_판매자마다_한_줄이고_구매자는_합친_한_줄이다()
    {
        var a = await ListWood(Seller, 10, 30);
        var b = await ListWood(SellerB, 10, 32);

        await AuctionDb.SettleMarketAsync(Conn, Buyer, purchaseId: 900, Wood,
            new[] { new MarketAllocation(a, 10, 30), new MarketAllocation(b, 2, 32) }, buyerGold: 500, Now);

        // A: 300 − 15 = 285 · B: 64 − 3(3.2 내림) = 61 · 구매자: 364, 평균 단가 364 / 12 = 30.3 → 30
        Line((await Query(Seller)).Entries.Single()).ShouldBe((EAuctionHistoryType.Sold, 10, 30L, 285L, 15L));
        Line((await Query(SellerB)).Entries.Single()).ShouldBe((EAuctionHistoryType.Sold, 2, 32L, 61L, 3L));
        Line((await Query(Buyer)).Entries.Single()).ShouldBe((EAuctionHistoryType.Bought, 12, 30L, 364L, 0L));
    }

    [Fact]
    public async Task 만료는_돌려받은_등록비를_취소는_0을_남긴다()
    {
        var expired   = await ListWood(fee: 3);
        var cancelled = await ListWood(fee: 4);

        await AuctionDb.ReturnAsync(Conn, expired, AuctionReturnReason.Expired, Now);
        await AuctionDb.ReturnAsync(Conn, cancelled, AuctionReturnReason.Cancelled, Now);

        // 최신순 — 취소가 나중이다
        (await Query(Seller)).Entries.Select(Line).ShouldBe(new[]
        {
            (EAuctionHistoryType.Cancelled, 10, 30L, 0L, 0L),
            (EAuctionHistoryType.Expired, 10, 30L, 3L, 0L),
        });
    }

    [Fact]
    public async Task 이미_끝난_거래를_다시_돌려도_기록은_한_줄이다()
    {
        var tradeId = await ListWood();

        await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now);
        await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now);

        (await Query(Seller)).Entries.Count.ShouldBe(1);
    }

    // ── 조회 ──

    [Fact]
    public async Task 구분과_커서로_거르고_넘긴다()
    {
        for (var i = 0; i < 3; i++)
        {
            await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now);
        }
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Cancelled, Now);

        var first  = await Query(Seller, EAuctionHistoryType.Expired, pageSize: 2);
        var second = await Query(Seller, EAuctionHistoryType.Expired, cursor: first.Entries[^1].history_id, pageSize: 2);

        (first.Entries.Count, first.HasMore, second.Entries.Count, second.HasMore).ShouldBe((2, true, 1, false));
        first.Entries.Concat(second.Entries).ShouldAllBe(r => r.type == (int)EAuctionHistoryType.Expired);
    }

    [Fact]
    public async Task 기간은_며칠_안에_일어난_것만_준다()
    {
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now);
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now.AddDays(5));

        (await Query(Seller, days: 1, now: Now.AddDays(5))).Entries.Count.ShouldBe(1);
        (await Query(Seller, days: 7, now: Now.AddDays(5))).Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task 새_기록_수는_보고_나면_0이고_그_뒤_것만_다시_센다()
    {
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now);
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now);

        (await Query(Seller, pageSize: 0)).UnseenCount.ShouldBe(2);
        (await Query(Seller, markSeen: true)).UnseenCount.ShouldBe(2);
        (await Query(Seller, pageSize: 0)).UnseenCount.ShouldBe(0);

        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Cancelled, Now);
        (await Query(Seller, pageSize: 0)).UnseenCount.ShouldBe(1);
    }

    [Fact]
    public async Task 쪽_크기_0은_세기만_하고_줄을_주지_않는다()
    {
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now);

        (await Query(Seller, pageSize: 0)).Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task 보관_기간이_지난_기록은_조회할_때_지운다()
    {
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now);
        await AuctionDb.ReturnAsync(Conn, await ListWood(), AuctionReturnReason.Expired, Now.AddDays(31));

        (await Query(Seller, now: Now.AddDays(31))).Entries.Count.ShouldBe(1);
        Convert.ToInt64(_db.Query("SELECT COUNT(*) FROM t_auction_history")).ShouldBe(1);
    }
}
