using MikaNetwork.Server;

namespace WSGameServer;

/// <summary>
/// 로직 스레드와 경매장 사이의 다리. gRPC 호출은 로직 스레드 밖에서 돌고, 결과는 <see cref="ILogicExecutor.Post"/>로 돌아온다 —
/// 로직 스레드가 응답을 기다리면 모든 유저가 함께 멈춘다. DB 큐(<see cref="DBManager"/>)와 같은 모양이다.
/// </summary>
public sealed class AuctionService
{
    /// <summary>운영 인스턴스. 기동 때 <see cref="Configure"/>로 채운다 — 그 전에는 모든 호출이 "경매장 없음"으로 돌아온다.</summary>
    public static AuctionService Current { get; private set; } = new(null, null);

    private readonly IAuctionClient? _client;
    private readonly ILogicExecutor? _logic;

    public AuctionService(IAuctionClient? client, ILogicExecutor? logic)
    {
        _client = client;
        _logic  = logic;
    }

    public static void Configure(AuctionService service) => Current = service;

    public bool IsAvailable => _client is not null && _logic is not null;

    /// <summary>접속 중인 유저를 계정 Uid로 찾는다(판매자 알림). 테스트는 갈아 끼운다.</summary>
    public Func<long, User?> FindOnlineUser { get; init; } = uid => UserManager.Instance.TryGetUserByUid(uid, out var user) ? user : null;

    /// <summary>릴레이를 깨운다 — outbox·이벤트가 생긴 직후 주기를 기다리지 않게.</summary>
    public Action KickRelay { get; set; } = () => { };

    /// <summary>
    /// 경매장을 부르고 결과를 로직 스레드로 돌려준다. 전송 실패면 null이다 — 부르는 쪽이 "경매장 없음"으로 응답한다.
    /// </summary>
    public void Call<T>(Func<IAuctionClient, Task<T>> call, Action<T?> onDone) where T : class
    {
        if (!IsAvailable)
        {
            onDone(null);
            return;
        }

        _ = RunAsync(call, onDone);
    }

    private async Task RunAsync<T>(Func<IAuctionClient, Task<T>> call, Action<T?> onDone) where T : class
    {
        T? result = null;
        try
        {
            result = await call(_client!);
        }
        catch (Exception e)
        {
            ServerLog.Warn("경매", $"경매장 호출 실패 — {e.GetType().Name}: {e.Message}");
        }

        _logic!.Post(() => onDone(result));
    }
}

/// <summary>
/// 구매 시도 ID. 경매장 <c>Reserve</c>·<c>Confirm</c>의 멱등 키라 재시작을 넘어 겹치면 안 된다 —
/// 시각(틱) 기반으로 늘 증가시킨다. 시계가 뒤로 가지 않는다는 전제다.
/// </summary>
public static class AuctionIds
{
    private static long _last;

    public static long NextPurchaseId()
    {
        while (true)
        {
            var last = Interlocked.Read(ref _last);
            var next = Math.Max(last + 1, DateTime.UtcNow.Ticks);
            if (Interlocked.CompareExchange(ref _last, next, last) == last)
            {
                return next;
            }
        }
    }
}
