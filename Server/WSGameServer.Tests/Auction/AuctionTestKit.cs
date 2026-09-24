using System.Collections.Concurrent;
using Grpc.Core;
using MikaNetwork.Server;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 경매장 대역. 메서드마다 응답을 정해 둘 수 있고, 정하지 않은 호출은 "경매장에 닿지 않음"(RpcException)이다.
/// 받은 요청은 전부 기록한다.
/// </summary>
internal sealed class FakeAuctionClient : IAuctionClient
{
    public Func<Proto.RegisterRequest, Task<Proto.RegisterReply>>? Register { get; set; }
    public Func<Proto.ReserveRequest, Task<Proto.ReserveReply>>?   Reserve  { get; set; }
    public Func<Proto.ReserveQuantityRequest, Task<Proto.ReserveReply>>? ReserveQuantity { get; set; }
    public Func<Proto.MarketItemsRequest, Task<Proto.MarketItemsReply>>? MarketItems     { get; set; }
    public Func<Proto.PriceLadderRequest, Task<Proto.PriceLadderReply>>? PriceLadder     { get; set; }
    public Func<Proto.ConfirmRequest, Task>?                       Confirm  { get; set; }
    public Func<Proto.CancelRequest, Task<Proto.CancelReply>>?     Cancel   { get; set; }
    public Func<Proto.SearchRequest, Task<Proto.SearchReply>>?     Search   { get; set; }
    public Func<Proto.SellerListingsRequest, Task<Proto.SellerListingsReply>>? SellerListings { get; set; }
    public Func<Proto.FetchEventsRequest, Task<Proto.FetchEventsReply>>?       FetchEvents    { get; set; }
    public Func<Proto.AckEventsRequest, Task>?                                 AckEvents      { get; set; }
    public Func<Proto.ListingStatesRequest, Task<Proto.ListingStatesReply>>?   ListingStates  { get; set; }

    public List<object> Requests { get; } = new();

    public List<T> RequestsOf<T>() => Requests.OfType<T>().ToList();

    private Task<TReply> Call<TRequest, TReply>(Func<TRequest, Task<TReply>>? handler, TRequest request) where TRequest : notnull
    {
        Requests.Add(request);
        if (handler is null)
        {
            return Task.FromException<TReply>(new RpcException(new Status(StatusCode.Unavailable, "경매장 없음")));
        }

        return handler(request);
    }

    private Task Call<TRequest>(Func<TRequest, Task>? handler, TRequest request) where TRequest : notnull
    {
        Requests.Add(request);
        if (handler is null)
        {
            return Task.FromException(new RpcException(new Status(StatusCode.Unavailable, "경매장 없음")));
        }

        return handler(request);
    }

    public Task<Proto.RegisterReply> RegisterAsync(Proto.RegisterRequest request) => Call(Register, request);
    public Task<Proto.ReserveReply> ReserveAsync(Proto.ReserveRequest request) => Call(Reserve, request);
    public Task<Proto.ReserveReply> ReserveQuantityAsync(Proto.ReserveQuantityRequest request) => Call(ReserveQuantity, request);
    public Task<Proto.MarketItemsReply> GetMarketItemsAsync(Proto.MarketItemsRequest request) => Call(MarketItems, request);
    public Task<Proto.PriceLadderReply> GetPriceLadderAsync(Proto.PriceLadderRequest request) => Call(PriceLadder, request);
    public Task ConfirmAsync(Proto.ConfirmRequest request) => Call(Confirm, request);
    public Task<Proto.CancelReply> CancelAsync(Proto.CancelRequest request) => Call(Cancel, request);
    public Task<Proto.SearchReply> SearchAsync(Proto.SearchRequest request) => Call(Search, request);
    public Task<Proto.SellerListingsReply> GetSellerListingsAsync(Proto.SellerListingsRequest request) => Call(SellerListings, request);
    public Task<Proto.FetchEventsReply> FetchEventsAsync(Proto.FetchEventsRequest request) => Call(FetchEvents, request);
    public Task AckEventsAsync(Proto.AckEventsRequest request) => Call(AckEvents, request);
    public Task<Proto.ListingStatesReply> GetListingStatesAsync(Proto.ListingStatesRequest request) => Call(ListingStates, request);
}

/// <summary>유저 무관 DB 작업을 테스트의 <c>:memory:</c> 커넥션에 대고 돌린다.</summary>
internal sealed class SqliteRunner(SqliteFixture db) : IDbRunner
{
    public Task<T> RunAsync<T>(Func<DbConnection, Task<T>> body) => body(new DbConnection(db.Connection));
}

/// <summary>
/// Repository를 그 자리에서 <c>:memory:</c> DB에 실행하고, 결과를 로직 실행기로 돌려준다(운영 DBManager와 같은 순서).
/// 흐름 전체를 실제 SQL로 볼 때 쓴다.
/// </summary>
internal sealed class SqliteDBQueue(SqliteFixture db, ILogicExecutor logic) : IDBQueue
{
    public void Post<TRepository>(TRepository repository) where TRepository : IRepository
    {
        try
        {
            repository.ExecuteAsync(new DbConnection(db.Connection)).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            logic.Post(() => repository.OnFailed(e));
            return;
        }

        logic.Post(repository.Apply);
    }
}

/// <summary>
/// 여러 스레드에서 Post해도 되는 가짜 로직 실행기. 작업은 <see cref="Pump"/>를 부른 스레드에서만 돈다 —
/// 운영의 "로직 스레드 하나"와 같다. gRPC 응답처럼 다른 스레드에서 돌아오는 결과를 기다릴 때 쓴다.
/// </summary>
internal sealed class PumpedLogicExecutor : ILogicExecutor
{
    private readonly ConcurrentQueue<Action> _jobs = new();

    public void Start() { }
    public void Stop() { }

    public void Post(Action job) => _jobs.Enqueue(job);

    /// <summary>쌓인 작업을 비운다. 실행 중 새로 쌓인 것도 이어서 돈다.</summary>
    public void Drain()
    {
        while (_jobs.TryDequeue(out var job))
        {
            job();
        }
    }

    /// <summary>조건이 설 때까지 작업을 비우며 기다린다. 5초 안에 안 서면 실패다.</summary>
    public async Task Pump(Func<bool> until)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Drain();
            if (until())
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("기다리던 결과가 오지 않았다");
            }

            await Task.Delay(5);
        }
    }
}
