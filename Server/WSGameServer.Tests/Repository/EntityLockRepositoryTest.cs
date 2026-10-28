using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 잠금 SQL을 실제 SQLite에 대고 검증한다(T-137). 자기 개체 한 줄만 바꾸고, 경매에 넘어간 개체는 건드리지 않는다 —
/// 남의 개체를 잠그면 그 사람이 팔지 못하게 된다.
/// </summary>
public class EntityLockRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public EntityLockRepositoryTest()
    {
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private long Count(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 자기_캐릭터_한_줄만_잠그고_푼다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_character (character_id, user_id, character_tid) VALUES (500, 7, 1001), (501, 7, 1001), (600, 8, 1001)");

        await new EntityLockRepository(user, ELockTarget.Character, 500, locked: true).ExecuteAsync(new DbConnection(_db.Connection));
        await new EntityLockRepository(user, ELockTarget.Character, 600, locked: true).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character WHERE is_locked = 1").ShouldBe(1);
        Count("SELECT is_locked FROM t_character WHERE character_id = 500").ShouldBe(1);

        await new EntityLockRepository(user, ELockTarget.Character, 500, locked: false).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character WHERE is_locked = 1").ShouldBe(0);
    }

    [Fact]
    public async Task 경매에_넘어간_장비는_잠그지_않는다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, auction_trade_id) VALUES (10, 7, 1001, 0), (11, 7, 1001, 99)");

        await new EntityLockRepository(user, ELockTarget.Equip, 10, locked: true).ExecuteAsync(new DbConnection(_db.Connection));
        await new EntityLockRepository(user, ELockTarget.Equip, 11, locked: true).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_equip WHERE is_locked = 1").ShouldBe(1);
        Count("SELECT is_locked FROM t_user_equip WHERE equip_id = 10").ShouldBe(1);
    }

    [Fact]
    public async Task 자원은_자기_종류_한_줄만_잠그고_두_번_잠가도_한_줄이다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_user_item_lock (user_id, item_id) VALUES (8, 10001)");

        await new EntityLockRepository(user, ELockTarget.Resource, 10001, locked: true).ExecuteAsync(new DbConnection(_db.Connection));
        await new EntityLockRepository(user, ELockTarget.Resource, 10001, locked: true).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_item_lock WHERE user_id = 7 AND item_id = 10001").ShouldBe(1);

        await new EntityLockRepository(user, ELockTarget.Resource, 10001, locked: false).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_item_lock WHERE user_id = 7").ShouldBe(0);
        Count("SELECT COUNT(*) FROM t_user_item_lock WHERE user_id = 8").ShouldBe(1);
    }

    [Fact]
    public async Task 아티팩트는_자기_번호_한_줄만_잠그고_자원_잠금과_섞이지_않는다()
    {
        var user = new TestUserBuilder().Build(uid: 7);

        await new EntityLockRepository(user, ELockTarget.Artifact, 1, locked: true).ExecuteAsync(new DbConnection(_db.Connection));
        await new EntityLockRepository(user, ELockTarget.Artifact, 1, locked: true).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_artifact_lock WHERE user_id = 7 AND artifact_tid = 1").ShouldBe(1);
        Count("SELECT COUNT(*) FROM t_user_item_lock").ShouldBe(0);

        await new EntityLockRepository(user, ELockTarget.Artifact, 1, locked: false).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_artifact_lock").ShouldBe(0);
    }
}
