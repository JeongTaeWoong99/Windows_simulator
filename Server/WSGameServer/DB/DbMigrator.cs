using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace WSGameServer;

/// <summary>마이그레이션 파일 하나. 적용이 끝나면 DB의 <c>user_version</c>이 <see cref="Version"/>이 된다.</summary>
public sealed record Migration(int Version, string Name, string Sql);

// 기동 시 DB 스키마를 서버 판까지 올린다. 판은 PRAGMA user_version, 파일은 Shared/migrations/0001-<슬러그>.sql.
// 사람이 손으로 돌리지 않는다 — 손으로 돌리던 시절 적용이 빠진 DB로 로그인이 깨졌다(T-146).
public static class DbMigrator
{
    private static readonly Regex FileNamePattern = new(@"^(\d{4})-(.+)\.sql$");

    /// <summary>실행 폴더의 <c>Migrations/</c> — csproj가 <c>Shared/migrations</c>를 복사해 둔다.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Migrations");

    /// <summary>폴더의 <c>*.sql</c>을 번호순으로 읽는다. 번호가 1부터 빈틈없이 이어지지 않으면 예외.</summary>
    public static IReadOnlyList<Migration> LoadFrom(string directory)
    {
        var migrations = new List<Migration>();
        foreach (var path in Directory.GetFiles(directory, "*.sql"))
        {
            var fileName = Path.GetFileName(path);
            var match = FileNamePattern.Match(fileName);
            if (!match.Success)
            {
                throw new InvalidOperationException($"마이그레이션 파일명은 0001-<슬러그>.sql 형식이어야 한다: {fileName}");
            }

            migrations.Add(new Migration(int.Parse(match.Groups[1].Value), match.Groups[2].Value, File.ReadAllText(path)));
        }

        migrations.Sort((a, b) => a.Version.CompareTo(b.Version));

        // 겹침·빈 번호는 두 사람이 따로 번호를 딴 흔적이다 — 어느 쪽이 먼저인지 모르니 기동을 멈춘다.
        for (int i = 0; i < migrations.Count; i++)
        {
            if (migrations[i].Version != i + 1)
            {
                throw new InvalidOperationException($"마이그레이션 번호가 1부터 이어지지 않는다: {migrations[i].Version:D4}-{migrations[i].Name} (기대 {i + 1:D4})");
            }
        }

        return migrations;
    }

    /// <summary>판보다 큰 번호를 번호순으로 적용하고 적용한 것을 돌려준다. DB 판이 서버가 아는 마지막 번호보다 높으면 예외.</summary>
    public static IReadOnlyList<Migration> Apply(SqliteConnection conn, IReadOnlyList<Migration> migrations)
    {
        var pending = Pending(conn, migrations);
        foreach (var migration in pending)
        {
            // 파일 하나 = 트랜잭션 하나. user_version도 같은 트랜잭션에서 올려야 실패 시 판이 같이 되돌아간다.
            using var tx = conn.BeginTransaction();
            Execute(conn, tx, migration.Sql);
            Execute(conn, tx, $"PRAGMA user_version = {migration.Version};");
            tx.Commit();
        }

        return pending;
    }

    /// <summary>파일 DB를 서버 판까지 올린다. 적용할 것이 있으면 먼저 <c>&lt;db&gt;.v&lt;판&gt;-&lt;UTC 시각&gt;.bak</c>으로 백업한다.</summary>
    public static IReadOnlyList<Migration> MigrateFile(string dbPath, string migrationsDirectory)
    {
        var migrations = LoadFrom(migrationsDirectory);

        // 풀링을 끈다 — 기동 때 한 번 쓰고 닫는 커넥션이 파일을 붙잡고 있지 않게.
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        conn.Open();

        var version = UserVersion(conn);
        if (Pending(conn, migrations).Count == 0)
        {
            ServerLog.Info("DB", $"스키마 판 {version} — 적용할 마이그레이션 없음");
            return [];
        }

        var backupPath = $"{dbPath}.v{version}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false }.ToString()))
        {
            conn.BackupDatabase(backup);
        }
        ServerLog.Info("DB", $"마이그레이션 전 백업: {backupPath}");

        var applied = Apply(conn, migrations);
        foreach (var migration in applied)
        {
            ServerLog.Info("DB", $"마이그레이션 적용: {migration.Version:D4}-{migration.Name}");
        }

        return applied;
    }

    private static List<Migration> Pending(SqliteConnection conn, IReadOnlyList<Migration> migrations)
    {
        var version = UserVersion(conn);
        var latest = migrations.Count == 0 ? 0 : migrations.Max(m => m.Version);
        if (version > latest)
        {
            throw new InvalidOperationException($"DB 스키마 판({version})이 서버가 아는 마지막 번호({latest})보다 높다 — 더 새 서버가 쓰던 DB다.");
        }

        return migrations.Where(m => m.Version > version).OrderBy(m => m.Version).ToList();
    }

    private static long UserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return (long)cmd.ExecuteScalar()!;
    }

    private static void Execute(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
