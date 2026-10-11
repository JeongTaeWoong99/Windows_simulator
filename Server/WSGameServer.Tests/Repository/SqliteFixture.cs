using Microsoft.Data.Sqlite;

namespace WSGameServer;

/// <summary>
/// Repository SQL을 실제 SQLite(<c>:memory:</c>)에 대고 돌리기 위한 커넥션. 파일 DB는 건드리지 않는다.
/// 스키마는 운영 DB와 같은 마이그레이션(<c>Server/Shared/migrations</c>)으로 만든다 — DDL을 손으로 베끼지 않는다.
/// </summary>
internal sealed class SqliteFixture : IDisposable
{
    public SqliteConnection Connection { get; }

    public SqliteFixture()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
    }

    /// <summary>저장소의 마이그레이션을 전부 적용해 운영과 같은 테이블을 만든다.</summary>
    public void CreateSchema()
    {
        DbMigrator.Apply(Connection, DbMigrator.LoadFrom(DbMigrator.DefaultDirectory));
    }

    /// <summary>단일 값 조회. 검증용.</summary>
    public object? Query(string sql)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    public void Execute(string sql)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
