using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 장비 분해 SQL을 실제 SQLite에 대고 검증한다(T-131). 분해한 장비만 지우고 재료를 같은 트랜잭션에 쓴다 —
/// 남의 장비나 경매에 잠긴 장비를 지우면 거래 원장이 가리키는 행이 사라진다.
/// </summary>
public class DisassembleEquipsRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public DisassembleEquipsRepositoryTest()
    {
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private long Count(string sql) => (long)_db.Query(sql)!;

    private static List<ItemChangeInfo> Shards(int count)
    {
        return new List<ItemChangeInfo>
        {
            new() { ItemId = 100050, Count = count, Kind = EItemChangeKind.Add, Container = EContainer.Inventory, Slot = 0 },
        };
    }

    [Fact]
    public async Task 분해한_장비만_지우고_재료를_쓴다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, enchant_grade, enchant_1) VALUES (10, 7, 1001, 1, 1103), (11, 7, 1001, 0, 0), (12, 8, 1001, 0, 0)");

        // 12는 남의 장비다 — 요청에 섞여 와도 지우지 않는다.
        await new DisassembleEquipsRepository(user, new List<long> { 10, 12 }, Shards(16))
            .ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_equip WHERE equip_id IN (11, 12)").ShouldBe(2);
        Count("SELECT COUNT(*) FROM t_user_equip").ShouldBe(2);
        Count("SELECT count FROM t_user_inventory WHERE user_id = 7 AND item_id = 100050").ShouldBe(16);
    }

    [Fact]
    public async Task 경매에_잠긴_장비는_지우지_않는다()
    {
        var user = new TestUserBuilder().Build(uid: 7);
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, auction_trade_id) VALUES (10, 7, 1001, 99)");

        await new DisassembleEquipsRepository(user, new List<long> { 10 }, Shards(1))
            .ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_equip").ShouldBe(1);
    }
}
