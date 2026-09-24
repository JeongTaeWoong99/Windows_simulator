using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AuctionServer;

/// <summary>
/// 경매장 DB. <b>엔진의 쓰기 소비자 하나만 부른다</b> — 커넥션 하나를 계속 쥐고 동시 접근을 가정하지 않는다.
/// 매물은 <b>남은 수량</b>을 들고, 예약은 <c>t_reservation</c>에 "구매 1건 → 매물 여러 개 × 수량"으로 잡힌다.
/// 판매 가능 수량 = 남은 수량 − 진행 중 예약(구매 status 0)의 합이다.
/// </summary>
public sealed class AuctionStore : IDisposable
{
    // 스키마 판. 바뀌면 옛 파일로 조용히 돌지 않고 멈춘다 — 컬럼이 어긋나면 예약 합계가 틀린다.
    private const int SchemaVersion = 2;

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

        var version  = conn.ExecuteScalar<long>("PRAGMA user_version;");
        var hasTable = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE name = 't_listing';") > 0;
        if (hasTable && version != SchemaVersion)
        {
            conn.Dispose();
            throw new InvalidOperationException($"경매장 DB 스키마 판이 다르다({version} ≠ {SchemaVersion}): {path}");
        }

        conn.Execute(Schema);
        conn.Execute($"PRAGMA user_version = {SchemaVersion};");

