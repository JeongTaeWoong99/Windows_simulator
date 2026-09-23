using Microsoft.Extensions.Time.Testing;

namespace AuctionServer.Tests;

/// <summary>
/// 임시 파일 DB 위에 엔진을 띄운다. 파일이라 <see cref="Restart"/>로 "프로세스 재시작"을 흉내 낼 수 있다.
/// 시각은 <see cref="FakeTimeProvider"/>가 쥔다 — 만료·예약 타임아웃을 기다리지 않고 앞당긴다.
/// </summary>
internal sealed class EngineFixture : IAsyncDisposable
{
    public static readonly DateTime Start = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"auction-test-{Guid.NewGuid():N}.sqlite3");

    public FakeTimeProvider Time    { get; } = new(new DateTimeOffset(Start));
    public AuctionOptions   Options { get; } = new();
    public AuctionEngine    Engine  { get; private set; }

    public EngineFixture()
    {
        Engine = Open();
    }

    public DateTime Now => Time.GetUtcNow().UtcDateTime;

    public void Advance(TimeSpan by) => Time.Advance(by);

    /// <summary>엔진을 내리고 같은 DB 파일로 새로 띄운다. 메모리 인덱스는 DB에서 다시 만들어진다.</summary>
    public async Task Restart()
    {
        await Engine.DisposeAsync();
        Engine = Open();
    }

    private AuctionEngine Open()
    {
        var engine = new AuctionEngine(AuctionStore.Open(_path), Time, Options);
        engine.Start();
        return engine;
    }

    public Listing Item(long id, long unitPrice = 100, int count = 1, long seller = 100, int tid = 10001)
        => new(id, seller, ListingKind.Item, tid, Category: 1, Rarity: 1, count, EnchantGrade: 0, Array.Empty<int>(), unitPrice, Now.AddHours(48));

    public Listing Equip(long id, long unitPrice = 100, long seller = 100, int grade = 3, params int[] options)
        => new(id, seller, ListingKind.Equip, Tid: 5001, Category: 1, Rarity: 3, Count: 1, grade, options, unitPrice, Now.AddHours(48));

    public List<long> SearchIds(SearchQuery? query = null)
        => Engine.Search(query ?? new SearchQuery()).Listings.Select(l => l.ListingId).ToList();

    public async ValueTask DisposeAsync()
    {
        await Engine.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }
    }
}
