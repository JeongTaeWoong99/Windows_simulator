using System.Data;
using Microsoft.Data.Sqlite;
using MikaNetwork.Server;
using MikaUtils;

namespace WSGameServer;

public class DBManager : IDBQueue, IDbRunner
{
    // 커넥션을 만드는 방법만 안다 — 파일 DB냐 :memory:냐는 주입하는 쪽이 정한다.
    private Func<SqliteConnection>? _connectionFactory;
    private readonly ILogicExecutor _logicExecutor;

    // 잠금이 풀리길 기다리는 상한. 경매장 DB(AuctionStore)와 같은 값이다.
    private const int BusyTimeoutMs = 5000;
    
    public DBManager(ILogicExecutor logicExecutor)
    {
        _logicExecutor = logicExecutor;
    }
    /// <summary>커넥션 팩토리를 직접 주입한다. 테스트는 <c>:memory:</c> 커넥션을 넘긴다.</summary>
    public void Initialize(Func<SqliteConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>DB 파일 경로(<see cref="ResolveDbPath"/>의 결과)로 연결 문자열을 구성한다. 서버 시작 시 1회.</summary>
    public void Initialize(string dbPath)
    {
        // 문자열 직접 조립 대신 빌더 사용 → 경로에 공백/특수문자가 있어도 안전
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite,
        }.ToString();

        _connectionFactory = () => new SqliteConnection(connectionString);
    }

    // 절대 경로(Docker 볼륨)는 그대로 쓴다 — 없으면 마이그레이터가 새로 만든다.
    // 파일명이면 실행 위치(bin/publish)와 무관하게 실행 파일 폴더에서 상위로 거슬러 올라가며 소스의 Shared/<파일명>을 찾는다.
    public static string ResolveDbPath(string dbFileName)
    {
        if (Path.IsPathRooted(dbFileName))
        {
            return dbFileName;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Shared", dbFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"DB 파일을 찾을 수 없습니다: Shared/{dbFileName}");
    }

    public void Post<TRepository>(TRepository repository) where TRepository : IRepository
    {
        // 같은 Key(유저)는 직렬, 다른 Key는 병렬로 처리
        DBExecutor.Instance.Post(repository.Key, async () =>
        {
            try
            {
                // 작업마다 커넥션을 열어 Repository로 넘겨준다
                await using var conn = await OpenAsync();

                await repository.ExecuteAsync(new DbConnection(conn));
            }
            catch (Exception e)
            {
                // 성공이든 실패든 반드시 로직 스레드로 돌아간다 — 안 그러면 로그인이 무응답으로 멈춘다.
                _logicExecutor.Post(() => repository.OnFailed(e));
                return;
            }

            _logicExecutor.Post(repository.Apply);
        });
    }

    /// <summary>유저에 묶이지 않은 DB 작업(경매 릴레이). 호출한 스레드에서 돈다 — 로직 스레드에서 부르지 않는다.</summary>
    public async Task<T> RunAsync<T>(Func<DbConnection, Task<T>> body)
    {
        await using var conn = await OpenAsync();

        return await body(new DbConnection(conn));
    }

    // 잠기면 SQLite가 직접 기다린다 — 드라이버의 재시도는 step만 감싸서, RETURNING 문의 커밋(reset 시점) 잠김은 바로 실패한다.
    private async Task<SqliteConnection> OpenAsync()
    {
        var conn = _connectionFactory!();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
        await cmd.ExecuteNonQueryAsync();

        return conn;
    }
}
