using MikaProtocol;

namespace WSGameServer;

/// <summary>칸 변경 저장이 탭에 맞는 테이블·컬럼을 고치는지 실제 SQLite로 지킨다. 교환은 두 행을 한 트랜잭션으로 쓴다.</summary>
public class StorageSlotRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public StorageSlotRepositoryTest()
    {
        _db.CreatePlayerTables();
    }

    public void Dispose() => _db.Dispose();

    private static User NewUser() => new TestUserBuilder().Build(uid: 7);

    private long Scalar(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 자원_교환은_두_행의_칸을_바꾼다()
    {
        _db.Execute("INSERT INTO t_user_inventory (user_id, container, item_id, count, slot) VALUES (7, 0, 101, 1, 0), (7, 0, 102, 1, 1)");
        var repo = new SaveStorageSlotsRepository(NewUser(), EStorageTab.Resource,
            new[] { new SlotChange(101, 1), new SlotChange(102, 0) });

        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT slot FROM t_user_inventory WHERE item_id = 101").ShouldBe(1);
    }

    [Fact]
    public async Task 장비_칸은_slot_position에_쓴다()
    {
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, slot_position) VALUES (10, 7, 1101, 0)");
        var repo = new SaveStorageSlotsRepository(NewUser(), EStorageTab.Equip, new[] { new SlotChange(10, 6) });

        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT slot_position FROM t_user_equip WHERE equip_id = 10").ShouldBe(6);
    }

    [Fact]
    public async Task 남의_캐릭터_칸은_바꾸지_않는다()
    {
        _db.Execute("INSERT INTO t_character (character_id, user_id, character_tid, slot) VALUES (40, 8, 1002, 0)");
        var repo = new SaveStorageSlotsRepository(NewUser(), EStorageTab.Character, new[] { new SlotChange(40, 5) });

        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        // 유저 7의 저장이 유저 8의 행을 건드리면 안 된다
        Scalar("SELECT slot FROM t_character WHERE character_id = 40").ShouldBe(0);
    }
}
