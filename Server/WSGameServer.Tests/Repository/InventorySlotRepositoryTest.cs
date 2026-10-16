using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 칸 저장 SQL이 칸을 함께 쓰고 로그인이 되읽는지 실제 SQLite로 지킨다 — 칸을 빼먹으면 메모리는 맞는데 재로그인에서 자리가 사라진다.
/// </summary>
public class InventorySlotRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public InventorySlotRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private static User NewUser() => new TestUserBuilder().Build(uid: 7);

    private long Scalar(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 지급은_칸을_저장한다()
    {
        var change = new ItemChangeInfo { ItemId = 101, Count = 3, Kind = EItemChangeKind.Add, Slot = 5 };

        await new AddItemRepository(NewUser(), new List<ItemChangeInfo> { change }).ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT slot FROM t_user_inventory WHERE user_id = 7 AND container = 0 AND item_id = 101").ShouldBe(5);
    }

    [Fact]
    public async Task 같은_자원을_두_칸에_나눠_저장한다()
    {
        // MaxStack을 넘긴 지급 — 칸 0은 꽉 차고 칸 1에 이어 쌓였다(T-160)
        var changes = new List<ItemChangeInfo>
        {
            new() { ItemId = 101, Count = 9999, Kind = EItemChangeKind.Update, Slot = 0 },
            new() { ItemId = 101, Count = 5,    Kind = EItemChangeKind.Add,    Slot = 1 },
        };

        await new AddItemRepository(NewUser(), changes).ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT SUM(count) FROM t_user_inventory WHERE user_id = 7 AND item_id = 101").ShouldBe(10004);
    }

    [Fact]
    public async Task 칸에_다른_자원을_쓰면_그_칸을_덮어쓴다()
    {
        // 교환 — 칸 0에 있던 101 자리에 102가 들어왔다
        _db.Execute("INSERT INTO t_user_inventory (user_id, container, item_id, count, slot) VALUES (7, 0, 101, 1, 0)");
        var change = new ItemChangeInfo { ItemId = 102, Count = 4, Kind = EItemChangeKind.Update, Slot = 0 };

        await new SaveItemChangesRepository(NewUser(), new List<ItemChangeInfo> { change }).ExecuteAsync(new DbConnection(_db.Connection));

        Scalar("SELECT item_id FROM t_user_inventory WHERE user_id = 7 AND container = 0 AND slot = 0").ShouldBe(102);
    }

    [Fact]
    public async Task 다_쓴_자원은_그_보관함의_행만_지운다()
    {
        _db.Execute("INSERT INTO t_user_inventory (user_id, container, item_id, count, slot) VALUES (7, 0, 101, 1, 0), (7, 1, 101, 9, 0)");
        var change = new ItemChangeInfo { ItemId = 101, Count = 0, Kind = EItemChangeKind.Remove, Slot = 0 };

        await new SaveItemChangesRepository(NewUser(), new List<ItemChangeInfo> { change }).ExecuteAsync(new DbConnection(_db.Connection));

        // 인벤토리 행만 지워지고 창고(container 1)의 같은 자원은 남는다
        Scalar("SELECT COUNT(*) FROM t_user_inventory WHERE user_id = 7 AND item_id = 101").ShouldBe(1);
    }

    [Fact]
    public async Task 재로그인하면_자원과_캐릭터의_칸이_그대로다()
    {
        const long uid = 7;
        _db.Execute($"INSERT INTO t_user_inventory (user_id, container, item_id, count, slot) VALUES ({uid}, 0, 10001, 1, 9)");
        _db.Execute($"INSERT INTO t_character (character_id, user_id, character_tid, slot) VALUES (40, {uid}, {User.DefaultCharacterTid}, 3)");
        var b    = new TestUserBuilder();
        var user = b.Build(uid);
        var login = new LoginRepository(user);

        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        b.Channel.SentOf<S_InventoryResponse>().Last().Items!.Single().Slot.ShouldBe(9);
        user.TryGetCharacter(40, out var character).ShouldBeTrue();
        character.Slot.ShouldBe(3);
    }
}
