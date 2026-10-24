using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 이미 나뉜 자원 칸 정리(T-175 · 이슈 #82 보강). [합치기]는 칸 순서를 지킨 채 앞 칸부터 채우고,
/// [창고에 쌓기]는 창고에 이미 있는 자원만 옮긴다. 틀리면 칸이 뒤섞이거나 처음 보는 자원이 창고에 생긴다.
/// </summary>
public class UserStorageMergeTest
{
    public UserStorageMergeTest() => GameTableFixture.EnsureLoaded();

    private const int FishTid = 10001;   // MaxStack 9999
    private const int CarpTid = 10002;

    private static (User User, TestUserBuilder B) NewUser(params InventoryRow[] items)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadInventory(items);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static InventoryRow Item(int tid, int count, int slot, EContainer container = EContainer.Inventory)
        => new() { item_id = tid, count = count, slot = slot, container = (int)container };

    private static (int, int, int)[] Slots(S_StorageSlotsResponse res)
        => res.Items.Select(c => (c.ItemId, c.Slot, c.Count)).OrderBy(c => c.Slot).ToArray();

    private static (EContainer, long, int, int)[] Changes(S_WarehouseTransferResponse res)
        => res.Changes.Select(c => (c.Container, c.Key, c.Slot, c.Count)).OrderBy(c => c.Container).ThenBy(c => c.Slot).ToArray();

    // ── 합치기 ──

    [Fact]
    public void 합치기는_같은_자원을_앞_칸부터_채우고_뒤_칸을_비운다()
    {
        // 물고기 5 + 9998 + 7 = 10010 → 0번 9999 · 2번 11 · 4번 비움. 붕어(1번)는 그대로
        var (user, b) = NewUser(Item(FishTid, 5, 0), Item(CarpTid, 3, 1), Item(FishTid, 9998, 2), Item(FishTid, 7, 4));

        user.MergeStorage(EContainer.Inventory);

        Slots(b.Channel.SentOf<S_StorageSlotsResponse>().Single()).ShouldBe(new[]
        {
            (FishTid, 0, 9999),
            (FishTid, 2, 11),
            (FishTid, 4, 0),
        });
    }

    [Fact]
    public void 합칠_것이_없으면_아무_칸도_바꾸지_않는다()
    {
        var (user, b) = NewUser(Item(FishTid, 5, 0), Item(CarpTid, 3, 1));

        user.MergeStorage(EContainer.Inventory);

        var res = b.Channel.SentOf<S_StorageSlotsResponse>().Single();
        res.Result.ShouldBe(EResultCode.Ok);
        res.Items.ShouldBeEmpty();
    }

    [Fact]
    public void 창고_안에서도_합친다()
    {
        var (user, b) = NewUser(Item(FishTid, 5, 1, EContainer.Warehouse), Item(FishTid, 7, 3, EContainer.Warehouse));

        user.MergeStorage(EContainer.Warehouse);

        var res = b.Channel.SentOf<S_StorageSlotsResponse>().Single();
        res.Container.ShouldBe(EContainer.Warehouse);
        Slots(res).ShouldBe(new[] { (FishTid, 1, 12), (FishTid, 3, 0) });
    }

    [Fact]
    public void 합친_칸을_저장한다()
    {
        var (user, b) = NewUser(Item(FishTid, 5, 0), Item(FishTid, 7, 4));

        user.MergeStorage(EContainer.Inventory);

        b.DB.Posted.OfType<SaveItemChangesRepository>().Single().Changes
            .Select(c => (c.Slot, c.Count)).OrderBy(c => c.Slot).ShouldBe(new[] { (0, 12), (4, 0) });
    }

    // ── 창고에 쌓기 ──

    [Fact]
    public void 창고에_쌓기는_창고에_있는_자원만_창고_칸으로_옮긴다()
    {
        // 물고기 인벤토리 50 → 창고 100 + 50 = 150 · 붕어는 창고에 없어 그대로
        var (user, b) = NewUser(Item(FishTid, 50, 0), Item(CarpTid, 4, 1), Item(FishTid, 100, 3, EContainer.Warehouse));

        user.StackToWarehouse();

        Changes(b.Channel.SentOf<S_WarehouseTransferResponse>().Single()).ShouldBe(new[]
        {
            (EContainer.Inventory, (long)FishTid, 0, 0),
            (EContainer.Warehouse, (long)FishTid, 3, 150),
        });
    }

    [Fact]
    public void 창고_칸이_차면_남는_몫은_인벤토리에_그대로_둔다()
    {
        // 창고 9990 → 9999(+9) · 인벤토리 50 → 41
        var (user, b) = NewUser(Item(FishTid, 50, 0), Item(FishTid, 9990, 3, EContainer.Warehouse));

        user.StackToWarehouse();

        Changes(b.Channel.SentOf<S_WarehouseTransferResponse>().Single()).ShouldBe(new[]
        {
            (EContainer.Inventory, (long)FishTid, 0, 41),
            (EContainer.Warehouse, (long)FishTid, 3, 9999),
        });
    }

    [Fact]
    public void 창고에_쌓은_칸을_한_번에_저장한다()
    {
        var (user, b) = NewUser(Item(FishTid, 50, 0), Item(FishTid, 100, 3, EContainer.Warehouse));

        user.StackToWarehouse();

        b.DB.Posted.OfType<WarehouseTransferRepository>().Single().Items.Count.ShouldBe(2);
    }

    [Fact]
    public void 창고에_쌓은_뒤_수량_합계는_그대로다()
    {
        var (user, _) = NewUser(Item(FishTid, 50, 0), Item(FishTid, 30, 5), Item(FishTid, 9990, 3, EContainer.Warehouse));

        user.StackToWarehouse();

        user.GetItemCount(FishTid).ShouldBe(10070);
    }
}
