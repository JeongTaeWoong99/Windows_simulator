using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AuctionServer;

/// <summary>
/// 경매장 DB. <b>엔진의 쓰기 소비자 하나만 부른다</b> — 커넥션 하나를 계속 쥐고 동시 접근을 가정하지 않는다.
/// 상태 전이는 전부 조건부 UPDATE다. "읽고 확인한 뒤 쓰기"로 짜면 그 사이에 다른 요청이 끼어든다.
/// </summary>
public sealed class AuctionStore : IDisposable
{
    // SQLite datetime('now')와 같은 순서로 정렬되는 UTC 문자열 — 문자열 비교가 곧 시각 비교다.
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly SqliteConnection _conn;

    private AuctionStore(SqliteConnection conn)
    {
        _conn = conn;
    }

    /// <summary>
    /// 파일을 열고 스키마를 만든다. 거래 원천이라 <c>synchronous=FULL</c>이다 —
    /// NORMAL은 전원 장애 때 ack를 보낸 커밋을 잃을 수 있고, 그러면 메인과 어긋난다.
    /// </summary>
    public static AuctionStore Open(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode       = SqliteOpenMode.ReadWriteCreate,
            Pooling    = false,
        }.ToString());
        conn.Open();

        conn.Execute("PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL; PRAGMA busy_timeout = 5000;");
        conn.Execute(Schema);

        return new AuctionStore(conn);
    }

    private const string Schema = @"
        CREATE TABLE IF NOT EXISTS t_listing (
            listing_id           INTEGER PRIMARY KEY,           -- 매물 ID = 메인 t_auction_trade.trade_id. 등록 멱등 키
            seller_id            INTEGER NOT NULL,              -- 판매자 (메인 t_user.user_id)
            kind                 INTEGER NOT NULL,              -- 1=자원 2=장비
            tid                  INTEGER NOT NULL,              -- ItemTID 또는 EquipTID
            category             INTEGER NOT NULL,              -- 자원은 ItemType, 장비는 EquipKind
            rarity               INTEGER NOT NULL,              -- GlobalRarity
            count                INTEGER NOT NULL,              -- 수량 (장비는 1). 매물은 통째로만 팔린다
            enchant_grade        INTEGER NOT NULL DEFAULT 0,    -- 인챈트 등급 (0=없음)
            options              TEXT    NOT NULL DEFAULT '[]', -- 인챈트 옵션 TID JSON [TID, ...]
            unit_price           INTEGER NOT NULL,              -- 단가. 총액 = 단가 × 수량
            state                INTEGER NOT NULL DEFAULT 1,    -- 1 Listed · 2 Reserved · 3 Sold · 4 Cancelled · 5 Expired
            reserved_purchase_id INTEGER NOT NULL DEFAULT 0,    -- 예약한 구매 (t_purchase.purchase_id). 0=없음
            reserved_at          TEXT,                          -- 예약 시각 (UTC). 타임아웃 기준
            expires_at           TEXT    NOT NULL,              -- 만료 시각 (UTC)
            created_at           TEXT    NOT NULL,              -- 등록 시각 (UTC)
            closed_at            TEXT                           -- 종착 상태(Sold·Cancelled·Expired)가 된 시각 (UTC)
        ) STRICT;
        CREATE INDEX IF NOT EXISTS idx_listing_state_expires ON t_listing (state, expires_at);  -- 재시작 적재·만료 청소
        CREATE INDEX IF NOT EXISTS idx_listing_seller ON t_listing (seller_id, state);          -- 내 매물

        CREATE TABLE IF NOT EXISTS t_purchase (
            purchase_id  INTEGER PRIMARY KEY,           -- 구매 시도 ID (메인 발급). Reserve·Confirm 멱등 키
            listing_id   INTEGER NOT NULL,              -- 대상 매물 (t_listing.listing_id)
            buyer_id     INTEGER NOT NULL,              -- 구매자 (메인 t_user.user_id)
            total_price  INTEGER NOT NULL,              -- 예약 순간의 총액
            status       INTEGER NOT NULL DEFAULT 0,    -- 0 예약 · 1 확정 · 2 실패. 0이 아니면 Confirm을 다시 반영하지 않는다
            created_at   TEXT    NOT NULL,              -- 예약 시각 (UTC)
            confirmed_at TEXT                           -- Confirm을 받은 시각 (UTC)
        ) STRICT;

        CREATE TABLE IF NOT EXISTS t_event (
            event_id   INTEGER PRIMARY KEY AUTOINCREMENT, -- 이벤트 ID. AUTOINCREMENT — 확인 후 지워도 번호를 다시 쓰지 않는다
            kind       INTEGER NOT NULL,                  -- 1 취소 · 2 만료 — 메인이 잠금을 푼다
            listing_id INTEGER NOT NULL,                  -- 매물 (t_listing.listing_id)
            seller_id  INTEGER NOT NULL,                  -- 판매자 — 메인이 반환 우편을 보낼 곳
            created_at TEXT    NOT NULL                   -- 생긴 시각 (UTC)
        ) STRICT;";

    private static string ToDb(DateTime utc) => utc.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static DateTime FromDb(string text)
        => DateTime.SpecifyKind(DateTime.ParseExact(text, TimeFormat, CultureInfo.InvariantCulture), DateTimeKind.Utc);

    private sealed record ListingRow
    {
        public long   listing_id           { get; init; }
        public long   seller_id            { get; init; }
        public long   kind                 { get; init; }
        public long   tid                  { get; init; }
        public long   category             { get; init; }
        public long   rarity               { get; init; }
        public long   count                { get; init; }
        public long   enchant_grade        { get; init; }
        public string options              { get; init; } = "[]";
        public long   unit_price           { get; init; }
        public long   state                { get; init; }
        public long   reserved_purchase_id { get; init; }
        public string expires_at           { get; init; } = "";

        public Listing ToListing()
        {
            return new Listing(
                listing_id, seller_id, (ListingKind)kind, (int)tid, (int)category, (int)rarity, (int)count,
                (int)enchant_grade, JsonSerializer.Deserialize<int[]>(options) ?? Array.Empty<int>(),
                unit_price, FromDb(expires_at));
        }
    }

    private sealed record PurchaseRow
    {
        public long purchase_id { get; init; }
        public long listing_id  { get; init; }
        public long buyer_id    { get; init; }
        public long total_price { get; init; }
        public long status      { get; init; }
    }

    private const string ListingColumns =
        "listing_id, seller_id, kind, tid, category, rarity, count, enchant_grade, options, unit_price, state, reserved_purchase_id, expires_at";

    private ListingRow? FindListing(long listingId, SqliteTransaction? tx = null)
    {
        return _conn.QueryFirstOrDefault<ListingRow>(
            $"SELECT {ListingColumns} FROM t_listing WHERE listing_id = @listingId;", new { listingId }, tx);
    }

    /// <summary>판매 중(Listed)인 매물 전부 — 재시작 때 인덱스를 다시 만든다.</summary>
    public List<Listing> LoadListed()
    {
        return _conn.Query<ListingRow>($"SELECT {ListingColumns} FROM t_listing WHERE state = 1;")
            .Select(r => r.ToListing()).ToList();
    }

    /// <summary>새 매물이면 넣고 true. 같은 ID가 이미 있으면(재전송) 아무것도 안 하고 false.</summary>
    public bool InsertListing(Listing l, DateTime now)
    {
        var inserted = _conn.Execute(
            @"INSERT INTO t_listing (listing_id, seller_id, kind, tid, category, rarity, count, enchant_grade, options,
                                     unit_price, state, expires_at, created_at)
              VALUES (@ListingId, @SellerId, @kind, @Tid, @Category, @Rarity, @Count, @EnchantGrade, @options,
                      @UnitPrice, 1, @expiresAt, @createdAt)
              ON CONFLICT (listing_id) DO NOTHING;",
            new
            {
                l.ListingId, l.SellerId, kind = (int)l.Kind, l.Tid, l.Category, l.Rarity, l.Count, l.EnchantGrade,
                options = JsonSerializer.Serialize(l.Options), l.UnitPrice,
                expiresAt = ToDb(l.ExpiresAt), createdAt = ToDb(now),
            });

        return inserted == 1;
    }

    /// <summary>
    /// Listed → Reserved. 같은 구매 ID의 재시도면 처음 결과를 돌려준다.
    /// 성공이면 <paramref name="reserved"/>에 예약한 매물이 담긴다(인덱스에서 뺄 대상).
    /// </summary>
    public ReserveOutcome Reserve(long purchaseId, long listingId, long buyerId, long expectedTotal, DateTime now, out Listing? reserved)
    {
        reserved = null;
        using var tx = _conn.BeginTransaction();

        var previous = _conn.QueryFirstOrDefault<PurchaseRow>(
            "SELECT purchase_id, listing_id, buyer_id, total_price, status FROM t_purchase WHERE purchase_id = @purchaseId;",
            new { purchaseId }, tx);
        if (previous is not null)
        {
            // 같은 ID로 다른 매물·구매자가 왔다 — 재시도가 아니라 ID 충돌이다. Ok를 주면 메인이 예약 없는 매물을 정산한다.
            if (previous.listing_id != listingId || previous.buyer_id != buyerId)
            {
                return new ReserveOutcome(ReserveResult.InProgress);
            }

            var seller = FindListing(previous.listing_id, tx)?.seller_id ?? 0;
            return new ReserveOutcome(ReserveResult.Ok, seller, previous.total_price);
        }

        var row = FindListing(listingId, tx);
        var rejected = Judge(row, buyerId, expectedTotal, now);
        if (rejected is not null)
        {
            return new ReserveOutcome(rejected.Value);
        }

        var listing = row!.ToListing();
        var changed = _conn.Execute(
            @"UPDATE t_listing SET state = 2, reserved_purchase_id = @purchaseId, reserved_at = @now
              WHERE listing_id = @listingId AND state = 1;",
            new { purchaseId, listingId, now = ToDb(now) }, tx);
        if (changed == 0)
        {
            return new ReserveOutcome(ReserveResult.InProgress);
        }

        _conn.Execute(
            @"INSERT INTO t_purchase (purchase_id, listing_id, buyer_id, total_price, status, created_at)
              VALUES (@purchaseId, @listingId, @buyerId, @total, 0, @now);",
            new { purchaseId, listingId, buyerId, total = listing.TotalPrice, now = ToDb(now) }, tx);

        tx.Commit();
        reserved = listing;
        return new ReserveOutcome(ReserveResult.Ok, listing.SellerId, listing.TotalPrice);
    }

    private static ReserveResult? Judge(ListingRow? row, long buyerId, long expectedTotal, DateTime now)
    {
        if (row is null)
        {
            return ReserveResult.NotFound;
        }

        switch ((ListingState)row.state)
        {
            case ListingState.Reserved:
                return ReserveResult.InProgress;
            case ListingState.Sold:
                return ReserveResult.Sold;
            case ListingState.Cancelled:
            case ListingState.Expired:
                return ReserveResult.Closed;
        }

        if (FromDb(row.expires_at) <= now)
        {
            return ReserveResult.Closed;
        }

        if (row.seller_id == buyerId)
        {
            return ReserveResult.OwnListing;
        }

        if (row.unit_price * row.count != expectedTotal)
        {
            return ReserveResult.PriceChanged;
        }

        return null;
    }

    /// <summary>
    /// 메인 정산 결과를 반영한다. 성공이면 어느 상태든 Sold(메인이 이미 판 사실이다),
    /// 실패면 이 구매가 쥔 예약만 Listed로 푼다. 같은 구매는 한 번만 반영한다.
    /// </summary>
    /// <returns>인덱스에 다시 넣을 매물(실패로 풀린 경우) 또는 뺄 매물 ID.</returns>
    public (Listing? Relisted, long? Removed) Confirm(long purchaseId, bool success, DateTime now)
    {
        using var tx = _conn.BeginTransaction();

        var purchase = _conn.QueryFirstOrDefault<PurchaseRow>(
            "SELECT purchase_id, listing_id, buyer_id, total_price, status FROM t_purchase WHERE purchase_id = @purchaseId;",
            new { purchaseId }, tx);
        if (purchase is null || purchase.status != 0)
        {
            return (null, null);
        }

        _conn.Execute(
            "UPDATE t_purchase SET status = @status, confirmed_at = @now WHERE purchase_id = @purchaseId;",
            new { purchaseId, status = success ? 1 : 2, now = ToDb(now) }, tx);

        if (success)
        {
            _conn.Execute(
                @"UPDATE t_listing SET state = 3, reserved_purchase_id = @purchaseId, closed_at = @now
                  WHERE listing_id = @listingId AND state <> 3;",
                new { purchaseId, listingId = purchase.listing_id, now = ToDb(now) }, tx);
            tx.Commit();
            return (null, purchase.listing_id);
        }

        var released = _conn.Execute(
            @"UPDATE t_listing SET state = 1, reserved_purchase_id = 0, reserved_at = NULL
              WHERE listing_id = @listingId AND state = 2 AND reserved_purchase_id = @purchaseId;",
            new { purchaseId, listingId = purchase.listing_id }, tx);
        var relisted = released == 1 ? FindListing(purchase.listing_id, tx)!.ToListing() : null;

        tx.Commit();
        return (relisted, null);
    }

    /// <summary>판매자 취소. Listed → Cancelled + 이벤트를 한 트랜잭션으로 쓴다 — 이벤트가 빠지면 아이템이 영원히 잠긴다.</summary>
    public CancelResult Cancel(long listingId, long sellerId, DateTime now)
    {
        using var tx = _conn.BeginTransaction();

        var row = FindListing(listingId, tx);
        if (row is null)
        {
            return CancelResult.NotFound;
        }

        if (row.seller_id != sellerId)
        {
            return CancelResult.NotOwner;
        }

        switch ((ListingState)row.state)
        {
            case ListingState.Cancelled:
                return CancelResult.Ok;
            case ListingState.Reserved:
                return CancelResult.InProgress;
            case ListingState.Sold:
            case ListingState.Expired:
                return CancelResult.Closed;
        }

        var changed = _conn.Execute(
            "UPDATE t_listing SET state = 4, closed_at = @now WHERE listing_id = @listingId AND state = 1;",
            new { listingId, now = ToDb(now) }, tx);
        if (changed == 0)
        {
            return CancelResult.InProgress;
        }

        InsertEvent(AuctionEventKind.Cancelled, listingId, sellerId, now, tx);
        tx.Commit();
        return CancelResult.Ok;
    }

    /// <summary>만료 시각이 지난 Listed를 Expired로 바꾸고 이벤트를 남긴다. 만료된 매물 ID를 돌려준다.</summary>
    public List<long> ExpireDue(DateTime now)
    {
        using var tx = _conn.BeginTransaction();

        var due = _conn.Query<ListingRow>(
            $"SELECT {ListingColumns} FROM t_listing WHERE state = 1 AND expires_at <= @now;", new { now = ToDb(now) }, tx).ToList();

        foreach (var row in due)
        {
            _conn.Execute(
                "UPDATE t_listing SET state = 5, closed_at = @now WHERE listing_id = @id AND state = 1;",
                new { id = row.listing_id, now = ToDb(now) }, tx);
            InsertEvent(AuctionEventKind.Expired, row.listing_id, row.seller_id, now, tx);
        }

        tx.Commit();
        return due.Select(r => r.listing_id).ToList();
    }

    /// <summary>예약 시각이 <paramref name="cutoff"/> 이전인 Reserved를 Listed로 푼다. 풀린 매물을 돌려준다.</summary>
    public List<Listing> ReleaseStaleReservations(DateTime cutoff)
    {
        using var tx = _conn.BeginTransaction();

        var stale = _conn.Query<ListingRow>(
            $"SELECT {ListingColumns} FROM t_listing WHERE state = 2 AND reserved_at <= @cutoff;",
            new { cutoff = ToDb(cutoff) }, tx).ToList();

        _conn.Execute(
            @"UPDATE t_listing SET state = 1, reserved_purchase_id = 0, reserved_at = NULL
              WHERE state = 2 AND reserved_at <= @cutoff;",
            new { cutoff = ToDb(cutoff) }, tx);

        tx.Commit();
        return stale.Select(r => r.ToListing()).ToList();
    }

    private void InsertEvent(AuctionEventKind kind, long listingId, long sellerId, DateTime now, SqliteTransaction tx)
    {
        _conn.Execute(
            "INSERT INTO t_event (kind, listing_id, seller_id, created_at) VALUES (@kind, @listingId, @sellerId, @now);",
            new { kind = (int)kind, listingId, sellerId, now = ToDb(now) }, tx);
    }

    private sealed record EventRow
    {
        public long event_id   { get; init; }
        public long kind       { get; init; }
        public long listing_id { get; init; }
        public long seller_id  { get; init; }
    }

    public List<AuctionEvent> FetchEvents(int max)
    {
        return _conn.Query<EventRow>(
                "SELECT event_id, kind, listing_id, seller_id FROM t_event ORDER BY event_id LIMIT @max;", new { max })
            .Select(r => new AuctionEvent(r.event_id, (AuctionEventKind)r.kind, r.listing_id, r.seller_id))
            .ToList();
    }

    public void AckEvents(IReadOnlyCollection<long> eventIds)
    {
        _conn.Execute("DELETE FROM t_event WHERE event_id IN @eventIds;", new { eventIds });
    }

    /// <summary>매물별 상태. 모르는 매물은 null이다.</summary>
    public Dictionary<long, ListingState?> GetStates(IReadOnlyCollection<long> listingIds)
    {
        var found = _conn.Query<(long Id, long State)>(
                "SELECT listing_id, state FROM t_listing WHERE listing_id IN @listingIds;", new { listingIds })
            .ToDictionary(r => r.Id, r => (ListingState?)(ListingState)r.State);

        return listingIds.Distinct().ToDictionary(id => id, id => found.GetValueOrDefault(id));
    }

    /// <summary>한 판매자의 진행 중 매물(Listed·Reserved), 등록 순.</summary>
    public List<ListingWithState> GetSellerListings(long sellerId)
    {
        return _conn.Query<ListingRow>(
                $"SELECT {ListingColumns} FROM t_listing WHERE seller_id = @sellerId AND state IN (1, 2) ORDER BY listing_id;",
                new { sellerId })
            .Select(r => new ListingWithState(r.ToListing(), (ListingState)r.state))
            .ToList();
    }

    public void Dispose() => _conn.Dispose();
}
