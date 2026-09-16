using Microsoft.Data.Sqlite;

namespace WSGameServer;

/// <summary>
/// Repository SQL을 실제 SQLite(<c>:memory:</c>)에 대고 돌리기 위한 커넥션. 파일 DB는 건드리지 않는다.
/// 스키마는 운영 DB(<c>Server/Shared/game.sqlite3</c>)와 같아야 한다 — 어긋나면 여기서 통과하고 운영에서 깨진다.
/// </summary>
internal sealed class SqliteFixture : IDisposable
{
    public SqliteConnection Connection { get; }

    public SqliteFixture()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
    }

    public void CreateUserTable()
    {
        Execute(@"
            CREATE TABLE t_user (
                user_id     INTEGER PRIMARY KEY,
                provider_id TEXT    NOT NULL UNIQUE,
                nickname    TEXT    NOT NULL,
                admin_level INTEGER NOT NULL DEFAULT 0,
                is_deleted  INTEGER NOT NULL DEFAULT 0,
                is_banned   INTEGER NOT NULL DEFAULT 0,
                created_at  TEXT    NOT NULL DEFAULT (datetime('now'))
            ) STRICT;");
    }

    public void Execute(string sql)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
