using Microsoft.Data.Sqlite;
using MikaProtocol;

namespace WSGameServer;

/// <summary>기존 보관 영약을 한 번만 환급하고, 설정·버프·보유 차감을 원자적으로 저장하는지 검증한다.</summary>
public class SlotAutoUseRepositoryTest : IDisposable
{
    private const int Elixir = 100045;
    private const long Uid = 7;
    private readonly SqliteFixture _db = new();
    private readonly IReadOnlyList<Migration> _migrations;

    public SlotAutoUseRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        _migrations = DbMigrator.LoadFrom(DbMigrator.DefaultDirectory);
        DbMigrator.Apply(_db.Connection, _migrations.Where(m => m.Version <= 14).ToList());
    }

    public void Dispose() => _db.Dispose();
    private long Query(string sql) => (long)_db.Query(sql)!;
    private Task Refund() => LegacySlotAutoUseRefund.RefundAsync(new DbConnection(_db.Connection), Uid, TestUserBuilder.Base);
    private void Migrate() => DbMigrator.Apply(_db.Connection, _migrations);
    private void OldStock(int slot, int count, long userId = Uid)
        => _db.Execute($"INSERT INTO t_user_slot_auto_use (user_id, slot_index, item_tid, count) VALUES ({userId}, {slot}, {Elixir}, {count});");

    [Fact]
    public async Task 기존_보관분은_설정을_ON으로_유지하고_인벤토리에_한_번만_돌려준다()
    {
        OldStock(0, 4);
        OldStock(9, 3);
        Migrate();

        await Refund();
        await Refund();

        Query("SELECT SUM(count) FROM t_user_inventory WHERE user_id = 7 AND item_id = 100045 AND container = 0").ShouldBe(7);
        Query("SELECT COUNT(*) FROM t_user_slot_auto_use WHERE user_id = 7").ShouldBe(2);
        Query("SELECT COUNT(*) FROM t_user_slot_auto_use_refund WHERE user_id = 7").ShouldBe(0);
        Query("SELECT COUNT(*) FROM t_user_mail").ShouldBe(0);
    }

    [Fact]
    public async Task 인벤토리에_일부만_들어가면_나머지는_우편으로_돌려준다()
    {
        OldStock(0, 5);
        var max = User.MaxStackOf(Elixir);
        _db.Execute($"INSERT INTO t_user_inventory (user_id, container, slot, item_id, count) VALUES (7, 0, 0, {Elixir}, {max - 2});");
        for (var slot = 1; slot < User.StorageCapacity; slot++)
        {
            _db.Execute($"INSERT INTO t_user_inventory (user_id, container, slot, item_id, count) VALUES (7, 0, {slot}, {900000 + slot}, 1);");
        }
        _db.Execute($"INSERT INTO t_user_inventory (user_id, container, slot, item_id, count) VALUES (7, 1, 0, {Elixir}, 1);");
        Migrate();

        await Refund();
        await Refund();

        Query("SELECT count FROM t_user_inventory WHERE user_id = 7 AND container = 0 AND slot = 0").ShouldBe(9999);
        Query("SELECT count FROM t_user_inventory WHERE user_id = 7 AND container = 1 AND slot = 0").ShouldBe(1);
        var mailbox = await new DbConnection(_db.Connection).QueryAsync<UserMailRow>("SELECT * FROM t_user_mail WHERE user_id = 7;");
        var mail = mailbox.ShouldHaveSingleItem();
        mail.template_tid.ShouldBe(MailCatalog.OverflowTemplateTid);
        MailAttachment.FromRow(mail).Items.ShouldBe(new[] { (Elixir, 3) });
    }

    [Fact]
    public async Task 환급은_다른_유저의_보관분을_건드리지_않는다()
    {
        OldStock(0, 4);
        OldStock(0, 8, userId: 8);
        Migrate();
        await Refund();
        Query("SELECT SUM(count) FROM t_user_slot_auto_use_refund WHERE user_id = 8").ShouldBe(8);
        Query("SELECT COUNT(*) FROM t_user_inventory WHERE user_id = 8").ShouldBe(0);
    }

    [Fact]
    public async Task 우편_환급에_실패하면_인벤토리_환급과_원장_삭제도_되돌린다()
    {
        OldStock(0, 10000);
        for (var slot = 1; slot < User.StorageCapacity; slot++)
        {
            _db.Execute($"INSERT INTO t_user_inventory (user_id, container, slot, item_id, count) VALUES (7, 0, {slot}, {900000 + slot}, 1);");
        }
        Migrate();
        _db.Execute("CREATE TRIGGER fail_refund BEFORE INSERT ON t_user_mail BEGIN SELECT RAISE(ABORT, 'refund failed'); END;");

        await Should.ThrowAsync<SqliteException>(Refund);

        Query("SELECT COUNT(*) FROM t_user_inventory WHERE user_id = 7 AND slot = 0").ShouldBe(0);
        Query("SELECT SUM(count) FROM t_user_slot_auto_use_refund WHERE user_id = 7").ShouldBe(10000);
        _db.Execute("DROP TRIGGER fail_refund;");
        await Refund();
        Query("SELECT SUM(count) FROM t_user_inventory WHERE user_id = 7 AND item_id = 100045").ShouldBe(9999);
        Query("SELECT COUNT(*) FROM t_user_mail WHERE user_id = 7").ShouldBe(1);
    }

    [Fact]
    public async Task 로그인_스냅샷에는_환급된_재고와_ON_설정이_같이_들어온다()
    {
        OldStock(0, 4);
        Migrate();
        _db.Execute("INSERT INTO t_character (character_id, user_id, character_tid) VALUES (500, 7, 1001);");
        var b = new TestUserBuilder();
        var user = b.Build(uid: Uid);
        var login = new LoginRepository(user);
        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        user.GetItemCount(Elixir).ShouldBe(4);
        b.Channel.SentOf<S_WorkStationSlotsResponse>().ShouldHaveSingleItem().Slots!
            .Single(s => s.SlotIndex == 0).AutoUse!.ShouldHaveSingleItem().On.ShouldBeTrue();
    }

    [Fact]
    public async Task ON_저장은_반복해도_한_행이고_OFF는_설정을_지운다()
    {
        Migrate();
        var user = new TestUserBuilder().Build(uid: Uid);
        var on = new SaveSlotAutoUseRepository(user, new[] { new SlotAutoUseChange(0, Elixir, true) });
        await on.ExecuteAsync(new DbConnection(_db.Connection));
        await on.ExecuteAsync(new DbConnection(_db.Connection));
        Query("SELECT COUNT(*) FROM t_user_slot_auto_use WHERE user_id = 7").ShouldBe(1);
        await new SaveSlotAutoUseRepository(user, new[] { new SlotAutoUseChange(0, Elixir, false) })
            .ExecuteAsync(new DbConnection(_db.Connection));
        Query("SELECT COUNT(*) FROM t_user_slot_auto_use WHERE user_id = 7").ShouldBe(0);
    }

    [Fact]
    public async Task 보유_차감_저장이_실패하면_ON_설정과_버프도_남기지_않는다()
    {
        Migrate();
        _db.Execute("CREATE TRIGGER fail_consume BEFORE INSERT ON t_user_inventory BEGIN SELECT RAISE(ABORT, 'consume failed'); END;");
        var user = new TestUserBuilder().Build(uid: Uid);
        var save = new SaveSlotAutoUseRepository(user, new[] { new SlotAutoUseChange(0, Elixir, true) },
            new[] { new SlotBuffChange(0, Elixir, 20) },
            new[] { new ItemChangeInfo { ItemId = Elixir, Count = 9, Slot = 0, Container = EContainer.Inventory } });

        await Should.ThrowAsync<SqliteException>(() => save.ExecuteAsync(new DbConnection(_db.Connection)));

        Query("SELECT COUNT(*) FROM t_user_slot_auto_use").ShouldBe(0);
        Query("SELECT COUNT(*) FROM t_user_slot_buff").ShouldBe(0);
    }
}
