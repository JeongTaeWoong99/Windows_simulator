using Microsoft.Data.Sqlite;

namespace WSGameServer;

/// <summary>
/// 파일 DB가 다른 연결에 잠겨 있을 때 운영 DB 큐(<see cref="DBManager"/>)가 <b>바로 실패하지 않고 기다리는지</b> 검증한다.
/// 기다리지 않으면 채취 정산·뽑기 쓰기가 겹친 순간 <c>database is locked</c>로 세션이 끊긴다 (이슈 #85).
/// </summary>
public class DBManagerLockTest : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ws-lock-{Guid.NewGuid():N}");
    private readonly string _path;

    public DBManagerLockTest()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "game.sqlite3");

        using var conn = new SqliteConnection($"Data Source={_path};Pooling=False");
        conn.Open();
        Execute(conn, "CREATE TABLE t (v INTEGER NOT NULL);");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task 다른_연결이_쓰기_중이면_풀릴_때까지_기다렸다가_쓴다()
    {
        var manager = new DBManager(new FakeLogicExecutor());
        manager.Initialize(_path);

        // 다른 연결이 쓰기 잠금을 잡고 있다 — 채취 정산 트랜잭션이 도는 중인 상황
        using var holder = new SqliteConnection($"Data Source={_path};Pooling=False");
        holder.Open();
        using var tx = holder.BeginTransaction();
        Execute(holder, "INSERT INTO t (v) VALUES (1);", tx);

        var write = Task.Run(() => manager.RunAsync(db => db.ExecuteAsync("INSERT INTO t (v) VALUES (2);")));

        // 0.5초 뒤 잠금을 푼다 — 그동안 기다렸다면 쓰기가 성공한다
        await Task.Delay(500);
        tx.Commit();

        (await write).ShouldBe(1);
    }

    [Fact]
    public async Task 다른_연결이_읽는_중이어도_RETURNING_쓰기가_기다렸다가_커밋된다()
    {
        var manager = new DBManager(new FakeLogicExecutor());
        manager.Initialize(_path);

        // 다른 연결이 읽기 트랜잭션을 잡고 있다(SHARED) — 롤백 저널에서는 커밋에 필요한 EXCLUSIVE를 막는다.
        // RETURNING 문은 커밋이 step이 아니라 reset에서 일어나 드라이버의 재시도 루프를 거치지 않는다 (GrantCharacterRepository).
        using var holder = new SqliteConnection($"Data Source={_path};Pooling=False");
        holder.Open();
        using var tx = holder.BeginTransaction(deferred: true);
        Execute(holder, "SELECT COUNT(*) FROM t;", tx);

        var write = Task.Run(() => manager.RunAsync(db => db.ExecuteScalarAsync<long>("INSERT INTO t (v) VALUES (2) RETURNING v;")));

        await Task.Delay(500);
        tx.Commit();

        (await write).ShouldBe(2);
    }

    private static void Execute(SqliteConnection conn, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        cmd.ExecuteNonQuery();
    }
}
