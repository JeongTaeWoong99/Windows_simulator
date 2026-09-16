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

    /// <summary>로그인 조회(<c>LoginRepository</c>)가 읽는 유저 데이터 테이블 전부.</summary>
    public void CreatePlayerTables()
    {
        Execute(@"
            CREATE TABLE t_character (
                character_id  INTEGER PRIMARY KEY,
                user_id       INTEGER NOT NULL,
                character_tid INTEGER NOT NULL,
                level         INTEGER NOT NULL DEFAULT 1,
                exp           INTEGER NOT NULL DEFAULT 0,
                created_at    TEXT    NOT NULL DEFAULT (datetime('now')),
                farming_bonus INTEGER NOT NULL DEFAULT 0,
                fishing_bonus INTEGER NOT NULL DEFAULT 0,
                mining_bonus  INTEGER NOT NULL DEFAULT 0,
                logging_bonus INTEGER NOT NULL DEFAULT 0,
                hunting_bonus INTEGER NOT NULL DEFAULT 0
            ) STRICT;
            CREATE TABLE t_user_currency (
                user_id INTEGER PRIMARY KEY,
                gold    INTEGER NOT NULL DEFAULT 0,
                dia     INTEGER NOT NULL DEFAULT 0
            ) STRICT;
            CREATE TABLE t_user_inventory (
                user_id INTEGER NOT NULL,
                item_id INTEGER NOT NULL,
                count   INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (user_id, item_id)
            ) STRICT;
            CREATE TABLE t_user_workstation_slot (
                user_id        INTEGER NOT NULL,
                slot_index     INTEGER NOT NULL,
                industry       INTEGER NOT NULL DEFAULT 0,
                industry_level INTEGER NOT NULL DEFAULT 1,
                character_id   INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (user_id, slot_index)
            ) STRICT;
            CREATE TABLE t_user_industry_level (
                user_id        INTEGER NOT NULL,
                industry       INTEGER NOT NULL,
                unlocked_level INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (user_id, industry)
            ) STRICT;
            CREATE TABLE t_user_unlock (
                user_id     INTEGER NOT NULL,
                unlock_tid  INTEGER NOT NULL,
                unlocked_at TEXT    NOT NULL DEFAULT (datetime('now')),
                PRIMARY KEY (user_id, unlock_tid)
            ) STRICT;");

        CreateEquipTables();
    }

    /// <summary>장비 개체·착용 매핑. 운영 DDL과 같아야 한다. 로그인 조회에도 필요해 <see cref="CreatePlayerTables"/>가 함께 만든다.</summary>
    public void CreateEquipTables()
    {
        Execute(@"
            CREATE TABLE t_user_equip (
                equip_id      INTEGER PRIMARY KEY,
                user_id       INTEGER NOT NULL,
                equip_tid     INTEGER NOT NULL,
                slot_position INTEGER NOT NULL DEFAULT 0,
                created_at    TEXT    NOT NULL DEFAULT (datetime('now'))
            ) STRICT;
            CREATE TABLE t_character_equip (
                character_id INTEGER NOT NULL,
                slot         INTEGER NOT NULL,
                equip_id     INTEGER NOT NULL UNIQUE,
                PRIMARY KEY (character_id, slot)
            ) STRICT;");
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
