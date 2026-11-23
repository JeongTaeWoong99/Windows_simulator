using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>개발 편의 치트가 지정한 캐릭터와 보관함에만 작용하는지 확인한다.</summary>
public class UserDeveloperCheatsTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    public UserDeveloperCheatsTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public async Task 레벨을_내리면_조건에_못_미치는_장비만_해제한다()
    {
        var builder = new TestUserBuilder();
        builder.Growth.LoadAll();
        var user = builder.Build();
        user.AdminLevel = 1;
        user.Uid = 7;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = 1001, level = 40, exp = 5 },
            new CharacterRow { character_id = 2, character_tid = 1001, level = 40, slot = 1 },
        });
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = 1, equip_tid = 201101, slot_position = 0 },
            new UserEquipRow { equip_id = 2, equip_tid = 2001, slot_position = 1 },
        }, new[]
        {
            new CharacterEquipRow { character_id = 1, slot = (int)EquipSlot.Weapon, equip_id = 1 },
            new CharacterEquipRow { character_id = 1, slot = (int)EquipSlot.Accessory1, equip_id = 2 },
        });

        Execute(user, ECheatCommand.SetCharacterLevel, 1, 10);

        user.TryGetCharacter(1, out var target).ShouldBeTrue();
        target.Level.ShouldBe(10);
        target.Exp.ShouldBe(0);
        user.TryGetCharacter(2, out var other).ShouldBeTrue();
        other.Level.ShouldBe(40);
        user.TryGetEquip(1, out var equip).ShouldBeTrue();
        equip.IsEquipped.ShouldBeFalse();
        user.TryGetEquip(2, out var accessory).ShouldBeTrue();
        accessory.IsEquipped.ShouldBeTrue();
        builder.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        builder.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem().Equips.ShouldHaveSingleItem().EquipId.ShouldBe(1);

        using var database = new SqliteFixture();
        database.CreateSchema();
        database.Execute("INSERT INTO t_user (user_id, provider_id, nickname) VALUES (7, 'test-user', 'test')");
        database.Execute("INSERT INTO t_character (character_id, user_id, character_tid, level, exp, slot) VALUES (1, 7, 1001, 40, 5, 0), (2, 7, 1001, 40, 0, 1)");
        database.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, slot_position) VALUES (1, 7, 201101, 0), (2, 7, 2001, 1)");
        database.Execute("INSERT INTO t_character_equip (character_id, slot, equip_id) VALUES (1, 1, 1), (1, 2, 2)");
        await builder.DB.PostedOf<SetCheatCharacterLevelsRepository>().ShouldHaveSingleItem().ExecuteAsync(new DbConnection(database.Connection));
        database.Query("SELECT level FROM t_character WHERE character_id = 1").ShouldBe(10L);
        database.Query("SELECT exp FROM t_character WHERE character_id = 1").ShouldBe(0L);
        database.Query("SELECT equip_id FROM t_character_equip WHERE character_id = 1").ShouldBe(2L);
    }

    [Fact]
    public void 창고_자원만_비우면_잠금과_인벤토리_수량은_유지된다()
    {
        var builder = new TestUserBuilder();
        var user = builder.Build();
        user.AdminLevel = 1;
        user.LoadInventory(new[]
        {
            new InventoryRow { item_id = 10001, count = 2, slot = 0, container = (int)EContainer.Inventory },
            new InventoryRow { item_id = 10001, count = 3, slot = 0, container = (int)EContainer.Warehouse },
            new InventoryRow { item_id = 20001, count = 5, slot = 1, container = (int)EContainer.Warehouse },
        });
        user.LoadItemLocks(new[] { new UserItemLockRow { item_id = 20001 } });

        Execute(user, ECheatCommand.ClearItems, 1, 1);

        user.GetItemCount(10001).ShouldBe(2);
        user.GetItemCount(20001).ShouldBe(5);
        builder.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 상자_시뮬레이션은_잘못된_개수를_거절한다()
    {
        var builder = new TestUserBuilder();
        var user = builder.Build();
        user.AdminLevel = 1;

        Execute(user, ECheatCommand.SimulateBoxOpen, 100081, 0);

        builder.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        builder.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 레벨_일괄_설정은_창고_캐릭터까지_만렙으로_맞춘다()
    {
        var builder = new TestUserBuilder();
        builder.Growth.LoadAll();
        var user = builder.Build();
        user.AdminLevel = 1;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = 1001, level = 2 },
            new CharacterRow { character_id = 2, character_tid = 1001, level = 3, container = (int)EContainer.Warehouse },
        });

        Execute(user, ECheatCommand.SetCharacterLevel, 0, long.MaxValue);

        user.TryGetCharacter(2, out var stored).ShouldBeTrue();
        stored.Level.ShouldBe(builder.Growth.MaxLevel);
        user.TryGetCharacter(1, out var carried).ShouldBeTrue();
        carried.Level.ShouldBe(builder.Growth.MaxLevel);
        builder.Channel.SentOf<S_CharacterSyncResponse>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task 보관함을_비우면_보호_대상은_메모리와_DB에_모두_남는다()
    {
        var builder = new TestUserBuilder();
        var user = builder.Build();
        user.AdminLevel = 1;
        user.Uid = 7;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = 1001, level = 3 },
            new CharacterRow { character_id = 2, character_tid = 1001, is_locked = 1, slot = 1 },
            new CharacterRow { character_id = 3, character_tid = 1001, container = (int)EContainer.Warehouse },
        });
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = 1, equip_tid = 1001 },
            new UserEquipRow { equip_id = 2, equip_tid = 1001, is_locked = 1, slot_position = 1 },
            new UserEquipRow { equip_id = 3, equip_tid = 1001, container = (int)EContainer.Warehouse },
        }, new[] { new CharacterEquipRow { character_id = 1, slot = (int)EquipSlot.Weapon, equip_id = 1 } });
        user.LoadInventory(new[] { new InventoryRow { item_id = 10001, count = 2 } });

        Execute(user, ECheatCommand.ClearItems, 0, 2);

        user.OwnedCharacterCount.ShouldBe(2);
        user.TryGetCharacter(3, out _).ShouldBeFalse();
        user.TryGetEquip(3, out _).ShouldBeFalse();
        builder.Channel.SentOf<S_EquipListResponse>().ShouldHaveSingleItem().Equips.Select(equip => equip.EquipId).ShouldBe(new[] { 1L, 2L }, ignoreOrder: true);
        user.GetItemCount(10001).ShouldBe(0);

        using var database = new SqliteFixture();
        database.CreateSchema();
        database.Execute("INSERT INTO t_user (user_id, provider_id, nickname) VALUES (7, 'test-user', 'test')");
        database.Execute("INSERT INTO t_character (character_id, user_id, character_tid, container, slot) VALUES (1, 7, 1001, 0, 0), (2, 7, 1001, 0, 1), (3, 7, 1001, 1, 0)");
        database.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid, container, slot_position) VALUES (1, 7, 1001, 0, 0), (2, 7, 1001, 0, 1), (3, 7, 1001, 1, 0)");
        database.Execute("INSERT INTO t_character_equip (character_id, slot, equip_id) VALUES (1, 1, 1)");
        database.Execute("INSERT INTO t_user_inventory (user_id, container, slot, item_id, count) VALUES (7, 0, 0, 10001, 2)");
        await builder.DB.PostedOf<ClearCheatItemsRepository>().ShouldHaveSingleItem().ExecuteAsync(new DbConnection(database.Connection));
        database.Query("SELECT COUNT(*) FROM t_character WHERE user_id = 7").ShouldBe(2L);
        database.Query("SELECT COUNT(*) FROM t_user_equip WHERE user_id = 7").ShouldBe(2L);
        database.Query("SELECT COUNT(*) FROM t_user_inventory WHERE user_id = 7").ShouldBe(0L);
        database.Query("SELECT equip_id FROM t_character_equip WHERE character_id = 1").ShouldBe(1L);
    }

    [Fact]
    public void 비우기를_반복해도_마지막_캐릭터는_남는다()
    {
        var builder = new TestUserBuilder();
        var user = builder.Build();
        user.AdminLevel = 1;
        user.LoadCharacters(new[] { new CharacterRow { character_id = 1, character_tid = 1001, level = 1 } });

        Execute(user, ECheatCommand.ClearItems, 3, 2);
        Execute(user, ECheatCommand.ClearItems, 3, 2);

        user.OwnedCharacterCount.ShouldBe(1);
        builder.DB.Posted.ShouldBeEmpty();
    }

    private static void Execute(User user, ECheatCommand command, long arg1, long arg2)
        => user.ExecuteCheat(new C_CheatRequest { Command = command, Arg1 = arg1, Arg2 = arg2 }, Now);
}
