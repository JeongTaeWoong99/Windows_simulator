using Microsoft.Data.Sqlite;
using MikaNetwork.Server;
using MikaUtils;
using WSGameServer.User;
using WSGameServer.User.Repository;

namespace WSGameServer.DB;

public class DBManager : Singleton<DBManager>
{
    private string _connectionString = "";

    // DB 파일명을 받아 연결 문자열을 구성한다 (서버 시작 시 1회 호출)
    public void Initialize(string dbFileName)
    {
        string dbPath = ResolveDbPath(dbFileName);

        // 문자열 직접 조립 대신 빌더 사용 → 경로에 공백/특수문자가 있어도 안전
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite,
        }.ToString();
    }

    // 실행 위치(bin/publish)와 무관하게 소스의 Shared/<dbFileName>을 찾는다.
    // 실행 파일 폴더에서 상위로 거슬러 올라가며 탐색한다.
    private static string ResolveDbPath(string dbFileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Shared", dbFileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"DB 파일을 찾을 수 없습니다: Shared/{dbFileName}");
    }

    public void Post(IRepository repository)
    {
        // 같은 Key(유저)는 직렬, 다른 Key는 병렬로 처리
        DBExecutor.Instance.Post(repository.Key, async () =>
        {
            // 작업마다 커넥션을 열어 Repository로 넘겨준다
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await repository.ExecuteAsync(conn); // SP 실행 -- 다른 스레드가 작업 이어서 할 수 있음 (순서는 보장)

            LogicExecutor.Instance.Post(repository.Apply);
        });
    }
}
