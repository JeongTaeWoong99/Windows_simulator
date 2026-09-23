using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Proto = AuctionProtocol;

namespace AuctionServer.Tests;

/// <summary>
/// 운영과 같은 호스트 조립(<see cref="AuctionHost"/>)에 TestServer만 끼워 gRPC로 부른다.
/// proto ↔ 도메인 매핑(열거값·시각·옵션 목록)이 어긋나면 메인이 보낸 매물이 엉뚱하게 저장·검색된다.
/// </summary>
public class AuctionGrpcServiceTest : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    private readonly string           _path = Path.Combine(Path.GetTempPath(), $"auction-grpc-{Guid.NewGuid():N}.sqlite3");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Start));

    private WebApplication      _app    = null!;
    private GrpcChannel         _channel = null!;
    private Proto.Auction.AuctionClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Auction:DbPath"] = _path;
        builder.Services.AddSingleton<TimeProvider>(_time);
        AuctionHost.ConfigureServices(builder);

        _app = builder.Build();
        _app.Services.GetRequiredService<AuctionEngine>();
        AuctionHost.MapEndpoints(_app);
        await _app.StartAsync();

        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = _app.GetTestServer().CreateHandler() });
        _client  = new Proto.Auction.AuctionClient(_channel);
    }

    public async Task DisposeAsync()
    {
        _channel.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }
    }

    private static Proto.ListingSnapshot Equip(long id, long unitPrice, long seller = 100)
    {
        var snapshot = new Proto.ListingSnapshot
        {
            ListingId       = id,
            SellerId        = seller,
            Kind            = 2,
            Tid             = 5001,
            Category        = 3,
            Rarity          = 4,
            Count           = 1,
            EnchantGrade    = 5,
            UnitPrice       = unitPrice,
            ExpiresAtUnixMs = new DateTimeOffset(Start.AddHours(48)).ToUnixTimeMilliseconds(),
        };
        snapshot.Options.AddRange(new[] { 701, 702 });
        return snapshot;
    }

    [Fact]
    public async Task 등록한_스냅샷이_검색_응답에_그대로_실린다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250) });

        var reply = await _client.SearchAsync(new Proto.SearchRequest { Kind = 2, Category = 3 });

        var view = reply.Listings.Single();
        (view.ListingId, view.Tid, view.Rarity, view.EnchantGrade, view.UnitPrice, view.TotalPrice)
            .ShouldBe((1L, 5001, 4, 5, 250L, 250L));
        view.Options.ShouldBe(new[] { 701, 702 });
        view.ExpiresAtUnixMs.ShouldBe(new DateTimeOffset(Start.AddHours(48)).ToUnixTimeMilliseconds());
        view.State.ShouldBe(Proto.ListingState.Listed);
    }

    [Fact]
    public async Task 스냅샷이_없는_등록은_거부한다()
    {
        var reply = await _client.RegisterAsync(new Proto.RegisterRequest());

        reply.Result.ShouldBe(Proto.RegisterResult.Rejected);
    }

    [Fact]
    public async Task 예약_결과가_판매자와_총액을_싣는다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250, seller: 100) });

        var reply = await _client.ReserveAsync(new Proto.ReserveRequest { PurchaseId = 9, ListingId = 1, BuyerId = 200, ExpectedTotal = 250 });

        (reply.Result, reply.SellerId, reply.TotalPrice).ShouldBe((Proto.ReserveResult.Ok, 100L, 250L));
    }

    [Fact]
    public async Task 예약_실패_사유가_구분돼_돌아온다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250) });
        await _client.ReserveAsync(new Proto.ReserveRequest { PurchaseId = 9, ListingId = 1, BuyerId = 200, ExpectedTotal = 250 });

        var reply = await _client.ReserveAsync(new Proto.ReserveRequest { PurchaseId = 10, ListingId = 1, BuyerId = 300, ExpectedTotal = 250 });

        reply.Result.ShouldBe(Proto.ReserveResult.InProgress);
    }

    [Fact]
    public async Task 취소하면_취소_이벤트를_가져갈_수_있다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250, seller: 100) });

        var cancel = await _client.CancelAsync(new Proto.CancelRequest { ListingId = 1, SellerId = 100 });
        var events = await _client.FetchEventsAsync(new Proto.FetchEventsRequest { Max = 10 });

        cancel.Result.ShouldBe(Proto.CancelResult.Ok);
        events.Events.Select(e => (e.Kind, e.ListingId, e.SellerId)).ShouldBe(new[] { (Proto.EventKind.Cancelled, 1L, 100L) });
    }

    [Fact]
    public async Task 확인한_이벤트는_지워진다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250) });
        await _client.CancelAsync(new Proto.CancelRequest { ListingId = 1, SellerId = 100 });
        var events = await _client.FetchEventsAsync(new Proto.FetchEventsRequest { Max = 10 });

        var ack = new Proto.AckEventsRequest();
        ack.EventIds.AddRange(events.Events.Select(e => e.EventId));
        await _client.AckEventsAsync(ack);

        (await _client.FetchEventsAsync(new Proto.FetchEventsRequest { Max = 10 })).Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task 모르는_매물의_상태는_NotFound다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250) });
        var request = new Proto.ListingStatesRequest();
        request.ListingIds.AddRange(new long[] { 1, 404 });

        var reply = await _client.GetListingStatesAsync(request);

        reply.States.OrderBy(s => s.ListingId).Select(s => (s.ListingId, s.State))
            .ShouldBe(new[] { (1L, Proto.ListingState.Listed), (404L, Proto.ListingState.NotFound) });
    }

    [Fact]
    public async Task 내_매물에_예약_상태가_실린다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250, seller: 100) });
        await _client.ReserveAsync(new Proto.ReserveRequest { PurchaseId = 9, ListingId = 1, BuyerId = 200, ExpectedTotal = 250 });

        var reply = await _client.GetSellerListingsAsync(new Proto.SellerListingsRequest { SellerId = 100 });

        reply.Listings.Single().State.ShouldBe(Proto.ListingState.Reserved);
    }

    [Fact]
    public async Task 청소_서비스가_주기마다_만료를_처리한다()
    {
        await _client.RegisterAsync(new Proto.RegisterRequest { Listing = Equip(1, 250) });

        _time.Advance(TimeSpan.FromHours(48) + TimeSpan.FromSeconds(10));

        var events = await WaitForEvents();
        events.Single().Kind.ShouldBe(Proto.EventKind.Expired);
    }

    // 청소는 백그라운드 타이머라 가짜 시계를 넘긴 뒤 한 박자 늦게 돈다.
    private async Task<IReadOnlyList<Proto.AuctionEvent>> WaitForEvents()
    {
        for (var i = 0; i < 100; i++)
        {
            var reply = await _client.FetchEventsAsync(new Proto.FetchEventsRequest { Max = 10 });
            if (reply.Events.Count > 0)
            {
                return reply.Events;
            }

            await Task.Delay(20);
        }

        return Array.Empty<Proto.AuctionEvent>();
    }
}
