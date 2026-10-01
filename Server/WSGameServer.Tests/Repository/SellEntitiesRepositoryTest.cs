namespace WSGameServer;

/// <summary>
/// 개체 판매 SQL을 실제 SQLite에 대고 검증한다. 판 개체만 지우고 잔액을 같은 트랜잭션에 쓴다 —
/// 남의 개체나 경매에 잠긴 장비를 지우면 거래 원장이 가리키는 행이 사라진다.
/// </summary>
public class SellEntitiesRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public SellEntitiesRepositoryTest()
    {
        _db.CreatePlayerTables();
    }

    public void Dispose() => _db.Dispose();

    private long Count(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 판_개체만_지우고_잔액을_쓴다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_character (character_id, user_id, character_tid) VALUES (500, 7, 1001), (501, 7, 1001), (600, 8, 1001)");
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001), (11, 7, 1001), (12, 8, 1001)");

        // 600·12는 남의 개체다 — 요청에 섞여 와도 지우지 않는다.
        await new SellEntitiesRepository(user, new List<long> { 500, 600 }, new List<long> { 10, 12 }, gold: 1234)
            .ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character WHERE character_id IN (501, 600)").ShouldBe(2);
        Count("SELECT COUNT(*) FROM t_character").ShouldBe(2);
        Count("SELECT COUNT(*) FROM t_user_equip WHERE equip_id IN (11, 12)").ShouldBe(2);
        Count("SELECT COUNT(*) FROM t_user_equip").ShouldBe(2);
        Count("SELECT gold FROM t_user_currency WHERE user_id = 7").ShouldBe(1234);
    }

    [Fact]
    public async Task 경매에_잠긴_장비는_지우지_않는다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, auction_trade_id) VALUES (10, 7, 1001, 99)");

        await new SellEntitiesRepository(user, new List<long>(), new List<long> { 10 }, gold: 0)
            .ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_equip").ShouldBe(1);
    }
}
