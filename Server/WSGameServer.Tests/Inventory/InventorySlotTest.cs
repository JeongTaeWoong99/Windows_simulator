namespace WSGameServer;

/// <summary>
/// 자원 칸(T-058) — 처음 얻는 자원은 첫 빈 칸, 수량만 늘면 자리 유지, 0개가 되면 칸을 비운다.
/// 모든 저장 경로가 ItemChangeInfo.Slot을 그대로 쓰므로 여기가 틀리면 DB 칸도 틀린다.
/// </summary>
public class InventorySlotTest
{
    [Fact]
    public void 처음_얻는_자원은_첫_빈_칸에_들어간다()
    {
        var inventory = new Inventory();
        inventory.Load(new[] { new Item(101, 1, 0), new Item(102, 1, 2) });

        // 0·2가 찼다 → 1
        inventory.AddItem(103, 5).Slot.ShouldBe(1);
    }

    [Fact]
    public void 수량만_늘면_칸은_그대로다()
    {
        var inventory = new Inventory();
        inventory.Load(new[] { new Item(101, 1, 7) });

        inventory.AddItem(101, 3).Slot.ShouldBe(7);
    }

    [Fact]
    public void 다_쓴_자원의_칸은_다음_새_자원이_쓴다()
    {
        var inventory = new Inventory();
        inventory.Load(new[] { new Item(101, 2, 0), new Item(102, 1, 1) });

        inventory.TryRemoveItems(new Dictionary<int, int> { [101] = 2 }, out _);

        // 101이 떠난 칸 0이 비었다 → 새 자원은 칸 2가 아니라 0
        inventory.AddItem(104, 1).Slot.ShouldBe(0);
    }

    [Fact]
    public void 로그인_때_칸이_겹치면_뒤의_것을_첫_빈_칸으로_옮긴다()
    {
        var inventory = new Inventory();
        inventory.Load(new[] { new Item(101, 1, 0), new Item(102, 1, 0), new Item(103, 1, 1) });

        // 102가 0을 101과 겹친다 → 0·1이 찼으니 2
        inventory.Snapshot().Single(i => i.ItemId == 102).Slot.ShouldBe(2);
    }

    [Fact]
    public void 차감_변경분도_칸을_싣는다()
    {
        var inventory = new Inventory();
        inventory.Load(new[] { new Item(101, 5, 4) });

        inventory.TryRemoveItems(new Dictionary<int, int> { [101] = 2 }, out var changes);

        changes.Single().Slot.ShouldBe(4);
    }
}
