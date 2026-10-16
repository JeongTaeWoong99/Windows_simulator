using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 창고는 놓인 자리일 뿐이다(T-164 · 이슈 #74). 창고의 캐릭터·장비·자원도 꺼내지 않고 모든 기능에 쓰고,
/// 배치·착용한 채 맡겨도 상태가 그대로다. 틀리면 창고에 둔 것이 "없는 것"으로 처리돼 착용·배치가 풀린다.
/// </summary>
public class UserWarehouseUseTest
{
    public UserWarehouseUseTest() => GameTableFixture.EnsureLoaded();

    private const int  FishTid  = 10001;
    private const int  SwordTid = 9101;
    private const long Sword    = 11;
    private const long CharA    = 500;
    private const long StoredC  = 502;
    private const int  Inv      = (int)EContainer.Inventory;
    private const int  Wh       = (int)EContainer.Warehouse;

    private static readonly DateTime Base = TestUserBuilder.Base;

    private static TestUserBuilder Builder()
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", GlobalRarity = GlobalRarity.Common, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });
        return b;
    }

    private static (User User, TestUserBuilder B) NewUser(
        InventoryRow[]? items = null, CharacterRow[]? characters = null, UserEquipRow[]? equips = null,
        params CharacterEquipRow[] worn)
    {
        var b    = Builder();
        var user = b.Build();
        user.LoadInventory(items ?? Array.Empty<InventoryRow>());
        user.LoadCharacters(characters ?? new[]
        {
            Character(CharA, 0),
            Character(StoredC, 0, Wh),
        });
        user.LoadEquips(equips ?? Array.Empty<UserEquipRow>(), worn);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static CharacterRow Character(long id, int slot, int container = Inv)
        => new() { character_id = id, character_tid = 1001, level = 1, slot = slot, container = container };

    private static UserEquipRow SwordRow(int container = Inv)
        => new() { equip_id = Sword, equip_tid = SwordTid, slot_position = 0, container = container };

    private static InventoryRow Item(int tid, int count, int slot, int container = Inv)
        => new() { item_id = tid, count = count, slot = slot, container = container };

    private static CharacterEquipRow Wearing(long characterId)
        => new() { character_id = characterId, slot = (int)EquipSlot.Weapon, equip_id = Sword };

    // ── 맡기기 ──

    [Fact]
    public void 배치_중인_캐릭터도_창고로_보내고_배치는_그대로다()
    {
        var (user, b) = NewUser(characters: new[] { Character(CharA, 0) });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Farming, CharA, Base, null) });

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Character, 0, 0, 0);

        b.Channel.SentOf<S_WarehouseTransferResponse>().Last().Result.ShouldBe(EResultCode.Ok);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.CharacterId.ShouldBe(CharA);
    }

    [Fact]
    public void 장착_중인_장비도_창고로_보내고_착용은_그대로다()
    {
        var (user, b) = NewUser(equips: new[] { SwordRow() }, worn: Wearing(CharA));

        user.TransferWarehouse(EContainer.Inventory, EStorageTab.Equip, 0, 0, 0);

        b.Channel.SentOf<S_WarehouseTransferResponse>().Last().Result.ShouldBe(EResultCode.Ok);
        user.TryGetEquip(Sword, out var sword).ShouldBeTrue();
        sword.EquippedCharacterId.ShouldBe(CharA);
    }

    // ── 꺼내지 않고 쓰기 ──

    [Fact]
    public void 창고의_캐릭터를_작업에_배치한다()
    {
        var (user, b) = NewUser();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.None, 0, Base) });

        user.AssignWorkStation(0, IndustryType.Excavation, StoredC, Base);

        b.Channel.SentOf<S_WorkStationAssignResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 창고의_장비를_창고의_캐릭터에_장착한다()
    {
        var (user, b) = NewUser(equips: new[] { SwordRow(Wh) });

        user.TryEquip(StoredC, Sword, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 창고_장비의_변경은_창고_칸으로_알린다()
    {
        // 컨테이너를 잘못 실으면 클라는 장비가 인벤토리로 옮겨진 줄 안다
        var (user, b) = NewUser(equips: new[] { SwordRow(Wh) });

        user.TryEquip(CharA, Sword, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipSyncResponse>().Single().Equips.Single().Container.ShouldBe(EContainer.Warehouse);
    }

    [Fact]
    public void 창고의_캐릭터를_잠근다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Character, StoredC, true);

        b.Channel.SentOf<S_EntityLockResponse>().Single().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 창고의_캐릭터를_팔면_창고에서_사라진다()
    {
        var (user, b) = NewUser();

        user.TrySellEntities(new[] { StoredC }, Array.Empty<long>());

        b.Channel.SentOf<S_EntitySellResponse>().Single().Result.ShouldBe(EResultCode.Ok);
        user.TryGetCharacter(StoredC, out _).ShouldBeFalse();
    }

    [Fact]
    public void 창고에_캐릭터가_남으면_인벤토리의_마지막_캐릭터를_판다()
    {
        // 인벤토리 1명 + 창고 1명 — 팔아도 1명이 남는다
        var (user, b) = NewUser();

        user.TrySellEntities(new[] { CharA }, Array.Empty<long>());

        b.Channel.SentOf<S_EntitySellResponse>().Single().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 창고까지_합쳐_마지막_캐릭터는_팔_수_없다()
    {
        var (user, b) = NewUser(characters: new[] { Character(StoredC, 0, Wh) });

        user.TrySellEntities(new[] { StoredC }, Array.Empty<long>());

        b.Channel.SentOf<S_EntitySellResponse>().Single().Result.ShouldBe(EResultCode.SellLastCharacter);
    }

    // ── 자원 ──

    [Fact]
    public void 자원_수량은_창고까지_합쳐_센다()
    {
        var (user, _) = NewUser(new[] { Item(FishTid, 3, 0), Item(FishTid, 5, 2, Wh) });

        user.GetItemCount(FishTid).ShouldBe(8);
    }

    [Fact]
    public void 자원은_인벤토리에서_먼저_빼고_모자란_만큼_창고에서_뺀다()
    {
        // 인벤토리 3 + 창고 5 에서 6 → 인벤토리 0(칸 비움) · 창고 2
        var (user, _) = NewUser(new[] { Item(FishTid, 3, 0), Item(FishTid, 5, 2, Wh) });

        user.TryConsumeItems(new Dictionary<int, int> { [FishTid] = 6 }, out var changes).ShouldBeTrue();

        changes.Select(c => (c.Container, c.Count, c.Slot)).ShouldBe(new[]
        {
            (EContainer.Inventory, 0, 0),
            (EContainer.Warehouse, 2, 2),
        });
    }

    [Fact]
    public void 창고까지_합쳐도_모자라면_아무것도_빼지_않는다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 3, 0), Item(FishTid, 5, 2, Wh) });

        user.TryConsumeItems(new Dictionary<int, int> { [FishTid] = 9 }, out _).ShouldBeFalse();

        user.GetItemCount(FishTid).ShouldBe(8);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 창고의_자원도_판다()
    {
        var (user, b) = NewUser(new[] { Item(FishTid, 5, 2, Wh) });

        user.TrySellItems(new Dictionary<int, int> { [FishTid] = 5 }, 50, out var changes).ShouldBeTrue();

        changes.Single().Container.ShouldBe(EContainer.Warehouse);
        user.Gold.ShouldBe(50);
    }

    // ── 로그인 ──

    [Fact]
    public void 창고에_맡긴_장착_장비는_재로그인_뒤에도_입혀져_있다()
    {
        var (user, _) = NewUser(
            characters: new[] { Character(StoredC, 0, Wh) },
            equips: new[] { SwordRow(Wh) },
            worn: Wearing(StoredC));

        user.TryGetEquip(Sword, out var sword).ShouldBeTrue();
        sword.EquippedCharacterId.ShouldBe(StoredC);
    }

    [Fact]
    public void 창고에_맡긴_배치_캐릭터는_재로그인_뒤에도_배치돼_있다()
    {
        var b    = Builder();
        var user = b.Build();

        user.OnLoginDataLoaded(new PlayerLoginData(
            new List<InventoryRow>(),
            null,
            new List<CharacterRow> { Character(CharA, 0), Character(StoredC, 0, Wh) },
            new List<WorkStationSlotRow> { new() { slot_index = 0, industry = (int)IndustryType.Excavation, industry_level = 1, character_id = StoredC } },
            new List<UserUnlockRow>(),
            new List<UserEquipRow>(),
            new List<CharacterEquipRow>()), Base);

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.CharacterId.ShouldBe(StoredC);
    }
}
