using System.Threading.Channels;

namespace AuctionServer;

/// <summary>
/// 경매장 본체. <b>쓰기는 채널 하나·소비자 하나로 줄 세운다</b> — 명령마다 SQLite 커밋 → 인덱스 반영 순서가 고정된다.
/// 둘 이상이 동시에 쓰면 DB 커밋 순서와 인덱스 반영 순서가 어긋나, DB는 Listed인데 검색에서 사라진 매물이 생긴다.
/// 검색은 채널을 타지 않는다 — 그 순간의 불변 인덱스를 락 없이 읽는다.
/// </summary>
public sealed class AuctionEngine : IAsyncDisposable
{
    private readonly AuctionStore   _store;
    private readonly TimeProvider   _time;
    private readonly AuctionOptions _options;

    private readonly Channel<Func<Task>> _writes = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions { SingleReader = true });

    private ListingIndex _index = ListingIndex.Empty;
    private Task?        _consumer;

    public AuctionEngine(AuctionStore store, TimeProvider time, AuctionOptions options)
    {
        _store   = store;
        _time    = time;
        _options = options;
    }

    public ListingIndex Index => Volatile.Read(ref _index);

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>DB의 판매 중 매물로 인덱스를 만든 뒤 쓰기 소비자를 띄운다. 끝나기 전에는 요청을 받지 않는다.</summary>
    public void Start()
    {
        var index = ListingIndex.Empty;
        foreach (var listing in _store.LoadListed())
        {
            index = index.Add(listing);
        }

        Volatile.Write(ref _index, index);
        _consumer = Task.Run(ConsumeAsync);
    }

    private async Task ConsumeAsync()
    {
        await foreach (var command in _writes.Reader.ReadAllAsync())
        {
            await command();
        }
    }

    // 명령을 채널에 넣고 소비자가 실행한 결과를 기다린다. 예외도 호출자에게 그대로 돌아간다.
    private Task<T> Enqueue<T>(Func<T> body)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var accepted = _writes.Writer.TryWrite(() =>
        {
            try
            {
                done.SetResult(body());
            }
            catch (Exception e)
            {
                done.SetException(e);
            }

            return Task.CompletedTask;
        });

        if (!accepted)
        {
            done.SetException(new ObjectDisposedException(nameof(AuctionEngine)));
        }

        return done.Task;
    }

    private void Publish(ListingIndex index) => Volatile.Write(ref _index, index);

    public Task<RegisterResult> RegisterAsync(Listing listing)
    {
        return Enqueue(() =>
        {
            var now = Now;
            if (!IsValid(listing, now))
            {
                return RegisterResult.Rejected;
            }

            // 이미 있으면(재전송) 인덱스도 건드리지 않는다 — 팔린 매물이 되살아나면 안 된다.
            if (_store.InsertListing(listing, now))
            {
                Publish(_index.Add(listing));
            }

            return RegisterResult.Ok;
        });
    }

    private static bool IsValid(Listing l, DateTime now)
    {
        if (l.ListingId <= 0 || l.SellerId <= 0 || l.Count <= 0 || l.UnitPrice <= 0)
        {
            return false;
        }

        if (l.Kind is not (ListingKind.Item or ListingKind.Equip))
        {
            return false;
        }

        if (l.Kind == ListingKind.Equip && l.Count != 1)
        {
            return false;
        }

        if (l.Options.Count > 3 || l.ExpiresAt <= now)
        {
            return false;
        }

        // 총액이 long을 넘으면 가격 비교가 뒤집힌다.
        return l.UnitPrice <= long.MaxValue / l.Count;
    }

    public Task<ReserveOutcome> ReserveAsync(long purchaseId, long listingId, long buyerId, long expectedTotal)
    {
        return Enqueue(() =>
        {
            var outcome = _store.Reserve(purchaseId, listingId, buyerId, expectedTotal, Now, out var reserved);
            if (reserved is not null)
            {
                Publish(_index.Remove(reserved.ListingId));
            }

            return outcome;
        });
    }

    public Task ConfirmAsync(long purchaseId, bool success)
    {
        return Enqueue(() =>
        {
            var (relisted, removed) = _store.Confirm(purchaseId, success, Now);
            if (relisted is not null)
            {
                Publish(_index.Add(relisted));
            }

            if (removed is { } id)
            {
                Publish(_index.Remove(id));
            }

            return true;
        });
    }

    public Task<CancelResult> CancelAsync(long listingId, long sellerId)
    {
        return Enqueue(() =>
        {
            var result = _store.Cancel(listingId, sellerId, Now);
            if (result == CancelResult.Ok)
            {
                Publish(_index.Remove(listingId));
            }

            return result;
        });
    }

    /// <summary>만료 → Expired(+이벤트), 오래된 예약 → Listed. 주기 타이머가 부른다.</summary>
    public Task SweepAsync()
    {
        return Enqueue(() =>
        {
            var now   = Now;
            var index = _index;

            foreach (var id in _store.ExpireDue(now))
            {
                index = index.Remove(id);
            }

            foreach (var listing in _store.ReleaseStaleReservations(now - _options.ReservationTimeout))
            {
                index = index.Add(listing);
            }

            Publish(index);
            return true;
        });
    }

    public Task<IReadOnlyList<AuctionEvent>> FetchEventsAsync(int max)
        => Enqueue<IReadOnlyList<AuctionEvent>>(() => _store.FetchEvents(Math.Clamp(max, 1, 1000)));

    public Task AckEventsAsync(IReadOnlyCollection<long> eventIds)
    {
        return Enqueue(() =>
        {
            _store.AckEvents(eventIds);
            return true;
        });
    }

    public Task<IReadOnlyDictionary<long, ListingState?>> GetStatesAsync(IReadOnlyCollection<long> listingIds)
        => Enqueue<IReadOnlyDictionary<long, ListingState?>>(() => _store.GetStates(listingIds));

    public Task<IReadOnlyList<ListingWithState>> GetSellerListingsAsync(long sellerId)
        => Enqueue<IReadOnlyList<ListingWithState>>(() => _store.GetSellerListings(sellerId));

    public SearchPage Search(SearchQuery query) => Index.Search(query, Now);

    public async ValueTask DisposeAsync()
    {
        _writes.Writer.TryComplete();
        if (_consumer is not null)
        {
            await _consumer;
        }

        _store.Dispose();
    }
}
