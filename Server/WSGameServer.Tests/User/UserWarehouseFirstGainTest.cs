using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 자원 지급은 창고의 덜 찬 같은 자원 칸부터 채우고, 남는 몫만 인벤토리로 간다(T-175 · 이슈 #82).
/// 틀리면 창고에 맡겨 둔 자원이 들어올 때마다 인벤토리에 새 칸이 생기거나, 처음 보는 자원이 창고에 생긴다.
/// </summary>
public class UserWarehouseFirstGainTest
{
    public UserWarehouseFirstGainTest() => GameTableFixture.EnsureLoaded();

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

    [Fact]
    public void 창고에_덜_찬_같은_자원_칸이_있으면_거기부터_채운다()
    {
        var (user, _) = NewUser(Item(FishTid, 5, 0), Item(FishTid, 100, 3, EContainer.Warehouse));

        var changes = user.GainItem(FishTid, 7);

        changes.Select(c => (c.Container, c.Slot, c.Count)).ShouldBe(new[] { (EContainer.Warehouse, 3, 107) });
    }

    [Fact]
    public void 창고_칸이_차면_남는_몫은_인벤토리의_같은_자원_칸으로_간다()
    {
        // 창고 9990 → 9999(+9) · 남은 11은 인벤토리 5 → 16
        var (user, _) = NewUser(Item(FishTid, 5, 0), Item(FishTid, 9990, 3, EContainer.Warehouse));

        var changes = user.GainItem(FishTid, 20);

        changes.Select(c => (c.Container, c.Slot, c.Count)).ShouldBe(new[]
        {
            (EContainer.Warehouse, 3, 9999),
            (EContainer.Inventory, 0, 16),
        });
    }

    [Fact]
    public void 창고에_없는_자원은_창고_빈_칸에_새로_놓지_않는다()
    {
        var (user, _) = NewUser(Item(FishTid, 100, 3, EContainer.Warehouse));

        var changes = user.GainItem(CarpTid, 4);

        changes.Select(c => (c.Container, c.Slot, c.Count)).ShouldBe(new[] { (EContainer.Inventory, 0, 4) });
    }

    [Fact]
    public void 창고의_가득_찬_같은_자원_칸은_건너뛴다()
    {
        var (user, _) = NewUser(Item(FishTid, 9999, 0, EContainer.Warehouse), Item(FishTid, 10, 1, EContainer.Warehouse));

        var changes = user.GainItem(FishTid, 5);

        changes.Select(c => (c.Container, c.Slot, c.Count)).ShouldBe(new[] { (EContainer.Warehouse, 1, 15) });
    }

    [Fact]
    public void 인벤토리가_가득_차도_창고에_들어갈_자리가_있으면_받는다()
    {
        var items = Enumerable.Range(0, User.StorageCapacity).Select(i => Item(900_000 + i, 1, i))
            .Append(Item(FishTid, 100, 0, EContainer.Warehouse))
            .ToArray();
        var (user, _) = NewUser(items);

        user.HasStorageFor(new[] { (FishTid, 50) }, 0, 0).ShouldBeTrue();
    }

    [Fact]
    public void 창고의_남은_자리를_넘는_몫은_인벤토리가_가득_차면_거절한다()
    {
        // 창고 9990 — 남은 자리 9. 10개는 1개가 인벤토리로 가야 하는데 칸이 없다.
        var items = Enumerable.Range(0, User.StorageCapacity).Select(i => Item(900_000 + i, 1, i))
            .Append(Item(FishTid, 9990, 0, EContainer.Warehouse))
            .ToArray();
        var (user, _) = NewUser(items);

        user.HasStorageFor(new[] { (FishTid, 10) }, 0, 0).ShouldBeFalse();
    }

    [Fact]
    public void 지급한_창고_칸을_저장한다()
    {
        var (user, b) = NewUser(Item(FishTid, 100, 3, EContainer.Warehouse));

        user.GainItem(FishTid, 7);

        b.DB.Posted.OfType<AddItemRepository>().Single().Changes
            .Select(c => (c.Container, c.Slot, c.Count)).ShouldBe(new[] { (EContainer.Warehouse, 3, 107) });
    }
}
