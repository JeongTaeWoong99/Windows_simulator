using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 자리 이동(T-058) — 빈 칸이면 옮기고 차 있으면 교환, 거절이면 아무것도 바꾸지 않는다.
/// 칸은 응답 후에 바뀌므로(클라는 낙관적 갱신을 하지 않는다) 응답에 실린 칸이 곧 화면이다.
/// </summary>
public class UserStorageSlotTest
{
    private const int CharacterTid = 1002;
    private const int FishTid      = 10001;   // MaxStack 9999

    public UserStorageSlotTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWithItems(params (int Id, int Slot)[] items)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadInventory(items.Select(i => new InventoryRow { item_id = i.Id, count = 1, slot = i.Slot }).ToList());
        return (user, b);
    }

    private static S_StorageSlotsResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_StorageSlotsResponse>().Last();

    [Fact]
    public void 찬_칸으로_옮기면_교환된_두_칸을_돌려준다()
    {
        var (user, b) = UserWithItems((101, 0), (102, 1));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 1);

        Last(b).Slots.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { (101L, 1), (102L, 0) });
    }

    [Fact]
    public void 옮긴_칸은_다음_스냅샷에도_남는다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 7);
        user.SendInventory();

        b.Channel.SentOf<S_InventoryResponse>().Last().Items!.Single().Slot.ShouldBe(7);
    }

    [Fact]
    public void 옮기면_바뀐_칸을_저장한다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 3);

        // 자원은 칸이 키다(T-160) — 새 칸에 묶음을 쓰고 떠난 칸은 비운다
        b.DB.PostedOf<SaveItemChangesRepository>().Single().Changes
            .Select(c => (c.Slot, c.ItemId, c.Count)).ShouldBe(new[] { (3, 101, 1), (0, 101, 0) }, ignoreOrder: true);
    }

    [Fact]
    public void 같은_자원_칸에_놓으면_한_칸으로_합친다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadInventory(new[]
        {
            new InventoryRow { item_id = FishTid, count = 30, slot = 0 },
            new InventoryRow { item_id = FishTid, count = 40, slot = 4 },
        });

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 4);

        // 30 + 40 = 70이 칸 4에, 칸 0은 빈다
        Last(b).Items.Select(c => (c.Slot, c.Count)).ShouldBe(new[] { (4, 70), (0, 0) }, ignoreOrder: true);
    }

    [Fact]
    public void 합쳐서_넘치는_몫은_원래_칸에_남는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadInventory(new[]
        {
            new InventoryRow { item_id = FishTid, count = 9000, slot = 0 },
            new InventoryRow { item_id = FishTid, count = 1500, slot = 4 },
        });

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 4);

        // 칸 4는 9999까지 8499만 받는다 → 칸 0에 501이 남는다
        Last(b).Items.Select(c => (c.Slot, c.Count)).ShouldBe(new[] { (4, 9999), (0, 501) }, ignoreOrder: true);
    }

    [Fact]
    public void 칸_수_밖으로는_옮길_수_없다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, User.StorageCapacity);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotOutOfRange);
    }

    [Fact]
    public void 없는_보관함은_거절한다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot((EContainer)9, EStorageTab.Resource, 0, 1);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotOutOfRange);
    }

    [Fact]
    public void 빈_칸에서는_옮길_수_없다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 5, 1);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotEmpty);
    }

    [Fact]
    public void 같은_칸으로_옮기면_아무것도_저장하지_않는다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 0);

        Last(b).Result.ShouldBe(EResultCode.Ok);
        b.DB.PostedOf<SaveStorageSlotsRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 캐릭터_탭도_교환된다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = CharacterTid, level = 1, slot = 0 },
            new CharacterRow { character_id = 2, character_tid = CharacterTid, level = 1, slot = 4 },
        });

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Character, 4, 0);

        Last(b).Slots.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { (2L, 0), (1L, 4) });
    }

    [Fact]
    public void 지급_대기_중인_칸으로는_옮길_수_없다()
    {
        // 칸 1이 비어 있지만 뽑기 응답을 기다리며 예약돼 있다 — 옮기면 지급된 캐릭터와 겹친다
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = CharacterTid, level = 1, slot = 0 },
            new CharacterRow { character_id = 2, character_tid = CharacterTid, level = 1, slot = 2 },
        });
        user.GrantGachaCharacters(new[] { CharacterTid });

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Character, 0, 1);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotPending);
    }
}
