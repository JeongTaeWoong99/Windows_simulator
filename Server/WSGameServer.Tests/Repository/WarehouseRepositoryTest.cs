using MikaProtocol;

namespace WSGameServer;

/// <summary>창고 이동 저장과 재로그인 적재를 실제 SQLite로 지킨다 — 메모리만 맞고 DB가 어긋나면 재접속에서 물건이 사라진다.</summary>
public class WarehouseRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public WarehouseRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private static User NewUser() => new TestUserBuilder().Build(uid: 7);

    private long Scalar(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 자원_이동은_양쪽_보관함의_행을_쓴다()
    {
        _db.Execute("INSERT INTO t_user_inventory (user_id, container, item_id, count, slot) VALUES (7, 0, 10001, 10, 0)");
        var items = new List<ItemChangeInfo>
        {
            new() { Container = EContainer.Inventory, ItemId = 10001, Count = 0,  Slot = 0 },
            new() { Container = EContainer.Warehouse, ItemId = 10001, Count = 10, Slot = 4 },
        };

        await new WarehouseTransferRepository(NewUser(), EStorageTab.Resource, items, new List<EntityMove>())
            .ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT COUNT(*) FROM t_user_inventory WHERE user_id = 7 AND container = 0").ShouldBe(0);
        Scalar("SELECT slot FROM t_user_inventory WHERE user_id = 7 AND container = 1 AND item_id = 10001").ShouldBe(4);
    }

    [Fact]
    public async Task 장비_이동은_보관함과_칸을_함께_쓴다()
    {
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, slot_position) VALUES (10, 7, 1101, 0)");

        await new WarehouseTransferRepository(NewUser(), EStorageTab.Equip, new List<ItemChangeInfo>(),
                new List<EntityMove> { new(10, EContainer.Warehouse, 8) })
            .ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT container * 100 + slot_position FROM t_user_equip WHERE equip_id = 10").ShouldBe(108);
    }

    [Fact]
    public async Task 재로그인하면_창고의_자원과_캐릭터가_창고로_돌아온다()
    {
        const long uid = 7;
        _db.Execute($"INSERT INTO t_user_inventory (user_id, container, item_id, count, slot) VALUES ({uid}, 1, 10001, 5, 2)");
        _db.Execute($"INSERT INTO t_character (character_id, user_id, character_tid, slot) VALUES (40, {uid}, {User.DefaultCharacterTid}, 0)");
        _db.Execute($"INSERT INTO t_character (character_id, user_id, character_tid, container, slot) VALUES (41, {uid}, {User.DefaultCharacterTid}, 1, 6)");
        var b     = new TestUserBuilder();
        var user  = b.Build(uid);
        var login = new LoginRepository(user);

        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        b.Channel.SentOf<S_InventoryResponse>().Last().Items!
            .Select(i => (i.Container, i.ItemId, i.Count, i.Slot))
            .ShouldBe(new[] { (EContainer.Warehouse, 10001, 5, 2) });
        user.GetItemCount(10001).ShouldBe(0);
        user.TryGetCharacter(41, out _).ShouldBeFalse();
        b.Channel.SentOf<S_CharacterListResponse>().Last().Characters
            .Single(c => c.CharacterId == 41).Container.ShouldBe(EContainer.Warehouse);
    }

    [Fact]
    public async Task 창고에만_캐릭터가_있으면_시작_캐릭터를_또_주지_않는다()
    {
        const long uid = 7;
        _db.Execute($"INSERT INTO t_character (character_id, user_id, character_tid, container, slot) VALUES (41, {uid}, {User.DefaultCharacterTid}, 1, 0)");
        var b     = new TestUserBuilder();
        var user  = b.Build(uid);
        var login = new LoginRepository(user);

        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        b.DB.PostedOf<GrantCharacterRepository>().ShouldBeEmpty();
    }
}
