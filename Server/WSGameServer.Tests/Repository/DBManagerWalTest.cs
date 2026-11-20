using Microsoft.Data.Sqlite;

namespace WSGameServer;

/// <summary>
/// 운영 DB 큐(<see cref="DBManager"/>)가 파일 DB를 <b>WAL로 열고, 끌 때 WAL을 본체에 합치는지</b> 검증한다.
/// WAL이 아니면 동접 500명에서 <c>database is locked</c>로 세션이 끊기고(T-192 측정), 합치지 않으면 git의 game.sqlite3가 낡는다.
/// </summary>
public class DBManagerWalTest : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ws-wal-{Guid.NewGuid():N}");
    private readonly string _path;

    public DBManagerWalTest()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "game.sqlite3");

        using var conn = new SqliteConnection($"Data Source={_path};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE t (v INTEGER NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task 파일_DB는_WAL_모드로_열린다()
    {
        var manager = new DBManager(new FakeLogicExecutor());
        manager.Initialize(_path);

        var mode = await manager.RunAsync(db => db.ExecuteScalarAsync<string>("PRAGMA journal_mode;"));

        mode.ShouldBe("wal");
    }

    [Fact]
    public async Task 연결마다_동기화는_NORMAL이다()
    {
        // WAL에서 NORMAL은 전원이 나가도 DB가 깨지지 않는다 — 마지막 커밋 몇 개만 잃을 수 있다. 0 OFF · 1 NORMAL · 2 FULL
        var manager = new DBManager(new FakeLogicExecutor());
        manager.Initialize(_path);

        var synchronous = await manager.RunAsync(db => db.ExecuteScalarAsync<long>("PRAGMA synchronous;"));

        synchronous.ShouldBe(1);
    }

    [Fact]
    public async Task 체크포인트하면_쓴_내용이_본체에_합쳐지고_WAL_파일이_빈다()
    {
        var manager = new DBManager(new FakeLogicExecutor());
        manager.Initialize(_path);
        await manager.RunAsync(db => db.ExecuteAsync("INSERT INTO t (v) VALUES (7);"));

        manager.Checkpoint();

        var wal = new FileInfo(_path + "-wal");
        (wal.Exists ? wal.Length : 0).ShouldBe(0);
    }
}
