using Microsoft.Data.Sqlite;

namespace WSGameServer;

/// <summary>
/// 기동 시 마이그레이션(<see cref="DbMigrator"/>)이 <c>user_version</c>보다 큰 번호만 번호순으로, 파일 하나씩 원자적으로 적용하는지 지킨다.
/// 틀리면 이미 적용한 변경을 다시 돌려 기동이 깨지거나, 반쯤 적용된 DB가 남는다.
/// </summary>
public class DbMigratorTest : IDisposable
{
    private static readonly Migration CreateA = new(1, "create-a", "CREATE TABLE t_a (id INTEGER PRIMARY KEY) STRICT;");

    // t_a가 있어야 성공한다 — 순서가 뒤집히면 실패한다.
    private static readonly Migration AddName = new(2, "add-name", "ALTER TABLE t_a ADD COLUMN name TEXT NOT NULL DEFAULT '';");

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ws-migrator-{Guid.NewGuid():N}");

    public DbMigratorTest()
    {
        _conn.Open();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        _conn.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void 빈_DB에는_모든_마이그레이션이_번호순으로_적용된다()
    {
        var applied = DbMigrator.Apply(_conn, [AddName, CreateA]);

        applied.Select(m => m.Version).ShouldBe([1, 2]);
    }

    [Fact]
    public void 적용이_끝나면_판이_마지막_번호가_된다()
    {
        DbMigrator.Apply(_conn, [CreateA, AddName]);

        UserVersion(_conn).ShouldBe(2);
    }

    [Fact]
    public void 판_이하의_번호는_다시_돌지_않는다()
    {
        // 1번까지 적용된 DB — 1번을 다시 돌리면 "이미 있는 테이블"로 실패한다.
        Execute(_conn, "CREATE TABLE t_a (id INTEGER PRIMARY KEY) STRICT; PRAGMA user_version = 1;");

        var applied = DbMigrator.Apply(_conn, [CreateA, AddName]);

        applied.Select(m => m.Version).ShouldBe([2]);
    }

    [Fact]
    public void 실패한_마이그레이션의_변경은_모두_되돌려진다()
    {
        // 첫 문장은 성공하고 두 번째 문장에서 실패한다.
        var broken = new Migration(2, "broken", "CREATE TABLE t_b (id INTEGER PRIMARY KEY) STRICT; INSERT INTO t_missing VALUES (1);");

        Should.Throw<Exception>(() => DbMigrator.Apply(_conn, [CreateA, broken]));

        TableExists(_conn, "t_b").ShouldBeFalse();
    }

    [Fact]
    public void 실패한_마이그레이션은_판을_올리지_않는다()
    {
        var broken = new Migration(2, "broken", "INSERT INTO t_missing VALUES (1);");

        Should.Throw<Exception>(() => DbMigrator.Apply(_conn, [CreateA, broken]));

        UserVersion(_conn).ShouldBe(1);
    }

    [Fact]
    public void DB_판이_서버가_아는_마지막_번호보다_높으면_거절한다()
    {
        // 새 서버가 올린 DB를 옛 서버가 여는 경우 — 모르는 컬럼이 있는 DB를 조용히 쓰면 안 된다.
        Execute(_conn, "PRAGMA user_version = 3;");

        Should.Throw<InvalidOperationException>(() => DbMigrator.Apply(_conn, [CreateA, AddName]));
    }

    [Fact]
    public void 폴더의_파일은_번호순으로_읽힌다()
    {
        WriteMigration("0002-add-name.sql", AddName.Sql);
        WriteMigration("0001-create-a.sql", CreateA.Sql);

        DbMigrator.LoadFrom(_dir).Select(m => m.Name).ShouldBe(["create-a", "add-name"]);
    }

    [Fact]
    public void 번호가_겹치면_거절한다()
    {
        // 두 사람이 같은 번호를 따로 만든 경우
        WriteMigration("0001-create-a.sql", CreateA.Sql);
        WriteMigration("0001-create-b.sql", "CREATE TABLE t_b (id INTEGER PRIMARY KEY) STRICT;");

        Should.Throw<InvalidOperationException>(() => DbMigrator.LoadFrom(_dir));
    }

    [Fact]
    public void 번호가_비면_거절한다()
    {
        WriteMigration("0001-create-a.sql", CreateA.Sql);
        WriteMigration("0003-add-name.sql", AddName.Sql);

        Should.Throw<InvalidOperationException>(() => DbMigrator.LoadFrom(_dir));
    }

    [Fact]
    public void 번호_없는_sql_파일은_거절한다()
    {
        WriteMigration("add-name.sql", AddName.Sql);

        Should.Throw<InvalidOperationException>(() => DbMigrator.LoadFrom(_dir));
    }

    [Fact]
    public void 적용할_것이_있으면_적용_전_DB를_백업한다()
    {
        var dbPath = CreateFileDb();
        var migrations = Path.Combine(_dir, "migrations");
        Directory.CreateDirectory(migrations);
        File.WriteAllText(Path.Combine(migrations, "0001-create-a.sql"), CreateA.Sql);

        DbMigrator.MigrateFile(dbPath, migrations);

        // 백업은 적용 전 상태다 — t_a가 없어야 한다.
        var backup = Directory.GetFiles(_dir, "*.bak").ShouldHaveSingleItem();
        using var conn = new SqliteConnection($"Data Source={backup};Pooling=False");
        conn.Open();
        TableExists(conn, "t_a").ShouldBeFalse();
    }

    [Fact]
    public void 적용할_것이_없으면_백업을_남기지_않는다()
    {
        var dbPath = CreateFileDb();
        var migrations = Path.Combine(_dir, "migrations");
        Directory.CreateDirectory(migrations);

        DbMigrator.MigrateFile(dbPath, migrations);

        Directory.GetFiles(_dir, "*.bak").ShouldBeEmpty();
    }

    [Fact]
    public void 저장소의_마이그레이션은_빈_DB에_끝까지_적용된다()
    {
        // Server/Shared/migrations가 실행 폴더로 복사된 것 — 문법 오류나 순서 문제가 있으면 여기서 깨진다.
        var migrations = DbMigrator.LoadFrom(DbMigrator.DefaultDirectory);

        DbMigrator.Apply(_conn, migrations);

        UserVersion(_conn).ShouldBe(migrations.Count);
    }

    private string CreateFileDb()
    {
        var path = Path.Combine(_dir, "game.sqlite3");
        using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        conn.Open();
        return path;
    }

    private void WriteMigration(string fileName, string sql)
    {
        File.WriteAllText(Path.Combine(_dir, fileName), sql);
    }

    private static long UserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return (long)cmd.ExecuteScalar()!;
    }

    private static bool TableExists(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        cmd.Parameters.AddWithValue("$name", name);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
