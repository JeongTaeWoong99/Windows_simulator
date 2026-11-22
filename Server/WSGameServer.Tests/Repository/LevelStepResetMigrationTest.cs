namespace WSGameServer;

/// <summary>
/// 채취 단계 개편(T-208) 마이그레이션 <c>0017</c>이 옛 데이터를 지우는지 지킨다 —
/// 산업 레벨(슬롯 · 개척 특성)은 Lv1로, 옛 재질 상자 15종(100019~100033)은 저장된 모든 곳에서 뺀다.
/// 틀리면 옛 레벨 값(2~5)이 새 단계(1 · 20 · 40 …)와 섞여 슬롯이 없는 레벨로 돌거나, 표에 없는 상자가 남는다.
/// </summary>
public class LevelStepResetMigrationTest : IDisposable
{
    private const int ResetVersion = 17;

    private readonly SqliteFixture _db = new();

    public LevelStepResetMigrationTest()
    {
        var all = DbMigrator.LoadFrom(DbMigrator.DefaultDirectory);
        DbMigrator.Apply(_db.Connection, all.Where(m => m.Version < ResetVersion).ToList());
    }

    public void Dispose() => _db.Dispose();

    private void ApplyReset() => DbMigrator.Apply(_db.Connection, DbMigrator.LoadFrom(DbMigrator.DefaultDirectory));

    private long Count(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public void 슬롯의_산업_레벨은_모두_1이_된다()
    {
        _db.Execute("INSERT INTO t_user_workstation_slot (user_id, slot_index, industry, industry_level, character_id) VALUES (1, 0, 2, 3, 10), (1, 1, 4, 1, 11), (2, 0, 5, 5, 12)");

        ApplyReset();

        Count("SELECT COUNT(*) FROM t_user_workstation_slot WHERE industry_level <> 1").ShouldBe(0);
        Count("SELECT COUNT(*) FROM t_user_workstation_slot").ShouldBe(3);
    }

    [Fact]
    public void 개척_특성만_기본_레벨로_돌아간다()
    {
        // 101~105 = 개척 · 201 = 농사 속도. 기본 레벨은 행이 없는 상태다.
        _db.Execute("INSERT INTO t_user_trait (user_id, user_trait_tid, level) VALUES (1, 101, 4), (1, 105, 2), (1, 201, 3)");

        ApplyReset();

        Count("SELECT COUNT(*) FROM t_user_trait WHERE user_trait_tid BETWEEN 101 AND 105").ShouldBe(0);
        Count("SELECT level FROM t_user_trait WHERE user_trait_tid = 201").ShouldBe(3);
    }

    [Fact]
    public void 인벤토리와_창고의_옛_상자를_지운다()
    {
        // 100019 · 100033 = 옛 상자 양 끝 · 100034 = 은빛 보물 지도(남아야 한다)
        _db.Execute("INSERT INTO t_user_inventory (user_id, container, slot, item_id, count) VALUES (1, 0, 0, 100019, 5), (1, 1, 3, 100033, 2), (1, 0, 1, 100034, 7)");
        _db.Execute("INSERT INTO t_user_item_lock (user_id, item_id) VALUES (1, 100025), (1, 100034)");

        ApplyReset();

        Count("SELECT COUNT(*) FROM t_user_inventory").ShouldBe(1);
        Count("SELECT item_id FROM t_user_inventory").ShouldBe(100034);
        Count("SELECT item_id FROM t_user_item_lock").ShouldBe(100034);
    }

    [Fact]
    public void 우편_첨부에서_옛_상자만_뺀다()
    {
        _db.Execute("""
            INSERT INTO t_user_mail (mail_id, user_id, template_tid, gold, items) VALUES
                (1, 1, 1, 1000, '[[100019,3]]'),
                (2, 1, 2, 0,    '[[100022,1],[20001,40],[100033,2]]'),
                (3, 1, 2, 0,    '[[20001,5]]')
            """);

        ApplyReset();

        _db.Query("SELECT items FROM t_user_mail WHERE mail_id = 1").ShouldBe("[]");
        _db.Query("SELECT items FROM t_user_mail WHERE mail_id = 2").ShouldBe("[[20001,40]]");
        _db.Query("SELECT items FROM t_user_mail WHERE mail_id = 3").ShouldBe("[[20001,5]]");
        Count("SELECT gold FROM t_user_mail WHERE mail_id = 1").ShouldBe(1000);
    }

    [Fact]
    public void 경매에_걸린_옛_상자는_반환_처리되고_에스크로가_빈다()
    {
        // 1 = 옛 상자 매물 · 2 = 자원 매물(그대로)
        _db.Execute("""
            INSERT INTO t_auction_trade (trade_id, seller_id, kind, tid, count, unit_price, listing_fee) VALUES
                (1, 1, 1, 100020, 4, 100, 10),
                (2, 1, 1, 20001, 9, 5, 1)
            """);

        ApplyReset();

        Count("SELECT state * 10 + count FROM t_auction_trade WHERE trade_id = 1").ShouldBe(30);
        Count("SELECT state * 10 + count FROM t_auction_trade WHERE trade_id = 2").ShouldBe(19);
    }
}
