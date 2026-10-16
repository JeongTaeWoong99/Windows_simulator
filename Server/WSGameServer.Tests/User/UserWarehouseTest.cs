using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 인벤토리 ↔ 창고 이동(T-107 · 이슈 #58). 놓은 칸이 비었으면 이동, 차 있으면 스왑.
/// 자원은 같은 자원이면 합치고, 다른 자원 칸이면 전량일 때만 스왑한다. 거절이면 아무것도 바꾸지 않는다.
/// </summary>
public class UserWarehouseTest
{
    public UserWarehouseTest() => GameTableFixture.EnsureLoaded();

    private const int  FishTid  = 10001;   // MaxStack 9999
    private const int  CarpTid  = 10002;
    private const int  SwordTid = 9101;
    private const long Sword    = 11;
    private const long CharA    = 500;
    private const long CharB    = 501;
    private const long StoredC  = 502;

    private static (User User, TestUserBuilder B) NewUser(
        InventoryRow[]? items = null, CharacterRow[]? characters = null, UserEquipRow[]? equips = null,
        params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", GlobalRarity = GlobalRarity.Common, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });

        var user = b.Build();
        user.LoadInventory(items ?? Array.Empty<InventoryRow>());
        user.LoadCharacters(characters ?? new[]
        {
            new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, slot = 0 },
            new CharacterRow { character_id = CharB, character_tid = 1001, level = 1, slot = 1 },
        });
        user.LoadEquips(equips ?? Array.Empty<UserEquipRow>(), worn);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static InventoryRow Item(int tid, int count, int slot, EContainer container = EContainer.Inventory)
        => new() { item_id = tid, count = count, slot = slot, container = (int)container };

    private static S_WarehouseTransferResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_WarehouseTransferResponse>().Last();

    private static (EContainer, long, int, int)[] Changes(TestUserBuilder b)
        => Last(b).Changes.Select(c => (c.Container, c.Key, c.Slot, c.Count)).ToArray();

    // ── 자원 ──

    [Fact]
    public void 빈_창고_칸에_자원_일부를_놓으면_양쪽에_나뉜다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 3, 4);

        Changes(b).ShouldBe(new[]
        {
            (EContainer.Inventory, (long)FishTid, 0, 6),
            (EContainer.Warehouse, (long)FishTid, 3, 4),
        });
    }

    [Fact]
    public void 같은_자원이_받는_쪽에_있으면_놓은_칸과_무관하게_합친다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0), Item(FishTid, 5, 7, EContainer.Warehouse) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 2, 10);

        Changes(b).ShouldBe(new[]
        {
            (EContainer.Inventory, (long)FishTid, 0, 0),
            (EContainer.Warehouse, (long)FishTid, 7, 15),
        });
    }

    [Fact]
    public void 다른_자원_칸에_전량을_놓으면_스왑한다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0), Item(CarpTid, 3, 4, EContainer.Warehouse) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 4, 10);

        user.SendInventory();
        b.Channel.SentOf<S_InventoryResponse>().Last().Items!
            .Select(i => (i.Container, i.ItemId, i.Slot, i.Count))
            .ShouldBe(new[] { (EContainer.Inventory, CarpTid, 0, 3), (EContainer.Warehouse, FishTid, 4, 10) }, ignoreOrder: true);
    }

    [Fact]
    public void 다른_자원_칸에_일부를_놓으면_거절하고_아무것도_바꾸지_않는다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0), Item(CarpTid, 3, 4, EContainer.Warehouse) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 4, 9);

        Last(b).Result.ShouldBe(EResultCode.WarehousePartialSwap);
        user.GetItemCount(FishTid).ShouldBe(10);
        b.DB.PostedOf<WarehouseTransferRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 스왑으로_돌아오는_자원이_이미_있으면_그_칸에_합친다()
    {
        // 인벤토리: 붕어(0) · 잉어(1) / 창고: 잉어(4) — 붕어를 창고 잉어 칸에 전량 놓는다
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0), Item(CarpTid, 2, 1), Item(CarpTid, 3, 4, EContainer.Warehouse) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 4, 10);

        user.SendInventory();
        b.Channel.SentOf<S_InventoryResponse>().Last().Items!
            .Select(i => (i.Container, i.ItemId, i.Slot, i.Count))
            .ShouldBe(new[] { (EContainer.Inventory, CarpTid, 1, 5), (EContainer.Warehouse, FishTid, 4, 10) }, ignoreOrder: true);
    }

    [Fact]
    public void 합치면_최대_수량을_넘으면_거절한다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 9000, 0), Item(FishTid, 1000, 2, EContainer.Warehouse) });

        user.TransferWarehouse(EContainer.Warehouse, EStorageTab.Resource, 2, 0, 1000);

        Last(b).Result.ShouldBe(EResultCode.StorageOverMaxStack);
        b.DB.PostedOf<WarehouseTransferRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 가진_것보다_많이_옮기면_거절한다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 3, 0) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 0, 4);

        Last(b).Result.ShouldBe(EResultCode.NotEnoughItem);
    }

    [Fact]
    public void 자원_이동은_양쪽_변경을_한_번에_저장한다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, 3, 4);

        b.DB.PostedOf<WarehouseTransferRepository>().Single().Items
            .Select(i => (i.Container, i.ItemId, i.Count, i.Slot))
            .ShouldBe(new[] { (EContainer.Inventory, FishTid, 6, 0), (EContainer.Warehouse, FishTid, 4, 3) });
    }

    // ── 칸 ──

    [Fact]
    public void 창고_칸_수_밖에는_놓을_수_없다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 10, 0) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Resource, 0, User.WarehouseCapacity, 10);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotOutOfRange);
    }

    [Fact]
    public void 빈_칸에서는_옮길_수_없다()
    {
        var (user, b) = NewUser();

        user.TransferWarehouse(EContainer.Warehouse, EStorageTab.Resource, 0, 0, 1);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotEmpty);
    }

    [Fact]
    public void 창고_안에서도_자리를_옮긴다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 1, 0, EContainer.Warehouse) });

        user.MoveStorageSlot(EContainer.Warehouse, EStorageTab.Resource, 0, 49);

        b.Channel.SentOf<S_StorageSlotsResponse>().Last().Slots.Single().Slot.ShouldBe(49);
        b.DB.PostedOf<SaveStorageSlotsRepository>().ShouldHaveSingleItem();
    }

    [Fact]
    public void 지급_대기_칸으로는_꺼낼_수_없다()
    {
        var (user, b) = NewUser(characters: new[]
        {
            new CharacterRow { character_id = CharA,   character_tid = 1001, level = 1, slot = 0 },
            new CharacterRow { character_id = StoredC, character_tid = 1001, level = 1, slot = 0, container = (int)EContainer.Warehouse },
        });
        user.GrantGachaCharacters(new[] { 1001 });   // 인벤토리 칸 1 예약

        user.TransferWarehouse(EContainer.Warehouse, EStorageTab.Character, 0, 1, 0);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotPending);
    }

    // ── 캐릭터 · 장비 ──

    [Fact]
    public void 캐릭터를_창고로_보내면_창고_칸에_선다()
    {
        var (user, b) = NewUser();

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Character, 0, 5, 0);

        Changes(b).ShouldBe(new[] { (EContainer.Warehouse, CharA, 5, 1) });
        b.DB.PostedOf<WarehouseTransferRepository>().Single().Entities
            .ShouldBe(new[] { new EntityMove(CharA, EContainer.Warehouse, 5) });
    }

    [Fact]
    public void 찬_창고_칸에_놓으면_두_캐릭터가_자리를_맞바꾼다()
    {
        var (user, b) = NewUser(characters: new[]
        {
            new CharacterRow { character_id = CharA,   character_tid = 1001, level = 1, slot = 0 },
            new CharacterRow { character_id = StoredC, character_tid = 1001, level = 1, slot = 2, container = (int)EContainer.Warehouse },
        });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Character, 0, 2, 0);

        Changes(b).ShouldBe(new[] { (EContainer.Warehouse, CharA, 2, 1), (EContainer.Inventory, StoredC, 0, 1) });
        user.TryGetCharacter(StoredC, out var back).ShouldBeTrue();
        back.Slot.ShouldBe(0);
    }

    [Fact]
    public void 창고의_캐릭터도_스냅샷에_창고로_실린다()
    {
        var (user, b) = NewUser(characters: new[]
        {
            new CharacterRow { character_id = CharA,   character_tid = 1001, level = 1, slot = 0 },
            new CharacterRow { character_id = StoredC, character_tid = 1001, level = 1, slot = 3, container = (int)EContainer.Warehouse },
        });

        user.SendCharacters();

        b.Channel.SentOf<S_CharacterListResponse>().Last().Characters
            .Select(c => (c.CharacterId, c.Container, c.Slot))
            .ShouldBe(new[] { (CharA, EContainer.Inventory, 0), (StoredC, EContainer.Warehouse, 3) });
    }
}
