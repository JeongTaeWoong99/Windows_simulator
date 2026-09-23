using Dapper;
using GameData;

namespace WSGameServer;

/// <summary>
/// 장비 매핑 SQL을 실제 SQLite에 대고 검증한다. 자동 이동은 매핑 행 최대 3개를 건드리므로
/// <b>한 트랜잭션</b>이어야 하고, UNIQUE(equip_id)에 걸리면 전부 되돌아가야 한다.
/// </summary>
public class EquipRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public EquipRepositoryTest()
    {
        _db.CreateEquipTables();
    }

    public void Dispose() => _db.Dispose();

    private static User NewUser() => new TestUserBuilder().Build(uid: 7);

    private long Count(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 지급은_개체_PK를_발급하고_창고_칸을_저장한다()
    {
        var user = NewUser();
        var repo = new GrantEquipRepository(user, equipTid: 1001, slotPosition: 3);

        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_equip WHERE user_id = 7 AND equip_tid = 1001 AND slot_position = 3").ShouldBe(1);
        repo.EquipId.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task 자동_이동은_이전_칸을_비우고_새_칸에_넣는다()
    {
        var user = NewUser();
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001), (11, 7, 1002)");
        // 장비 10은 캐릭터 500 무기 칸, 장비 11은 캐릭터 501 무기 칸
        _db.Execute("INSERT INTO t_character_equip VALUES (500, 1, 10), (501, 1, 11)");

        // 장비 10을 캐릭터 501 무기 칸으로 → 500 무기 칸 비움, 501 무기 칸의 11은 창고로, 10이 501 무기 칸에
        var repo = new SaveCharacterEquipRepository(user, new[]
        {
            new EquipMappingChange(500, EquipSlot.Weapon, 0),
            new EquipMappingChange(501, EquipSlot.Weapon, 10),
        });
        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character_equip").ShouldBe(1);
        Count("SELECT equip_id FROM t_character_equip WHERE character_id = 501 AND slot = 1").ShouldBe(10);
    }

    [Fact]
    public async Task 해제는_행을_지운다()
    {
        var user = NewUser();
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001)");
        _db.Execute("INSERT INTO t_character_equip VALUES (500, 4, 10)");

        await new SaveCharacterEquipRepository(user, new[] { new EquipMappingChange(500, EquipSlot.Gem, 0) })
            .ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character_equip").ShouldBe(0);
    }

    [Fact]
    public async Task 실패하면_전부_되돌아간다()
    {
        var user = NewUser();
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001)");
        _db.Execute("INSERT INTO t_character_equip VALUES (500, 1, 10)");

        // UNIQUE(equip_id)를 어긴다 — 같은 장비 10을 다른 캐릭터 칸에도 넣으려 한다(500 무기 칸을 비우는 변경이 없다).
        // 저장소는 넘겨받은 변경만 반영하고 equip_id 기준으로 알아서 지우지 않는다 — 메모리와 같은 규칙이어야 한다.
        var repo = new SaveCharacterEquipRepository(user, new[]
        {
            new EquipMappingChange(600, EquipSlot.Gem, 10),
        });

        await Should.ThrowAsync<Exception>(() => repo.ExecuteAsync(new DbConnection(_db.Connection)));

        Count("SELECT COUNT(*) FROM t_character_equip").ShouldBe(1);
        Count("SELECT character_id FROM t_character_equip").ShouldBe(500);
    }

    [Fact]
    public async Task 인챈트_컬럼은_기본값_0으로_읽힌다()
    {
        using var fx = new SqliteFixture();
        fx.CreateEquipTables();
        fx.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (7, 1, 1001)");

        var rows = (await fx.Connection.QueryAsync<UserEquipRow>(
            "SELECT equip_id, equip_tid, slot_position, enchant_grade, enchant_1, enchant_2, enchant_3 FROM t_user_equip")).ToList();

        rows.Count.ShouldBe(1);
        rows[0].enchant_grade.ShouldBe(0);
        rows[0].enchant_1.ShouldBe(0);
        rows[0].enchant_2.ShouldBe(0);
        rows[0].enchant_3.ShouldBe(0);
    }
}
