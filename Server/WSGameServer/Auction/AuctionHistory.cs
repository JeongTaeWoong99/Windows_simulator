using MikaProtocol;

namespace WSGameServer;

// t_auction_history 조회 전용 Row.
public sealed record AuctionHistoryRow
{
    public long   history_id    { get; init; }
    public int    type          { get; init; }
    public int    kind          { get; init; }
    public int    tid           { get; init; }
    public int    count         { get; init; }
    public long   unit_price    { get; init; }
    public long   amount        { get; init; }
    public long   fee           { get; init; }
    public int    enchant_grade { get; init; }
    public string created_at    { get; init; } = "";
}

/// <summary>완료 탭 한 쪽. <see cref="UnseenCount"/>는 이번 요청이 "봤다"로 표시하기 <b>전</b>의 새 기록 수다.</summary>
public sealed record AuctionHistoryPage(List<AuctionHistoryRow> Entries, bool HasMore, int UnseenCount);

/// <summary>
/// 거래 기록(T-156) — 경매장 완료 탭. 쓰기는 정산·반환 트랜잭션 안에서만 한다(<see cref="AuctionDb"/>) —
/// 우편과 기록이 함께 남거나 함께 롤백된다.
/// </summary>
public static class AuctionHistoryDb
{
    /// <summary>보관 기간 — 조회할 때 이보다 오래된 그 유저의 기록을 지운다. 목업 기간 드롭다운의 최대(30일)와 같다.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public const int MaxPageSize = 50;

    public static Task InsertAsync(
        DbConnection tx, long userId, EAuctionHistoryType type, AuctionItemSnapshot item, int count, long unitPrice,
        long amount, long fee, long tradeId, DateTime now)
    {
        return tx.ExecuteAsync(
            @"INSERT INTO t_auction_history (user_id, type, kind, tid, count, unit_price, amount, fee, enchant_grade, trade_id, created_at)
              VALUES (@userId, @type, @kind, @tid, @count, @unitPrice, @amount, @fee, @enchantGrade, @tradeId, @now);",
            new
            {
                userId, type = (int)type, kind = (int)item.Kind, tid = item.Tid, count, unitPrice, amount, fee,
                enchantGrade = item.EnchantGrade, tradeId, now = MailDb.ToDb(now),
            });
    }

    /// <summary>
    /// 최신순으로 한 쪽. 구분 <c>None</c>·기간 0은 조건 없음, 커서는 마지막으로 본 <c>history_id</c>(첫 쪽 0).
    /// 쪽 크기 0이면 줄 없이 새 기록 수만 센다(탭 배지). <paramref name="markSeen"/>이면 지금까지를 "봤다"로 남긴다.
    /// </summary>
    public static async Task<AuctionHistoryPage> QueryAsync(
        DbConnection connection, long userId, EAuctionHistoryType type, int days, long cursor, int pageSize, bool markSeen, DateTime now)
    {
        AuctionHistoryPage page = new(new List<AuctionHistoryRow>(), false, 0);

        await connection.InTransactionAsync(async tx =>
        {
            await tx.ExecuteAsync(
                "DELETE FROM t_auction_history WHERE user_id = @userId AND created_at < @before;",
                new { userId, before = MailDb.ToDb(now - Retention) });

            var lastSeen = await tx.QueryFirstOrDefaultAsync<long>(
                "SELECT last_seen_id FROM t_auction_history_seen WHERE user_id = @userId;", new { userId });
            var unseen = await tx.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM t_auction_history WHERE user_id = @userId AND history_id > @lastSeen;", new { userId, lastSeen });

            var rows = new List<AuctionHistoryRow>();
            var size = Math.Clamp(pageSize, 0, MaxPageSize);
            if (size > 0)
            {
                rows = await tx.QueryAsync<AuctionHistoryRow>(
                    @"SELECT history_id, type, kind, tid, count, unit_price, amount, fee, enchant_grade, created_at
                        FROM t_auction_history
                       WHERE user_id = @userId
                         AND (@type = 0 OR type = @type)
                         AND (@since = '' OR created_at >= @since)
                         AND (@cursor = 0 OR history_id < @cursor)
                       ORDER BY history_id DESC
                       LIMIT @limit;",
                    new
                    {
                        userId, type = (int)type, cursor, limit = size + 1,
                        since = days > 0 ? MailDb.ToDb(now.AddDays(-days)) : "",
                    });
            }

            if (markSeen)
            {
                await tx.ExecuteAsync(
                    @"INSERT INTO t_auction_history_seen (user_id, last_seen_id)
                      VALUES (@userId, (SELECT COALESCE(MAX(history_id), 0) FROM t_auction_history WHERE user_id = @userId))
                      ON CONFLICT (user_id) DO UPDATE SET last_seen_id = excluded.last_seen_id;",
                    new { userId });
            }

            var hasMore = rows.Count > size;
            page = new AuctionHistoryPage(hasMore ? rows.Take(size).ToList() : rows, hasMore, (int)unseen);
        });

        return page;
    }
}