        return new AuctionStore(conn);
    }

    private const string Schema = @"
        CREATE TABLE IF NOT EXISTS t_listing (
            listing_id    INTEGER PRIMARY KEY,           -- 매물 ID = 메인 t_auction_trade.trade_id. 등록 멱등 키
            seller_id     INTEGER NOT NULL,              -- 판매자 (메인 t_user.user_id)
            kind          INTEGER NOT NULL,              -- 1=자원(거래소) 2=장비(경매장)
            tid           INTEGER NOT NULL,              -- ItemTID 또는 EquipTID
            category      INTEGER NOT NULL,              -- 자원은 ItemType, 장비는 EquipKind
            rarity        INTEGER NOT NULL,              -- GlobalRarity
            count         INTEGER NOT NULL,              -- 남은 수량(예약분 포함). 체결마다 줄고 0이 되면 Sold
            listed_count  INTEGER NOT NULL,              -- 등록 수량. 바뀌지 않는다
            enchant_grade INTEGER NOT NULL DEFAULT 0,    -- 인챈트 등급 (0=없음)
            options       TEXT    NOT NULL DEFAULT '[]', -- 인챈트 옵션 TID JSON [TID, ...]
            unit_price    INTEGER NOT NULL,              -- 단가
            state         INTEGER NOT NULL DEFAULT 1,    -- 1 Listed · 3 Sold · 4 Cancelled · 5 Expired (예약 중은 t_reservation이 말한다)
            expires_at    TEXT    NOT NULL,              -- 만료 시각 (UTC)
            created_at    TEXT    NOT NULL,              -- 등록 시각 (UTC)
            closed_at     TEXT                           -- 종착 상태가 된 시각 (UTC)
        ) STRICT;
        CREATE INDEX IF NOT EXISTS idx_listing_state_expires ON t_listing (state, expires_at);    -- 재시작 적재·만료 청소
        CREATE INDEX IF NOT EXISTS idx_listing_seller ON t_listing (seller_id, state);            -- 내 매물
        CREATE INDEX IF NOT EXISTS idx_listing_tid ON t_listing (kind, tid, state, unit_price);   -- 거래소 수량 예약(종류별 최저가순)

        CREATE TABLE IF NOT EXISTS t_purchase (
            purchase_id  INTEGER PRIMARY KEY,         -- 구매 시도 ID (메인 발급). Reserve·Confirm 멱등 키
            buyer_id     INTEGER NOT NULL,            -- 구매자 (메인 t_user.user_id)
            total_price  INTEGER NOT NULL,            -- 예약한 총액
            status       INTEGER NOT NULL DEFAULT 0,  -- 0 예약 · 1 확정 · 2 실패 · 3 타임아웃. 0만 수량을 쥔다
            created_at   TEXT    NOT NULL,            -- 예약 시각 (UTC). 타임아웃 기준
            confirmed_at TEXT                         -- Confirm을 받은 시각 (UTC)
        ) STRICT;
        CREATE INDEX IF NOT EXISTS idx_purchase_status ON t_purchase (status, created_at);        -- 예약 타임아웃 청소

        CREATE TABLE IF NOT EXISTS t_reservation (
            purchase_id INTEGER NOT NULL,             -- 구매 시도 (t_purchase.purchase_id)
            listing_id  INTEGER NOT NULL,             -- 매물 (t_listing.listing_id)
            quantity    INTEGER NOT NULL,             -- 이 매물에서 잡은 수량
            unit_price  INTEGER NOT NULL,             -- 잡은 순간의 단가
            PRIMARY KEY (purchase_id, listing_id)     -- 한 구매는 한 매물을 한 줄로만 잡는다
        ) STRICT;
        CREATE INDEX IF NOT EXISTS idx_reservation_listing ON t_reservation (listing_id);         -- 매물별 예약 합계

        CREATE TABLE IF NOT EXISTS t_deal (
            deal_id    INTEGER PRIMARY KEY AUTOINCREMENT, -- 체결 ID
            kind       INTEGER NOT NULL,                  -- 1=자원 2=장비
            tid        INTEGER NOT NULL,                  -- ItemTID 또는 EquipTID
            quantity   INTEGER NOT NULL,                  -- 체결 수량
            unit_price INTEGER NOT NULL,                  -- 체결 단가
            dealt_at   TEXT    NOT NULL                   -- 체결 시각 (UTC) = Confirm 도착
        ) STRICT;
        CREATE INDEX IF NOT EXISTS idx_deal_tid ON t_deal (kind, tid, dealt_at);                  -- 시세(최근가·전일 평균)

        CREATE TABLE IF NOT EXISTS t_event (
            event_id   INTEGER PRIMARY KEY AUTOINCREMENT, -- 이벤트 ID. AUTOINCREMENT — 확인 후 지워도 번호를 다시 쓰지 않는다
            kind       INTEGER NOT NULL,                  -- 1 취소 · 2 만료 — 메인이 남은 수량을 돌려준다
            listing_id INTEGER NOT NULL,                  -- 매물 (t_listing.listing_id)
            seller_id  INTEGER NOT NULL,                  -- 판매자
            created_at TEXT    NOT NULL                   -- 생긴 시각 (UTC)
        ) STRICT;";

    private static string ToDb(DateTime utc) => utc.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static DateTime FromDb(string text)
        => DateTime.SpecifyKind(DateTime.ParseExact(text, TimeFormat, CultureInfo.InvariantCulture), DateTimeKind.Utc);

    // 진행 중 예약(구매 status 0)의 매물별 합계.
    private const string ReservedExpr =
        "COALESCE((SELECT SUM(r.quantity) FROM t_reservation r JOIN t_purchase p ON p.purchase_id = r.purchase_id " +
        "WHERE r.listing_id = l.listing_id AND p.status = 0), 0)";

    private const string ListingColumns =
        "l.listing_id, l.seller_id, l.kind, l.tid, l.category, l.rarity, l.count, l.enchant_grade, l.options, " +
        "l.unit_price, l.state, l.expires_at, " + ReservedExpr + " AS reserved";

    private sealed record ListingRow
    {
        public long   listing_id    { get; init; }
        public long   seller_id     { get; init; }
        public long   kind          { get; init; }
        public long   tid           { get; init; }
        public long   category      { get; init; }
        public long   rarity        { get; init; }
        public long   count         { get; init; }
        public long   enchant_grade { get; init; }
        public string options       { get; init; } = "[]";
        public long   unit_price    { get; init; }
        public long   state         { get; init; }
        public string expires_at    { get; init; } = "";
        public long   reserved      { get; init; }

        public long Available => count - reserved;

        /// <summary><paramref name="quantity"/>를 수량으로 한 매물. 검색에는 판매 가능 수량, 내 매물에는 남은 수량을 싣는다.</summary>
        public Listing ToListing(long quantity)
        {
            return new Listing(
                listing_id, seller_id, (ListingKind)kind, (int)tid, (int)category, (int)rarity, (int)quantity,
                (int)enchant_grade, JsonSerializer.Deserialize<int[]>(options) ?? Array.Empty<int>(),
                unit_price, FromDb(expires_at));
        }

        /// <summary>바깥에 보이는 상태 — 판매 중인데 예약이 걸려 있으면 Reserved다.</summary>
        public ListingState ViewState => state == (long)ListingState.Listed && reserved > 0 ? ListingState.Reserved : (ListingState)state;
    }

    private sealed record PurchaseRow
    {
        public long purchase_id { get; init; }
        public long buyer_id    { get; init; }
        public long total_price { get; init; }
        public long status      { get; init; }
    }

    private sealed record ReservationRow
    {
        public long listing_id { get; init; }
        public long seller_id  { get; init; }
        public long kind       { get; init; }
        public long tid        { get; init; }
        public long quantity   { get; init; }
        public long unit_price { get; init; }
    }

    private ListingRow? FindListing(long listingId, SqliteTransaction? tx = null)
    {
        return _conn.QueryFirstOrDefault<ListingRow>(
            $"SELECT {ListingColumns} FROM t_listing l WHERE l.listing_id = @listingId;", new { listingId }, tx);
    }

    /// <summary>
    /// 검색 인덱스에 올릴 모습 — 판매 중이고 판매 가능 수량이 남았으면 그 수량으로, 아니면 null(인덱스에서 뺀다).
    /// </summary>
    public Listing? IndexEntry(long listingId)
    {
        var row = FindListing(listingId);
        if (row is null || row.state != (long)ListingState.Listed || row.Available <= 0)
        {
            return null;
        }

        return row.ToListing(row.Available);
    }

    /// <summary>판매 중이고 판매 가능 수량이 남은 매물 전부 — 재시작 때 인덱스를 다시 만든다.</summary>
    public List<Listing> LoadListed()
    {
        return _conn.Query<ListingRow>($"SELECT {ListingColumns} FROM t_listing l WHERE l.state = 1;")
            .Where(r => r.Available > 0)
            .Select(r => r.ToListing(r.Available))
            .ToList();
    }

    /// <summary>새 매물이면 넣고 true. 같은 ID가 이미 있으면(재전송) 아무것도 안 하고 false.</summary>
    public bool InsertListing(Listing l, DateTime now)
    {
        var inserted = _conn.Execute(
            @"INSERT INTO t_listing (listing_id, seller_id, kind, tid, category, rarity, count, listed_count, enchant_grade, options,
                                     unit_price, state, expires_at, created_at)
              VALUES (@ListingId, @SellerId, @kind, @Tid, @Category, @Rarity, @Count, @Count, @EnchantGrade, @options,
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

    // ── 예약 ──

    private List<Allocation> LoadAllocations(long purchaseId, SqliteTransaction? tx = null)
    {
        return _conn.Query<ReservationRow>(
                @"SELECT r.listing_id, l.seller_id, l.kind, l.tid, r.quantity, r.unit_price
                  FROM t_reservation r JOIN t_listing l ON l.listing_id = r.listing_id
                  WHERE r.purchase_id = @purchaseId ORDER BY r.unit_price, r.listing_id;",
                new { purchaseId }, tx)
            .Select(r => new Allocation(r.listing_id, r.seller_id, (int)r.quantity, r.unit_price))
            .ToList();
    }

    private PurchaseRow? FindPurchase(long purchaseId, SqliteTransaction tx)
    {
        return _conn.QueryFirstOrDefault<PurchaseRow>(
            "SELECT purchase_id, buyer_id, total_price, status FROM t_purchase WHERE purchase_id = @purchaseId;", new { purchaseId }, tx);
    }

    private void InsertPurchase(long purchaseId, long buyerId, IReadOnlyList<Allocation> lines, DateTime now, SqliteTransaction tx)
    {
        _conn.Execute(
            "INSERT INTO t_purchase (purchase_id, buyer_id, total_price, status, created_at) VALUES (@purchaseId, @buyerId, @total, 0, @now);",
            new { purchaseId, buyerId, total = lines.Sum(a => a.Price), now = ToDb(now) }, tx);

        foreach (var a in lines)
        {
            _conn.Execute(
                "INSERT INTO t_reservation (purchase_id, listing_id, quantity, unit_price) VALUES (@purchaseId, @ListingId, @Quantity, @UnitPrice);",
                new { purchaseId, a.ListingId, a.Quantity, a.UnitPrice }, tx);
        }
    }

    /// <summary>
    /// 경매장 즉시구매 — 매물 하나를 통째로 잡는다. 이미 누가 일부라도 잡고 있으면 구매 중이다.
    /// 같은 구매 ID의 재시도면 처음 결과를 돌려준다.
    /// </summary>
    public ReserveOutcome ReserveListing(long purchaseId, long listingId, long buyerId, long expectedTotal, DateTime now)
    {
        using var tx = _conn.BeginTransaction();

        var previous = FindPurchase(purchaseId, tx);
        if (previous is not null)
        {
            // 같은 ID로 다른 매물·구매자가 왔다 — 재시도가 아니라 ID 충돌이다. Ok를 주면 메인이 예약 없는 매물을 정산한다.
            var lines = LoadAllocations(purchaseId, tx);
            if (previous.buyer_id != buyerId || lines.Count != 1 || lines[0].ListingId != listingId)
            {
                return new ReserveOutcome(ReserveResult.InProgress);
            }

            return new ReserveOutcome(ReserveResult.Ok, previous.total_price, lines);
        }

        var row = FindListing(listingId, tx);
        var rejected = Judge(row, buyerId, expectedTotal, now);
        if (rejected is not null)
        {
            return new ReserveOutcome(rejected.Value);
        }

        var allocation = new[] { new Allocation(row!.listing_id, row.seller_id, (int)row.count, row.unit_price) };
        InsertPurchase(purchaseId, buyerId, allocation, now, tx);

        tx.Commit();
        return new ReserveOutcome(ReserveResult.Ok, allocation[0].Price, allocation);
    }

    private static ReserveResult? Judge(ListingRow? row, long buyerId, long expectedTotal, DateTime now)
    {
        if (row is null)
        {
            return ReserveResult.NotFound;
        }

        switch ((ListingState)row.state)
        {
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

        if (row.reserved > 0)
        {
            return ReserveResult.InProgress;
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

    private sealed record CandidateRow
    {
        public long listing_id { get; init; }
        public long seller_id  { get; init; }
        public long unit_price { get; init; }
        public long available  { get; init; }
    }

    /// <summary>
    /// 거래소 수량 구매 — 한 종류를 단가 상한 안에서 최저가부터 <paramref name="quantity"/>개 잡는다. 여러 매물에 걸칠 수 있다.
    /// <b>다 채우지 못하면 아무것도 잡지 않는다</b>(NotEnough). 자기 매물은 건너뛴다. 같은 구매 ID의 재시도면 처음 결과를 돌려준다.
    /// </summary>
    public ReserveOutcome ReserveQuantity(long purchaseId, long buyerId, int tid, int quantity, long maxUnitPrice, DateTime now)
    {
        if (quantity <= 0 || maxUnitPrice <= 0)
        {
            return new ReserveOutcome(ReserveResult.NotEnough);
        }

        using var tx = _conn.BeginTransaction();

        var previous = FindPurchase(purchaseId, tx);
        if (previous is not null)
        {
            var lines = LoadAllocations(purchaseId, tx);
            if (previous.buyer_id != buyerId || lines.Sum(a => a.Quantity) != quantity)
            {
                return new ReserveOutcome(ReserveResult.InProgress);
            }

            return new ReserveOutcome(ReserveResult.Ok, previous.total_price, lines);
        }

        var candidates = _conn.Query<CandidateRow>(
            $@"SELECT listing_id, seller_id, unit_price, available FROM (
                   SELECT l.listing_id, l.seller_id, l.unit_price, l.count - {ReservedExpr} AS available
                   FROM t_listing l
                   WHERE l.kind = 1 AND l.tid = @tid AND l.state = 1 AND l.expires_at > @now
                     AND l.seller_id <> @buyerId AND l.unit_price <= @maxUnitPrice)
               WHERE available > 0
               ORDER BY unit_price, listing_id;",
            new { tid, now = ToDb(now), buyerId, maxUnitPrice }, tx);

        var allocations = new List<Allocation>();
        var remaining   = quantity;
        foreach (var c in candidates)
        {
            var take = (int)Math.Min(remaining, c.available);
            allocations.Add(new Allocation(c.listing_id, c.seller_id, take, c.unit_price));
            remaining -= take;
            if (remaining == 0)
            {
                break;
            }
        }

        if (remaining > 0)
        {
            return new ReserveOutcome(ReserveResult.NotEnough);
        }

        InsertPurchase(purchaseId, buyerId, allocations, now, tx);

        tx.Commit();
        return new ReserveOutcome(ReserveResult.Ok, allocations.Sum(a => a.Price), allocations);
    }

    // ── 확정 ──

    /// <summary>
    /// 메인 정산 결과. 성공이면 잡은 수량만큼 남은 수량을 줄이고(0이면 Sold) 체결을 기록한다 —
    /// 타임아웃으로 풀린 뒤 늦게 와도 반영한다(메인이 이미 판 사실이다). 실패면 예약을 놓는다. 같은 구매는 한 번만.
    /// </summary>
    /// <returns>인덱스를 다시 맞춰야 하는 매물 ID.</returns>
    public List<long> Confirm(long purchaseId, bool success, DateTime now)
    {
        using var tx = _conn.BeginTransaction();

        var purchase = FindPurchase(purchaseId, tx);
        if (purchase is null || purchase.status is 1 or 2)
        {
            return new List<long>();
        }

        var lines = _conn.Query<ReservationRow>(
            @"SELECT r.listing_id, l.seller_id, l.kind, l.tid, r.quantity, r.unit_price
              FROM t_reservation r JOIN t_listing l ON l.listing_id = r.listing_id WHERE r.purchase_id = @purchaseId;",
            new { purchaseId }, tx).ToList();

        _conn.Execute(
            "UPDATE t_purchase SET status = @status, confirmed_at = @now WHERE purchase_id = @purchaseId;",
            new { purchaseId, status = success ? 1 : 2, now = ToDb(now) }, tx);

        if (success)
        {
            foreach (var line in lines)
            {
                _conn.Execute(
                    @"UPDATE t_listing
                         SET count     = MAX(count - @quantity, 0),
                             state     = CASE WHEN count - @quantity <= 0 THEN 3 ELSE state END,
                             closed_at = CASE WHEN count - @quantity <= 0 THEN @now ELSE closed_at END
                       WHERE listing_id = @listingId;",
                    new { line.quantity, listingId = line.listing_id, now = ToDb(now) }, tx);

                _conn.Execute(
                    "INSERT INTO t_deal (kind, tid, quantity, unit_price, dealt_at) VALUES (@kind, @tid, @quantity, @unitPrice, @now);",
                    new { line.kind, line.tid, line.quantity, unitPrice = line.unit_price, now = ToDb(now) }, tx);
            }
        }

        tx.Commit();
        return lines.Select(l => l.listing_id).ToList();
    }

    // ── 취소·만료·타임아웃 ──

    /// <summary>
    /// 판매자 취소. 누가 잡고 있으면 구매 중이라 거절한다. Listed → Cancelled + 이벤트를 한 트랜잭션으로 쓴다 —
    /// 이벤트가 빠지면 남은 수량이 영원히 잠긴다. 일부 팔린 매물도 남은 수량은 취소할 수 있다.
    /// </summary>
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
            case ListingState.Sold:
            case ListingState.Expired:
                return CancelResult.Closed;
        }

        if (row.reserved > 0)
        {
            return CancelResult.InProgress;
        }

        _conn.Execute("UPDATE t_listing SET state = 4, closed_at = @now WHERE listing_id = @listingId AND state = 1;",
            new { listingId, now = ToDb(now) }, tx);
        InsertEvent(AuctionEventKind.Cancelled, listingId, sellerId, now, tx);

        tx.Commit();
        return CancelResult.Ok;
    }

    /// <summary>만료 시각이 지난 Listed를 Expired로 바꾸고 이벤트를 남긴다. 예약이 걸린 매물은 그 예약이 끝날 때까지 기다린다.</summary>
    public List<long> ExpireDue(DateTime now)
    {
        using var tx = _conn.BeginTransaction();

        var due = _conn.Query<ListingRow>(
                $"SELECT {ListingColumns} FROM t_listing l WHERE l.state = 1 AND l.expires_at <= @now;", new { now = ToDb(now) }, tx)
            .Where(r => r.reserved == 0)
            .ToList();

        foreach (var row in due)
        {
            _conn.Execute("UPDATE t_listing SET state = 5, closed_at = @now WHERE listing_id = @id AND state = 1;",
                new { id = row.listing_id, now = ToDb(now) }, tx);
            InsertEvent(AuctionEventKind.Expired, row.listing_id, row.seller_id, now, tx);
        }

        tx.Commit();
        return due.Select(r => r.listing_id).ToList();
    }

    /// <summary>예약 시각이 <paramref name="cutoff"/> 이전인 예약을 타임아웃시킨다. 수량이 풀린 매물 ID를 돌려준다.</summary>
    public List<long> ReleaseStaleReservations(DateTime cutoff)
    {
        using var tx = _conn.BeginTransaction();

        var listings = _conn.Query<long>(
            @"SELECT DISTINCT r.listing_id FROM t_reservation r JOIN t_purchase p ON p.purchase_id = r.purchase_id
              WHERE p.status = 0 AND p.created_at <= @cutoff;",
            new { cutoff = ToDb(cutoff) }, tx).ToList();

        _conn.Execute("UPDATE t_purchase SET status = 3 WHERE status = 0 AND created_at <= @cutoff;", new { cutoff = ToDb(cutoff) }, tx);

        tx.Commit();
        return listings;
    }

    private void InsertEvent(AuctionEventKind kind, long listingId, long sellerId, DateTime now, SqliteTransaction tx)
    {
        _conn.Execute(
            "INSERT INTO t_event (kind, listing_id, seller_id, created_at) VALUES (@kind, @listingId, @sellerId, @now);",
            new { kind = (int)kind, listingId, sellerId, now = ToDb(now) }, tx);
    }

    // ── 이벤트·조회 ──

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

    /// <summary>매물별 상태. 판매 중인데 예약이 걸려 있으면 Reserved, 모르는 매물은 null이다.</summary>
    public Dictionary<long, ListingState?> GetStates(IReadOnlyCollection<long> listingIds)
    {
        var found = _conn.Query<ListingRow>($"SELECT {ListingColumns} FROM t_listing l WHERE l.listing_id IN @listingIds;", new { listingIds })
            .ToDictionary(r => r.listing_id, r => (ListingState?)r.ViewState);

        return listingIds.Distinct().ToDictionary(id => id, id => found.GetValueOrDefault(id));
    }

    /// <summary>한 판매자의 진행 중 매물(Listed), 등록 순. 수량은 <b>남은 수량</b>이고 예약이 걸리면 Reserved로 보인다.</summary>
    public List<ListingWithState> GetSellerListings(long sellerId)
    {
        return _conn.Query<ListingRow>(
                $"SELECT {ListingColumns} FROM t_listing l WHERE l.seller_id = @sellerId AND l.state = 1 ORDER BY l.listing_id;",
                new { sellerId })
            .Select(r => new ListingWithState(r.ToListing(r.count), r.ViewState))
            .ToList();
    }

    private sealed record StatRow
    {
        public long  tid         { get; init; }
        public long  recent      { get; init; }
        public long? volume      { get; init; }
        public long? turnover    { get; init; }
    }

    /// <summary>
    /// 종류별 시세 — 최근 체결 단가, 전일(<paramref name="todayStart"/> 앞 24시간) 수량 가중 평균 단가. 체결이 없으면 0.
    /// </summary>
    public Dictionary<int, MarketStat> MarketStats(ListingKind kind, IReadOnlyCollection<int> tids, DateTime todayStart)
    {
        if (tids.Count == 0)
        {
            return new Dictionary<int, MarketStat>();
        }

        var rows = _conn.Query<StatRow>(
            @"SELECT d.tid,
                     (SELECT d2.unit_price FROM t_deal d2 WHERE d2.kind = @kind AND d2.tid = d.tid ORDER BY d2.deal_id DESC LIMIT 1) AS recent,
                     (SELECT SUM(d3.quantity) FROM t_deal d3
                       WHERE d3.kind = @kind AND d3.tid = d.tid AND d3.dealt_at >= @from AND d3.dealt_at < @to) AS volume,
                     (SELECT SUM(d3.quantity * d3.unit_price) FROM t_deal d3
                       WHERE d3.kind = @kind AND d3.tid = d.tid AND d3.dealt_at >= @from AND d3.dealt_at < @to) AS turnover
              FROM t_deal d WHERE d.kind = @kind AND d.tid IN @tids GROUP BY d.tid;",
            new { kind = (int)kind, tids, from = ToDb(todayStart.AddDays(-1)), to = ToDb(todayStart) });

        return rows.ToDictionary(
            r => (int)r.tid,
            r => new MarketStat((int)r.tid, r.recent, r.volume is > 0 ? r.turnover!.Value / r.volume.Value : 0));
    }

    public void Dispose() => _conn.Dispose();
}
