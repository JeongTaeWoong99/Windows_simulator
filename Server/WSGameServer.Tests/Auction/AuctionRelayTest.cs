using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 메인 → 경매장 릴레이 — outbox 전송 순서·재시도, 이벤트 반환·확인, 대사.
/// 여기가 틀리면 등록이 경매장에 영영 안 닿거나, 취소한 아이템이 잠긴 채 남거나, 같은 반환이 두 번 나간다.
/// </summary>
public class AuctionRelayTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const long Seller = 7;

    private readonly SqliteFixture      _db       = new();
    private readonly FakeAuctionClient  _client   = new();
    private readonly FakeLogicExecutor  _logic    = new() { RunImmediately = true };
    private readonly AuctionRelay       _relay;

    public AuctionRelayTest()
    {
        _db.CreatePlayerTables();
        _db.CreateMailTables();
        _db.CreateAuctionTables();

        _relay = new AuctionRelay(_client, new SqliteRunner(_db), _logic, _ => null);
        _client.Register    = _ => Task.FromResult(new Proto.RegisterReply { Result = Proto.RegisterResult.Ok });
        _client.Confirm     = _ => Task.CompletedTask;
        _client.FetchEvents = _ => Task.FromResult(new Proto.FetchEventsReply());
        _client.AckEvents   = _ => Task.CompletedTask;
    }

    public void Dispose() => _db.Dispose();

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private Task<long> Register(int count = 10, long unitPrice = 30, long fee = 3)
    {
        var item = new AuctionItemSnapshot { Kind = EAuctionKind.Item, Tid = 10001, Category = 2, Rarity = 1, Count = count };
        return AuctionDb.RegisterAsync(Conn, Seller, item, unitPrice, fee, new List<ItemChangeInfo>(), 997, 0, Now.AddHours(48), Now);
    }

    private void Events(params (long EventId, Proto.EventKind Kind, long ListingId)[] events)
    {
        var reply = new Proto.FetchEventsReply();
        reply.Events.AddRange(events.Select(e => new Proto.AuctionEvent { EventId = e.EventId, Kind = e.Kind, ListingId = e.ListingId, SellerId = Seller }));
        _client.FetchEvents = _ => Task.FromResult(reply);
    }

    private void States(params (long ListingId, Proto.ListingState State)[] states)
    {
        var reply = new Proto.ListingStatesReply();
        reply.States.AddRange(states.Select(s => new Proto.ListingStateEntry { ListingId = s.ListingId, State = s.State }));
        _client.ListingStates = _ => Task.FromResult(reply);
    }

    // ── outbox ──

    [Fact]
    public async Task 등록_메시지를_경매장_스냅샷으로_옮겨_보낸다()
    {
        var tradeId = await Register(count: 10, unitPrice: 30);

        await _relay.FlushOutboxAsync(Now);

        var sent = _client.RequestsOf<Proto.RegisterRequest>().Single().Listing;
        (sent.ListingId, sent.SellerId, sent.Kind, sent.Tid, sent.Count, sent.UnitPrice).ShouldBe((tradeId, Seller, 1, 10001, 10, 30L));
        sent.ExpiresAtUnixMs.ShouldBe(new DateTimeOffset(Now.AddHours(48)).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task 보낸_메시지는_다시_보내지_않는다()
    {
        await Register();
        await _relay.FlushOutboxAsync(Now);

        await _relay.FlushOutboxAsync(Now);

        _client.RequestsOf<Proto.RegisterRequest>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task 전송에_실패하면_거기서_멈추고_다음_바퀴에_이어_보낸다()
    {
        var first  = await Register();
        var second = await Register();
        _client.Register = r => r.Listing.ListingId == second
            ? Task.FromException<Proto.RegisterReply>(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "끊김")))
            : Task.FromResult(new Proto.RegisterReply());

        (await _relay.FlushOutboxAsync(Now)).ShouldBeFalse();
        _client.Register = _ => Task.FromResult(new Proto.RegisterReply());
        (await _relay.FlushOutboxAsync(Now)).ShouldBeTrue();

        _client.RequestsOf<Proto.RegisterRequest>().Select(r => r.Listing.ListingId).ShouldBe(new[] { first, second, second });
    }

    [Fact]
    public async Task 경매장이_등록을_거부하면_등록비까지_반환한다()
    {
        var tradeId = await Register(fee: 3);
        _client.Register = _ => Task.FromResult(new Proto.RegisterReply { Result = Proto.RegisterResult.Rejected });

        await _relay.FlushOutboxAsync(Now);

        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe(AuctionDb.Returned);
        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = 7 AND template_tid = {AuctionMail.FailedTemplateTid}").ShouldBe(3);
    }

    [Fact]
    public async Task 확정_메시지를_보낸다()
    {
        var tradeId = await Register(count: 10, unitPrice: 30);
        await _relay.FlushOutboxAsync(Now);
        await AuctionDb.SettleAsync(Conn, tradeId, 8, purchaseId: 900, totalPrice: 300, 700, 0, Now);

        await _relay.FlushOutboxAsync(Now);

        var confirm = _client.RequestsOf<Proto.ConfirmRequest>().Single();
        (confirm.PurchaseId, confirm.Success).ShouldBe((900L, true));
    }

    [Fact]
    public async Task 경매장에_닿지_않으면_이벤트도_건너뛴다()
    {
        await Register();
        _client.Register = null;

        (await _relay.RunOnceAsync(Now)).ShouldBeFalse();

        _client.RequestsOf<Proto.FetchEventsRequest>().ShouldBeEmpty();
    }

    // ── 이벤트 ──

    [Fact]
    public async Task 취소_이벤트는_등록비_없이_반환하고_확인한다()
    {
        var tradeId = await Register(fee: 3);
        Events((1, Proto.EventKind.Cancelled, tradeId));

        await _relay.DrainEventsAsync(Now);

        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = 7 AND template_tid = {AuctionMail.CancelledTemplateTid}").ShouldBe(0);
        _client.RequestsOf<Proto.AckEventsRequest>().Single().EventIds.ShouldBe(new[] { 1L });
    }

    [Fact]
    public async Task 만료_이벤트는_등록비와_함께_반환한다()
    {
        var tradeId = await Register(fee: 3);
        Events((1, Proto.EventKind.Expired, tradeId));

        await _relay.DrainEventsAsync(Now);

        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = 7 AND template_tid = {AuctionMail.ExpiredTemplateTid}").ShouldBe(3);
    }

    [Fact]
    public async Task 같은_이벤트가_두_번_와도_반환_우편은_한_통이다()
    {
        var tradeId = await Register();
        Events((1, Proto.EventKind.Cancelled, tradeId));
        await _relay.DrainEventsAsync(Now);

        await _relay.DrainEventsAsync(Now);   // 확인이 유실돼 같은 이벤트가 다시 왔다

        Scalar("SELECT COUNT(*) FROM t_user_mail").ShouldBe(1);
    }

    [Fact]
    public async Task 반환하면_접속_중인_판매자에게_알린다()
    {
        var b = new TestUserBuilder();
        var seller = b.Build(uid: Seller);
        seller.OnAuctionStateLoaded(1);
        var relay = new AuctionRelay(_client, new SqliteRunner(_db), _logic, uid => uid == Seller ? seller : null);
        var tradeId = await Register();
        Events((1, Proto.EventKind.Cancelled, tradeId));

        await relay.DrainEventsAsync(Now);

        b.Channel.SentOf<S_MailArrivedResponse>().Single().Mails!.Single().TemplateTid.ShouldBe(AuctionMail.CancelledTemplateTid);
        seller.ActiveListingCount.ShouldBe(0);
    }

    [Fact]
    public async Task 이벤트가_없으면_확인도_보내지_않는다()
    {
        await _relay.DrainEventsAsync(Now);

        _client.RequestsOf<Proto.AckEventsRequest>().ShouldBeEmpty();
    }

    // ── 대사 ──

    private async Task<long> RegisterAndSend(long fee = 3)
    {
        var tradeId = await Register(fee: fee);
        await _relay.FlushOutboxAsync(Now);
        return tradeId;
    }

    [Fact]
    public async Task 경매장에_없는_오래된_거래는_등록비까지_반환한다()
    {
        var tradeId = await RegisterAndSend(fee: 3);
        States((tradeId, Proto.ListingState.NotFound));

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);

        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = 7 AND template_tid = {AuctionMail.FailedTemplateTid}").ShouldBe(3);
    }

    [Fact]
    public async Task 경매장에서_판매중인_거래는_그대로_둔다()
    {
        var tradeId = await RegisterAndSend();
        States((tradeId, Proto.ListingState.Listed));

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);

        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe(AuctionDb.Listed);
    }

    [Fact]
    public async Task 경매장이_판매됐다는데_메인이_판매중이면_손대지_않는다()
    {
        var tradeId = await RegisterAndSend();
        States((tradeId, Proto.ListingState.Sold));

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);

        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe(AuctionDb.Listed);
        Scalar("SELECT COUNT(*) FROM t_user_mail").ShouldBe(0);
    }

    [Fact]
    public async Task 경매장에서_만료된_거래는_이벤트를_놓쳤어도_반환한다()
    {
        var tradeId = await RegisterAndSend(fee: 3);
        States((tradeId, Proto.ListingState.Expired));

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);

        Scalar($"SELECT gold FROM t_user_mail WHERE template_tid = {AuctionMail.ExpiredTemplateTid}").ShouldBe(3);
    }

    [Fact]
    public async Task 대사는_앞쪽_정상_매물에_막히지_않고_다음_묶음으로_넘어간다()
    {
        for (var i = 0; i <= AuctionRelay.BatchSize; i++)
        {
            await Register();
        }
        await _relay.FlushOutboxAsync(Now);
        await _relay.FlushOutboxAsync(Now);   // outbox도 한 번에 BatchSize씩 보낸다
        _client.ListingStates = r =>
        {
            var reply = new Proto.ListingStatesReply();
            reply.States.AddRange(r.ListingIds.Select(id => new Proto.ListingStateEntry { ListingId = id, State = Proto.ListingState.Listed }));
            return Task.FromResult(reply);
        };

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);
        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);
        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);

        // 101건 = 100건 + 1건, 끝까지 가면 처음으로 돌아간다
        _client.RequestsOf<Proto.ListingStatesRequest>().Select(r => r.ListingIds.Count).ShouldBe(new[] { 100, 1, 100 });
    }

    [Fact]
    public async Task 아직_어린_거래는_묻지_않는다()
    {
        await RegisterAndSend();

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter - TimeSpan.FromSeconds(1));

        _client.RequestsOf<Proto.ListingStatesRequest>().ShouldBeEmpty();
    }

    [Fact]
    public async Task 등록_메시지를_아직_못_보낸_거래는_묻지_않는다()
    {
        await Register();   // outbox에 남아 있다 — 경매장이 모르는 게 정상이다

        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);

        _client.RequestsOf<Proto.ListingStatesRequest>().ShouldBeEmpty();
    }

    [Fact]
    public async Task 깨우면_주기를_기다리지_않고_돈다()
    {
        await Register();
        using var stop = new CancellationTokenSource();
        var loop = _relay.RunAsync(() => Now, stop.Token);

        for (var i = 0; i < 100 && _client.RequestsOf<Proto.RegisterRequest>().Count == 0; i++)
        {
            _relay.Kick();
            await Task.Delay(10);
        }

        stop.Cancel();
        await loop;
        _client.RequestsOf<Proto.RegisterRequest>().Count.ShouldBe(1);
    }
}
