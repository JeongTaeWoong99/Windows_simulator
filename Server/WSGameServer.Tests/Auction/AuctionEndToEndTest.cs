using AuctionServer;
using GameData;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 경매 한 바퀴를 실제 부품으로 돈다 — 경매장 서버(TestServer·gRPC) + 메인 SQL(:memory:) + 유저 로직 + 릴레이.
/// 가짜는 네트워크 소켓과 시계뿐이다. 부품 테스트가 다 초록이어도 이음새(스냅샷 매핑·우편 첨부·잠금 해제)가
/// 어긋나면 여기서 빨개진다.
/// </summary>
public class AuctionEndToEndTest : IAsyncLifetime
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int  Carp   = 10001;   // 붕어 — BasePrice 10
    private const int  WoodSwordTid = 1001;   // 목검 — BasePrice 70
    private const long Sword  = 55;
    private const long SellerUid = 7, BuyerUid = 8, RivalUid = 9;

    private readonly string           _auctionPath = Path.Combine(Path.GetTempPath(), $"auction-e2e-{Guid.NewGuid():N}.sqlite3");
    private readonly FakeTimeProvider _auctionTime = new(new DateTimeOffset(Now));

    private readonly SqliteFixture       _db    = new();
    private readonly PumpedLogicExecutor _logic = new();
    private readonly Dictionary<long, User>            _online   = new();
    private readonly Dictionary<long, TestUserBuilder> _builders = new();

    private WebApplication    _app     = null!;
    private GrpcChannel       _channel = null!;
    private GrpcAuctionClient _client  = null!;
    private AuctionService    _service = null!;
    private AuctionRelay      _relay   = null!;

    public async Task InitializeAsync()
    {
        GameTableFixture.EnsureLoaded();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Auction:DbPath"] = _auctionPath;
        builder.Services.AddSingleton<TimeProvider>(_auctionTime);
        AuctionHost.ConfigureServices(builder);
        _app = builder.Build();
        _app.Services.GetRequiredService<AuctionEngine>();
        AuctionHost.MapEndpoints(_app);
        await _app.StartAsync();

        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = _app.GetTestServer().CreateHandler() });
        _client  = new GrpcAuctionClient(_channel, TimeSpan.FromSeconds(5));

        _db.CreatePlayerTables();
        _db.CreateMailTables();
        _db.CreateAuctionTables();

        _service = new AuctionService(_client, _logic) { FindOnlineUser = uid => _online.GetValueOrDefault(uid) };
        _relay   = new AuctionRelay(_client, new SqliteRunner(_db), _logic, _service.FindOnlineUser);
    }

    public async Task DisposeAsync()
    {
        _channel.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_auctionPath + suffix);
        }
    }

    private User Login(long uid, long gold = 1000, int carp = 20)
    {
        var b = new TestUserBuilder { Auction = _service, QueueOverride = new SqliteDBQueue(_db, _logic), ExecutorOverride = _logic };
        var user = b.WithPid($"pid-{uid}").Build(uid);
        user.OnAuctionStateLoaded(0);
        user.GainGold(gold);
        user.AddItem(Carp, carp);
        _logic.Drain();

        _online[uid]   = user;
        _builders[uid] = b;
        return user;
    }

    private List<T> Sent<T>(long uid) where T : IPacket => _builders[uid].Channel.SentOf<T>();

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private long RegisterItem(User seller, int count, long unitPrice)
    {
        seller.TryRegisterAuction(EAuctionKind.Item, Carp, count, 0, unitPrice, Now);
        _logic.Drain();

        var response = Sent<S_AuctionRegisterResponse>(seller.Uid).Last();
        response.Result.ShouldBe(EResultCode.Ok);
        return response.ListingId;
    }

    private async Task<S_AuctionBuyResponse> Buy(User buyer, long listingId, long total)
    {
        var before = Sent<S_AuctionBuyResponse>(buyer.Uid).Count;
        buyer.TryBuyAuction(listingId, total, Now);
        await _logic.Pump(() => Sent<S_AuctionBuyResponse>(buyer.Uid).Count > before);
        return Sent<S_AuctionBuyResponse>(buyer.Uid).Last();
    }

    private async Task<S_AuctionSearchResponse> Search(User user, C_AuctionSearchRequest request)
    {
        var before = Sent<S_AuctionSearchResponse>(user.Uid).Count;
        user.TrySearchAuction(request, Now);
        await _logic.Pump(() => Sent<S_AuctionSearchResponse>(user.Uid).Count > before);
        return Sent<S_AuctionSearchResponse>(user.Uid).Last();
    }

    private async Task<Proto.ListingState> AuctionState(long listingId)
    {
        var request = new Proto.ListingStatesRequest();
        request.ListingIds.Add(listingId);
        return (await _client.GetListingStatesAsync(request)).States.Single().State;
    }

    private void ClaimAll(User user)
    {
        user.TryClaimMail(0, Now);
        _logic.Drain();
    }

    // ── 자원 ──

    [Fact]
    public async Task 자원을_올리면_다른_유저가_검색해서_사고_우편으로_받는다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid, carp: 0);

        var listingId = RegisterItem(seller, count: 10, unitPrice: 30);
        await _relay.FlushOutboxAsync(Now);

        var found = await Search(buyer, new C_AuctionSearchRequest { Tids = new() { Carp } });
        var listing = found.Listings!.Single();
        (listing.ListingId, listing.Count, listing.TotalPrice).ShouldBe((listingId, 10, 300L));

        (await Buy(buyer, listingId, listing.TotalPrice)).Result.ShouldBe(EResultCode.Ok);
        ClaimAll(buyer);

        buyer.GetItemCount(Carp).ShouldBe(10);
        buyer.Gold.ShouldBe(700);
    }

    [Fact]
    public async Task 판매자는_수수료를_뺀_대금을_우편으로_받는다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var listingId = RegisterItem(seller, count: 10, unitPrice: 30);
        await _relay.FlushOutboxAsync(Now);

        await Buy(buyer, listingId, 300);
        ClaimAll(seller);

        // 1000 − 등록비 3 + (300 − 수수료 15) = 1282
        seller.Gold.ShouldBe(1282);
        seller.ActiveListingCount.ShouldBe(0);
    }

    [Fact]
    public async Task 확정이_전달되면_경매장_매물이_판매_완료가_된다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);
        await Buy(buyer, listingId, 300);

        await _relay.FlushOutboxAsync(Now);

        (await AuctionState(listingId)).ShouldBe(Proto.ListingState.Sold);
        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {listingId}").ShouldBe(AuctionDb.Settled);
    }

    [Fact]
    public async Task 오프라인_판매자의_대금은_DB_우편함에_남는다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);
        _online.Remove(SellerUid);

        await Buy(buyer, listingId, 300);

        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = {SellerUid} AND claimed_at IS NULL").ShouldBe(285);
        seller.TryGetMail(1, out _).ShouldBeFalse();
    }

    // ── 장비 ──

    [Fact]
    public async Task 인챈트_장비가_옵션까지_그대로_구매자_창고에_들어온다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        _builders[SellerUid].Enchants.LoadAll();
        _builders[BuyerUid].Enchants.LoadAll();
        _db.Execute($@"INSERT INTO t_user_equip (equip_id, user_id, equip_tid, slot_position, enchant_grade, enchant_1, enchant_2)
                       VALUES ({Sword}, {SellerUid}, {WoodSwordTid}, 0, {(int)GlobalRarity.Rare}, 101, 102)");
        seller.LoadEquips(
            new[] { new UserEquipRow { equip_id = Sword, equip_tid = WoodSwordTid, enchant_grade = (int)GlobalRarity.Rare, enchant_1 = 101, enchant_2 = 102 } },
            Array.Empty<CharacterEquipRow>());

        seller.TryRegisterAuction(EAuctionKind.Equip, 0, 0, Sword, 70, Now);
        _logic.Drain();
        await _relay.FlushOutboxAsync(Now);

        var found = await Search(buyer, new C_AuctionSearchRequest { Kind = EAuctionKind.Equip, OptionTids = new() { 102 } });
        var listing = found.Listings!.Single();
        (await Buy(buyer, listing.ListingId, listing.TotalPrice)).Result.ShouldBe(EResultCode.Ok);
        ClaimAll(buyer);

        buyer.TryGetEquip(Sword, out var equip).ShouldBeTrue();
        equip.EnchantGrade.ShouldBe(GlobalRarity.Rare);
        equip.EnchantOptionTids.ShouldBe(new[] { 101, 102 });
        Scalar($"SELECT user_id FROM t_user_equip WHERE equip_id = {Sword}").ShouldBe(BuyerUid);
        Scalar($"SELECT auction_trade_id FROM t_user_equip WHERE equip_id = {Sword}").ShouldBe(0);
        seller.TryGetEquip(Sword, out _).ShouldBeFalse();
    }

    // ── 취소 · 만료 ──

    [Fact]
    public async Task 취소하면_물건은_돌아오고_등록비는_돌아오지_않는다()
    {
        var seller = Login(SellerUid);
        var listingId = RegisterItem(seller, count: 10, unitPrice: 30);
        await _relay.FlushOutboxAsync(Now);

        seller.TryCancelAuction(listingId);
        await _logic.Pump(() => Sent<S_AuctionCancelResponse>(SellerUid).Count > 0);
        await _relay.DrainEventsAsync(Now);
        _logic.Drain();
        ClaimAll(seller);

        Sent<S_AuctionCancelResponse>(SellerUid).Single().Result.ShouldBe(EResultCode.Ok);
        (seller.GetItemCount(Carp), seller.Gold).ShouldBe((20, 997L));
        seller.ActiveListingCount.ShouldBe(0);
    }

    [Fact]
    public async Task 기간이_지나면_물건과_등록비가_함께_돌아온다()
    {
        var seller = Login(SellerUid);
        RegisterItem(seller, count: 10, unitPrice: 30);
        await _relay.FlushOutboxAsync(Now);

        _auctionTime.Advance(AuctionRules.ListingDuration);
        await _app.Services.GetRequiredService<AuctionEngine>().SweepAsync();
        await _relay.DrainEventsAsync(Now);
        _logic.Drain();
        ClaimAll(seller);

        (seller.GetItemCount(Carp), seller.Gold).ShouldBe((20, 1000L));
    }

    [Fact]
    public async Task 만료된_매물은_살_수_없다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);
        _auctionTime.Advance(AuctionRules.ListingDuration);

        (await Buy(buyer, listingId, 300)).Result.ShouldBe(EResultCode.AuctionClosed);
        buyer.Gold.ShouldBe(1000);
    }

    // ── 경합 · 실패 ──

    [Fact]
    public async Task 두_명이_동시에_사면_한_명만_산다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var rival  = Login(RivalUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);

        buyer.TryBuyAuction(listingId, 300, Now);
        rival.TryBuyAuction(listingId, 300, Now);
        await _logic.Pump(() => Sent<S_AuctionBuyResponse>(BuyerUid).Count > 0 && Sent<S_AuctionBuyResponse>(RivalUid).Count > 0);

        var results = new[] { Sent<S_AuctionBuyResponse>(BuyerUid).Single().Result, Sent<S_AuctionBuyResponse>(RivalUid).Single().Result };
        results.Count(r => r == EResultCode.Ok).ShouldBe(1);
        (buyer.Gold + rival.Gold).ShouldBe(1700);
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE template_tid = {AuctionMail.PurchasedTemplateTid}").ShouldBe(1);
    }

    [Fact]
    public async Task 본_가격이_바뀌었으면_사지_않고_골드도_그대로다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);

        (await Buy(buyer, listingId, 250)).Result.ShouldBe(EResultCode.AuctionPriceChanged);
        (buyer.Gold, buyer.AvailableGold).ShouldBe((1000L, 1000L));
    }

    [Fact]
    public async Task 자기_매물은_살_수_없다()
    {
        var seller = Login(SellerUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);

        (await Buy(seller, listingId, 300)).Result.ShouldBe(EResultCode.AuctionOwnListing);
    }

    [Fact]
    public async Task 경매장에_등록되기_전의_매물은_검색에_없다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        RegisterItem(seller, 10, 30);

        (await Search(buyer, new C_AuctionSearchRequest())).Listings!.ShouldBeEmpty();
    }

    [Fact]
    public async Task 경매장이_받았다가_잃은_매물은_대사가_등록비까지_돌려준다()
    {
        var seller = Login(SellerUid);
        var listingId = RegisterItem(seller, count: 10, unitPrice: 30);

        // 경매장이 ack 뒤 커밋을 잃은 상황 — 메인은 보냈다고 적었지만 경매장은 모른다.
        _db.Execute("UPDATE t_auction_outbox SET sent_at = '2026-08-03 00:00:00'");
        await _relay.ReconcileAsync(Now + AuctionRelay.StaleAfter);
        _logic.Drain();
        ClaimAll(seller);

        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {listingId}").ShouldBe(AuctionDb.Returned);
        (seller.GetItemCount(Carp), seller.Gold).ShouldBe((20, 1000L));
    }

    [Fact]
    public async Task 내_매물에_올린_것이_보인다()
    {
        var seller = Login(SellerUid);
        var listingId = RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);

        seller.TryGetMyAuctionListings();
        await _logic.Pump(() => Sent<S_AuctionMyListingsResponse>(SellerUid).Count > 0);

        Sent<S_AuctionMyListingsResponse>(SellerUid).Single().Listings!.Single().ListingId.ShouldBe(listingId);
    }
    // ── 거래소 (수량 구매) ──

    private async Task<S_MarketBuyResponse> BuyMarket(User buyer, int count, long maxUnitPrice)
    {
        var before = Sent<S_MarketBuyResponse>(buyer.Uid).Count;
        buyer.TryBuyMarket(Carp, count, maxUnitPrice, Now);
        await _logic.Pump(() => Sent<S_MarketBuyResponse>(buyer.Uid).Count > before);
        return Sent<S_MarketBuyResponse>(buyer.Uid).Last();
    }

    private async Task<MarketItemInfo> MarketItem(User user)
    {
        var before = Sent<S_MarketItemsResponse>(user.Uid).Count;
        user.TryGetMarketItems(0, new List<int> { Carp }, Now);
        await _logic.Pump(() => Sent<S_MarketItemsResponse>(user.Uid).Count > before);
        return Sent<S_MarketItemsResponse>(user.Uid).Last().Items!.Single();
    }

    [Fact]
    public async Task 거래소에서_여러_판매자의_매물을_최저가부터_원하는_수량만큼_산다()
    {
        var sellerA = Login(SellerUid);
        var sellerB = Login(RivalUid);
        var buyer   = Login(BuyerUid, carp: 0);
        RegisterItem(sellerA, count: 10, unitPrice: 30);
        RegisterItem(sellerB, count: 10, unitPrice: 32);
        await _relay.FlushOutboxAsync(Now);

        var before = await MarketItem(buyer);
        (before.LowestUnitPrice, before.AvailableCount).ShouldBe((30L, 20L));

        var bought = await BuyMarket(buyer, count: 15, maxUnitPrice: 32);
        ClaimAll(buyer);

        // 30 × 10 + 32 × 5 = 460
        (bought.Result, bought.TotalPrice).ShouldBe((EResultCode.Ok, 460L));
        (buyer.GetItemCount(Carp), buyer.Gold).ShouldBe((15, 540L));
    }

    [Fact]
    public async Task 거래소_판매자는_각자_판_만큼_대금을_받고_남은_수량은_계속_팔린다()
    {
        var sellerA = Login(SellerUid);
        var sellerB = Login(RivalUid);
        var buyer   = Login(BuyerUid);
        RegisterItem(sellerA, 10, 30);
        RegisterItem(sellerB, 10, 32);
        await _relay.FlushOutboxAsync(Now);

        await BuyMarket(buyer, 15, 32);
        await _relay.FlushOutboxAsync(Now);
        ClaimAll(sellerA);
        ClaimAll(sellerB);

        // A: 1000 − 등록비 3 + (300 − 15) = 1282 · B: 1000 − 등록비 3 + (160 − 8) = 1149
        (sellerA.Gold, sellerB.Gold).ShouldBe((1282L, 1149L));
        var after = await MarketItem(buyer);
        (after.LowestUnitPrice, after.AvailableCount, after.RecentUnitPrice).ShouldBe((32L, 5L, 32L));
    }

    [Fact]
    public async Task 일부_팔린_매물을_취소하면_남은_수량만_돌아온다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var listingId = RegisterItem(seller, count: 10, unitPrice: 30);
        await _relay.FlushOutboxAsync(Now);
        await BuyMarket(buyer, 4, 30);
        await _relay.FlushOutboxAsync(Now);

        seller.TryCancelAuction(listingId);
        await _logic.Pump(() => Sent<S_AuctionCancelResponse>(SellerUid).Count > 0);
        await _relay.DrainEventsAsync(Now);
        _logic.Drain();
        ClaimAll(seller);

        // 창고 20 − 10 + 6 = 16 · 골드 1000 − 3 + (120 − 6) = 1111 (취소라 등록비는 없다)
        (seller.GetItemCount(Carp), seller.Gold).ShouldBe((16, 1111L));
    }

    [Fact]
    public async Task 두_명이_같은_수량을_동시에_사면_있는_만큼만_팔린다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        var rival  = Login(RivalUid);
        RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);

        buyer.TryBuyMarket(Carp, 7, 30, Now);
        rival.TryBuyMarket(Carp, 7, 30, Now);
        await _logic.Pump(() => Sent<S_MarketBuyResponse>(BuyerUid).Count > 0 && Sent<S_MarketBuyResponse>(RivalUid).Count > 0);

        var results = new[] { Sent<S_MarketBuyResponse>(BuyerUid).Single().Result, Sent<S_MarketBuyResponse>(RivalUid).Single().Result };
        results.OrderBy(r => r).ShouldBe(new[] { EResultCode.Ok, EResultCode.MarketNotEnough });
        (buyer.Gold + rival.Gold).ShouldBe(1790);   // 2000 − 7 × 30
    }

    [Fact]
    public async Task 전일_평균가는_다음_날_목록에_나온다()
    {
        var seller = Login(SellerUid);
        var buyer  = Login(BuyerUid);
        RegisterItem(seller, 10, 30);
        await _relay.FlushOutboxAsync(Now);
        await BuyMarket(buyer, 4, 30);
        await _relay.FlushOutboxAsync(Now);

        _auctionTime.Advance(TimeSpan.FromDays(1));

        (await MarketItem(buyer)).YesterdayAvgPrice.ShouldBe(30);
    }
}

