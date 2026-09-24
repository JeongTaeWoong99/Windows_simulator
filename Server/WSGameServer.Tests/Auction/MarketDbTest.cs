using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 거래소 정산 SQL — 여러 판매자의 거래에서 수량을 나눠 차감하고, 판매자별로 대금을 보낸다.
/// 한 줄이라도 어긋나면 전부 되돌아가야 한다 — 일부만 차감되면 산 적 없는 수량이 사라지거나 대금이 빠진다.
/// </summary>
public class MarketDbTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int  Carp   = 10001;
    private const long SellerA = 7, SellerB = 9, Buyer = 8;

    private readonly SqliteFixture _db = new();

    public MarketDbTest()
    {
        _db.CreatePlayerTables();
        _db.CreateMailTables();
        _db.CreateAuctionTables();
    }

    public void Dispose() => _db.Dispose();

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private string? Text(string sql) => _db.Query(sql) as string;

    private Task<long> List(long seller, int count, long unitPrice, long fee = 10, int tid = Carp)
    {
        var item = new AuctionItemSnapshot { Kind = EAuctionKind.Item, Tid = tid, Category = 2, Rarity = 1, Count = count };
        return AuctionDb.RegisterAsync(Conn, seller, item, unitPrice, fee, new List<ItemChangeInfo>(), 1000, 0, Now.AddHours(48), Now);
    }

    private Task<AuctionMarketSettleResult> Settle(params MarketAllocation[] lines)
        => AuctionDb.SettleMarketAsync(Conn, Buyer, purchaseId: 900, Carp, lines, buyerGold: 500, buyerDia: 0, Now);

    [Fact]
    public async Task 여러_판매자에서_산_수량이_구매자_우편_한_통으로_간다()
    {
        var a = await List(SellerA, 10, 30);
        var b = await List(SellerB, 10, 32);

        (await Settle(new MarketAllocation(a, 10, 30), new MarketAllocation(b, 5, 32))).Settled.ShouldBeTrue();

        Text($"SELECT items FROM t_user_mail WHERE user_id = {Buyer}").ShouldBe("[[10001,15]]");
    }

    [Fact]
    public async Task 판매자마다_수수료를_뺀_대금이_간다()
    {
        var a = await List(SellerA, 10, 30);
        var b = await List(SellerB, 10, 32);

        await Settle(new MarketAllocation(a, 10, 30), new MarketAllocation(b, 5, 32));

        // A: 300 − 15 = 285 · B: 160 − 8 = 152
        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = {SellerA}").ShouldBe(285);
        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = {SellerB}").ShouldBe(152);
    }

    [Fact]
    public async Task 한_판매자의_여러_매물은_대금_우편_한_통으로_합친다()
    {
        var a1 = await List(SellerA, 10, 30);
        var a2 = await List(SellerA, 10, 31);

        var result = await Settle(new MarketAllocation(a1, 10, 30), new MarketAllocation(a2, 2, 31));

        // (300 − 15) + (62 − 3) = 344
        result.SellerMails.Select(m => (m.SellerId, m.Mail.gold)).ShouldBe(new[] { (SellerA, 344L) });
    }

    [Fact]
    public async Task 일부만_팔린_거래는_남은_수량으로_판매_중이다()
    {
        var a = await List(SellerA, 10, 30);

        await Settle(new MarketAllocation(a, 4, 30));

        (Scalar($"SELECT count FROM t_auction_trade WHERE trade_id = {a}"), Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {a}"))
            .ShouldBe((6L, (long)AuctionDb.Listed));
    }

    [Fact]
    public async Task 다_팔린_거래는_정산_완료이고_판매자별로_센다()
    {
        var a = await List(SellerA, 10, 30);
        var b = await List(SellerB, 10, 32);

        var result = await Settle(new MarketAllocation(a, 10, 30), new MarketAllocation(b, 5, 32));

        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {a}").ShouldBe(AuctionDb.Settled);
        result.SellerMails.Select(m => (m.SellerId, m.ClosedListings)).ShouldBe(new[] { (SellerA, 1), (SellerB, 0) });
    }

    [Fact]
    public async Task 수수료는_거래에_쌓인다()
    {
        var a = await List(SellerA, 10, 30);

        await Settle(new MarketAllocation(a, 4, 30));
        await AuctionDb.SettleMarketAsync(Conn, Buyer, 901, Carp, new[] { new MarketAllocation(a, 6, 30) }, 500, 0, Now);

        // 120 × 5% = 6 · 180 × 5% = 9
        Scalar($"SELECT sale_fee FROM t_auction_trade WHERE trade_id = {a}").ShouldBe(15);
    }

    [Fact]
    public async Task 정산에_성공하면_확정_메시지_한_줄이_남는다()
    {
        var a = await List(SellerA, 10, 30);
        var b = await List(SellerB, 10, 32);
        foreach (var row in await AuctionDb.LoadUnsentAsync(Conn, 10))
        {
            await AuctionDb.MarkSentAsync(Conn, row.outbox_id, Now);
        }

        await Settle(new MarketAllocation(a, 10, 30), new MarketAllocation(b, 5, 32));

        (await AuctionDb.LoadUnsentAsync(Conn, 10)).Select(r => r.payload).ShouldBe(new[] { "{\"PurchaseId\":900,\"Success\":true}" });
    }

    [Fact]
    public async Task 한_줄이라도_수량이_모자라면_아무것도_차감하지_않는다()
    {
        var a = await List(SellerA, 10, 30);
        var b = await List(SellerB, 3, 32);

        (await Settle(new MarketAllocation(a, 10, 30), new MarketAllocation(b, 5, 32))).Settled.ShouldBeFalse();

        Scalar($"SELECT count FROM t_auction_trade WHERE trade_id = {a}").ShouldBe(10);
        Scalar($"SELECT COUNT(*) FROM t_user_mail").ShouldBe(0);
    }

    [Fact]
    public async Task 실패하면_구매자_잔액을_되돌려_쓰고_확정_실패를_남긴다()
    {
        var a = await List(SellerA, 10, 30);
        await AuctionDb.ReturnAsync(Conn, a, AuctionReturnReason.Cancelled, Now);

        await Settle(new MarketAllocation(a, 4, 30));

        // 메모리는 구매 전 620 − 120 = 500을 들고 있다. 실패면 500 + 120 = 620이 남아야 한다.
        Scalar($"SELECT gold FROM t_user_currency WHERE user_id = {Buyer}").ShouldBe(620);
        Text($"SELECT payload FROM t_auction_outbox WHERE kind = 2").ShouldBe("{\"PurchaseId\":900,\"Success\":false}");
    }

    [Fact]
    public async Task 단가가_예약과_다르면_정산하지_않는다()
    {
        var a = await List(SellerA, 10, 30);

        (await Settle(new MarketAllocation(a, 4, 29))).Settled.ShouldBeFalse();
    }

    [Fact]
    public async Task 다른_종류의_거래는_정산하지_않는다()
    {
        var pike = await List(SellerA, 10, 30, tid: 10002);

        (await Settle(new MarketAllocation(pike, 4, 30))).Settled.ShouldBeFalse();
    }

    [Fact]
    public async Task 자기_거래는_정산하지_않는다()
    {
        var own = await List(Buyer, 10, 30);

        (await Settle(new MarketAllocation(own, 4, 30))).Settled.ShouldBeFalse();
    }

    // ── 일부 팔린 거래의 반환 ──

    [Fact]
    public async Task 일부_팔린_거래가_취소되면_남은_수량만_돌아간다()
    {
        var a = await List(SellerA, 10, 30, fee: 10);
        await Settle(new MarketAllocation(a, 4, 30));

        var result = await AuctionDb.ReturnAsync(Conn, a, AuctionReturnReason.Cancelled, Now);

        MailAttachment.FromRow(result!.Mail).Items.Single().ShouldBe((Carp, 6));
    }

    [Fact]
    public async Task 일부_팔린_거래가_만료되면_등록비도_남은_비율만큼_돌려준다()
    {
        var a = await List(SellerA, 10, 30, fee: 10);
        await Settle(new MarketAllocation(a, 4, 30));

        var result = await AuctionDb.ReturnAsync(Conn, a, AuctionReturnReason.Expired, Now);

        // 10 × 6 / 10 = 6
        MailAttachment.FromRow(result!.Mail).Gold.ShouldBe(6);
    }

    [Fact]
    public async Task 나누어떨어지지_않는_환급은_내림한다()
    {
        var a = await List(SellerA, 3, 30, fee: 10);
        await Settle(new MarketAllocation(a, 1, 30));

        var result = await AuctionDb.ReturnAsync(Conn, a, AuctionReturnReason.Expired, Now);

        // 10 × 2 / 3 = 6.67 → 6
        MailAttachment.FromRow(result!.Mail).Gold.ShouldBe(6);
    }

    [Fact]
    public async Task 일부_팔린_매물을_통째로_사면_남은_수량만큼만_받는다()
    {
        var a = await List(SellerA, 10, 30);
        await Settle(new MarketAllocation(a, 4, 30));

        var result = await AuctionDb.SettleAsync(Conn, a, 11, 901, totalPrice: 180, 500, 0, Now);

        MailAttachment.FromRow(result.BuyerMail!).Items.Single().ShouldBe((Carp, 6));
        Scalar($"SELECT count FROM t_auction_trade WHERE trade_id = {a}").ShouldBe(0);
    }
}
