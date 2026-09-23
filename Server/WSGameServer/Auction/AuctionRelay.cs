using System.Text.Json;
using Grpc.Core;
using MikaNetwork.Server;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 메인 → 경매장 전달을 맡는 백그라운드 일꾼. 세 가지를 한다 → Server/docs/경매장.md 4·6장.
/// ① outbox 전송 — 상태 변경과 같은 트랜잭션에 적힌 메시지만 보낸다(커밋된 것만 나간다).
/// ② 이벤트 반환 — 경매장의 취소·만료 이벤트를 가져가 반환 우편을 보내고 확인한다.
/// ③ 대사 — 오래 잠긴 거래를 경매장에 물어, 경매장이 잃었거나 끝난 것을 돌려준다.
/// 로직 스레드가 아니라 자기 스레드에서 돈다. 유저에게 알릴 것만 로직 스레드로 넘긴다.
/// </summary>
public sealed class AuctionRelay
{
    public const int BatchSize = 100;

    /// <summary>대사 대상이 되는 나이 — 이보다 어린 거래는 이벤트가 아직 오는 중일 수 있다.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan PollInterval      = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(5);

    /// <summary>경매장에 닿지 않을 때의 재시도 간격 — 1초마다 두드리면 장애 동안 로그만 쌓인다.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private readonly IAuctionClient    _client;
    private readonly IDbRunner         _db;
    private readonly ILogicExecutor    _logic;
    private readonly Func<long, User?> _findOnlineUser;

    private readonly SemaphoreSlim _kick = new(0, 1);

    // 대사 커서. 한 바퀴에 BatchSize만 보고 다음 바퀴는 그 뒤부터 — 끝까지 가면 처음으로 돌아간다.
    private long _reconcileAfter;

    public AuctionRelay(IAuctionClient client, IDbRunner db, ILogicExecutor logic, Func<long, User?> findOnlineUser)
    {
        _client         = client;
        _db             = db;
        _logic          = logic;
        _findOnlineUser = findOnlineUser;
    }

    /// <summary>다음 주기를 기다리지 않고 바로 돌게 한다. 여러 번 불러도 한 번만 깨운다.</summary>
    public void Kick()
    {
        if (_kick.CurrentCount == 0)
        {
            try
            {
                _kick.Release();
            }
            catch (SemaphoreFullException)
            {
                // 동시에 두 번 깨웠다 — 이미 깨어 있으니 그대로 둔다.
            }
        }
    }

