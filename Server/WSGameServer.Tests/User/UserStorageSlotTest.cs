using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 자리 이동(T-058) — 빈 칸이면 옮기고 차 있으면 교환, 거절이면 아무것도 바꾸지 않는다.
/// 칸은 응답 후에 바뀌므로(클라는 낙관적 갱신을 하지 않는다) 응답에 실린 칸이 곧 화면이다.
/// </summary>
public class UserStorageSlotTest
{
    private const int CharacterTid = 1002;

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

        b.Channel.SentOf<S_InventoryResponse>().Last().Items.Single().Slot.ShouldBe(7);
    }

    [Fact]
    public void 옮기면_바뀐_칸을_저장한다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, 3);

        b.DB.PostedOf<SaveStorageSlotsRepository>().Single().Changes.ShouldBe(new[] { new SlotChange(101, 3) });
    }

    [Fact]
    public void 칸_수_밖으로는_옮길_수_없다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Resource, 0, User.StorageCapacity);

        Last(b).Result.ShouldBe(EResultCode.StorageSlotOutOfRange);
    }

    [Fact]
    public void 창고는_아직_없는_보관함으로_거절한다()
    {
        var (user, b) = UserWithItems((101, 0));

        user.MoveStorageSlot(EContainer.Warehouse, EStorageTab.Resource, 0, 1);

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
}
