using Dapper;
using Microsoft.Data.Sqlite;

namespace AuctionServer.Tests;

/// <summary>
/// 경매장 DB 판 올리기. 판매 중 매물이 든 옛 파일을 버리지 않고 이어 써야 한다 —
/// 판이 맞지 않는다고 기동이 멈추면 거래 원천이 통째로 막힌다.
/// </summary>
public class AuctionStoreMigrationTest : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"auction-migrate-{Guid.NewGuid():N}.sqlite3");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }
    }

    // 판 2의 t_listing — seller_name이 없다.
    private void CreateVersion2File()
    {
        using var conn = new SqliteConnection($"Data Source={_path};Pooling=False");
        conn.Open();
        conn.Execute(@"
            CREATE TABLE t_listing (
                listing_id INTEGER PRIMARY KEY, seller_id INTEGER NOT NULL, kind INTEGER NOT NULL, tid INTEGER NOT NULL,
                category INTEGER NOT NULL, rarity INTEGER NOT NULL, count INTEGER NOT NULL, listed_count INTEGER NOT NULL,
                enchant_grade INTEGER NOT NULL DEFAULT 0, options TEXT NOT NULL DEFAULT '[]', unit_price INTEGER NOT NULL,
                state INTEGER NOT NULL DEFAULT 1, expires_at TEXT NOT NULL, created_at TEXT NOT NULL, closed_at TEXT
            ) STRICT;
            INSERT INTO t_listing (listing_id, seller_id, kind, tid, category, rarity, count, listed_count, unit_price, expires_at, created_at)
            VALUES (1, 100, 1, 10001, 1, 1, 5, 5, 30, '2099-01-01 00:00:00.000', '2026-09-24 00:00:00.000');
            PRAGMA user_version = 2;");
    }

    [Fact]
    public void 판_2_파일은_판매자_이름을_빈칸으로_이어_쓴다()
    {
        CreateVersion2File();

        using var store = AuctionStore.Open(_path);

        var listing = store.LoadListed().Single();
        (listing.ListingId, listing.Count, listing.SellerName).ShouldBe((1L, 5, ""));
    }
}