    public async Task RunAsync(Func<DateTime> clock, CancellationToken stop)
    {
        var nextReconcile = clock() + ReconcileInterval;

        while (!stop.IsCancellationRequested)
        {
            var reachable = false;
            try
            {
                reachable = await RunOnceAsync(clock());

                if (reachable && clock() >= nextReconcile)
                {
                    await ReconcileAsync(clock());
                    nextReconcile = clock() + ReconcileInterval;
                }
            }
            catch (RpcException e)
            {
                // 경매장 장애 — 한 바퀴를 건너뛰고 다시 한다. outbox·이벤트는 지워지지 않았다.
                ServerLog.Warn("경매", $"경매장에 닿지 않는다 — {RetryInterval.TotalSeconds:F0}초 뒤 재시도: {e.StatusCode}");
            }
            catch (Exception e)
            {
                ServerLog.Error("경매", "릴레이 한 바퀴 실패", e);
            }

            try
            {
                await _kick.WaitAsync(reachable ? PollInterval : RetryInterval, stop);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <returns>경매장에 닿았으면 true. outbox를 다 못 보냈으면 이벤트도 건너뛴다 — 어차피 닿지 않는다.</returns>
    public async Task<bool> RunOnceAsync(DateTime now)
    {
        if (!await FlushOutboxAsync(now))
        {
            return false;
        }

        await DrainEventsAsync(now);
        return true;
    }

    /// <summary>
    /// 미전송 메시지를 순서대로 보낸다. 전송에 실패하면 거기서 멈춘다 — 등록보다 확정이 먼저 가면 안 된다.
    /// </summary>
    /// <returns>전부 보냈으면 true.</returns>
    public async Task<bool> FlushOutboxAsync(DateTime now)
    {
        var rows = await _db.RunAsync(conn => AuctionDb.LoadUnsentAsync(conn, BatchSize));

        foreach (var row in rows)
        {
            try
            {
                await SendAsync(row, now);
            }
            catch (Exception e)
            {
                var reason = e is RpcException rpc ? rpc.StatusCode.ToString() : e.Message;
                ServerLog.Warn("경매", $"outbox 전송 실패 — 다음 바퀴에 재시도. outbox {row.outbox_id} 거래 {row.trade_id}: {reason}");
                return false;
            }

            await _db.RunAsync(async conn =>
            {
                await AuctionDb.MarkSentAsync(conn, row.outbox_id, now);
                return true;
            });
        }

        return true;
    }

    private async Task SendAsync(AuctionOutboxRow row, DateTime now)
    {
        if (row.kind == (int)AuctionOutboxKind.Confirm)
        {
            var confirm = JsonSerializer.Deserialize<AuctionConfirmMessage>(row.payload)!;
            await _client.ConfirmAsync(new Proto.ConfirmRequest { PurchaseId = confirm.PurchaseId, Success = confirm.Success });
            return;
        }

        var register = JsonSerializer.Deserialize<AuctionRegisterMessage>(row.payload)!;
        var reply    = await _client.RegisterAsync(new Proto.RegisterRequest { Listing = ToSnapshot(register) });
        if (reply.Result == Proto.RegisterResult.Rejected)
        {
            ServerLog.Warn("경매", $"경매장이 등록을 거부 — 반환한다. 거래 {row.trade_id}");
            await ReturnAsync(row.trade_id, AuctionReturnReason.Rejected, now);
        }
    }

    private static Proto.ListingSnapshot ToSnapshot(AuctionRegisterMessage m)
    {
        var snapshot = new Proto.ListingSnapshot
        {
            ListingId       = m.ListingId,
            SellerId        = m.SellerId,
            Kind            = (int)m.Item.Kind,
            Tid             = m.Item.Tid,
            Category        = m.Item.Category,
            Rarity          = m.Item.Rarity,
            Count           = m.Item.Count,
            EnchantGrade    = m.Item.EnchantGrade,
            UnitPrice       = m.UnitPrice,
            ExpiresAtUnixMs = m.ExpiresAtUnixMs,
        };
        snapshot.Options.AddRange(m.Item.Options);
        return snapshot;
    }

    /// <summary>취소·만료 이벤트를 반환 우편으로 바꾸고 확인한다. 반환이 실패하면 확인하지 않는다 — 다음 바퀴에 다시 온다.</summary>
    public async Task DrainEventsAsync(DateTime now)
    {
        var reply = await _client.FetchEventsAsync(new Proto.FetchEventsRequest { Max = BatchSize });
        if (reply.Events.Count == 0)
        {
            return;
        }

        var handled = new Proto.AckEventsRequest();
        foreach (var e in reply.Events)
        {
            var reason = e.Kind == Proto.EventKind.Cancelled ? AuctionReturnReason.Cancelled : AuctionReturnReason.Expired;
            await ReturnAsync(e.ListingId, reason, now);
            handled.EventIds.Add(e.EventId);
        }

        await _client.AckEventsAsync(handled);
    }

    /// <summary>
    /// 오래 Listed인 거래를 경매장에 묻는다. 경매장에 없으면(유실) 등록비까지 돌려주고, 끝났으면 사유대로 돌려준다.
    /// 경매장이 Sold라는데 메인이 Listed면 있을 수 없는 상태라 손대지 않고 로그만 남긴다.
    /// </summary>
    public async Task ReconcileAsync(DateTime now)
    {
        var stale = await _db.RunAsync(conn => AuctionDb.FindStaleListedAsync(conn, now - StaleAfter, BatchSize, _reconcileAfter));
        _reconcileAfter = stale.Count < BatchSize ? 0 : stale[^1];
        if (stale.Count == 0)
        {
            return;
        }

        var request = new Proto.ListingStatesRequest();
        request.ListingIds.AddRange(stale);
        var reply = await _client.GetListingStatesAsync(request);

        foreach (var entry in reply.States)
        {
            switch (entry.State)
            {
                case Proto.ListingState.NotFound:
                    ServerLog.Warn("경매", $"대사 — 경매장에 없는 거래를 반환한다. 거래 {entry.ListingId}");
                    await ReturnAsync(entry.ListingId, AuctionReturnReason.Lost, now);
                    break;
                case Proto.ListingState.Cancelled:
                    await ReturnAsync(entry.ListingId, AuctionReturnReason.Cancelled, now);
                    break;
                case Proto.ListingState.Expired:
                    await ReturnAsync(entry.ListingId, AuctionReturnReason.Expired, now);
                    break;
                case Proto.ListingState.Sold:
                    ServerLog.Error("경매", $"대사 — 경매장은 판매됐다는데 메인은 판매 중이다. 손대지 않는다. 거래 {entry.ListingId}");
                    break;
            }
        }
    }

    private async Task ReturnAsync(long tradeId, AuctionReturnReason reason, DateTime now)
    {
        var result = await _db.RunAsync(conn => AuctionDb.ReturnAsync(conn, tradeId, reason, now));
        if (result is null)
        {
            return;
        }

        ServerLog.Info("경매", $"반환 {reason} 거래 {tradeId} → 판매자 {result.SellerId} 우편 {result.Mail.mail_id}");
        _logic.Post(() => _findOnlineUser(result.SellerId)?.OnAuctionClosed(result.Mail));
    }
}
