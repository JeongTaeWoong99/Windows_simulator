using MikaProtocol;

namespace WSGameServer.Tests.InventoryTests;

/// <summary>
/// 칸별 묶음(T-160) — MaxStack은 칸 하나의 상한이다. 한 칸이 차면 빈 칸에 이어 쌓고,
/// 칸이 전부 찼을 때만 버린다. 차감은 덜 찬 칸부터 빼서 칸 수가 빨리 준다.
/// 틀리면 수확분이 칸이 남았는데 버려지거나, 칸 수 검사가 실제 지급과 어긋난다.
/// </summary>
public class InventoryStackTest
{
    private const int Fish = 101;
    private const int Carp = 102;

    [Fact]
    public void 칸이_차면_남는_몫은_빈_칸에_새_묶음으로_들어간다()
    {
        var inventory = new Inventory(capacity: 5);
        inventory.Load(new[] { new Item(Fish, 95, 0) });

        // 칸 0은 100까지 5개만 더 받는다 → 나머지 7개는 첫 빈 칸 1에
        var changes = inventory.AddItem(Fish, 12, maxStack: 100, out var added);

        added.ShouldBe(12);
        changes.Select(c => (c.Slot, c.Count)).ShouldBe(new[] { (0, 100), (1, 7) });
    }

    [Fact]
    public void 한_번에_여러_칸을_채운다()
    {
        var inventory = new Inventory(capacity: 5);

        // 250 = 100 + 100 + 50
        inventory.AddItem(Fish, 250, maxStack: 100, out _)
            .Select(c => (c.Slot, c.Count)).ShouldBe(new[] { (0, 100), (1, 100), (2, 50) });
    }

    [Fact]
    public void 칸이_전부_차면_못_넣은_몫은_버린다()
    {
        var inventory = new Inventory(capacity: 2);
        inventory.Load(new[] { new Item(Carp, 1, 0) });

        // 빈 칸은 1 하나 → 100만 들어가고 150은 버린다
        inventory.AddItem(Fish, 250, maxStack: 100, out var added);

        added.ShouldBe(100);
        inventory.GetCount(Fish).ShouldBe(100);
    }

    [Fact]
    public void 보유량은_칸들의_합이다()
    {
        var inventory = new Inventory(capacity: 5);
        inventory.Load(new[] { new Item(Fish, 100, 0), new Item(Carp, 3, 1), new Item(Fish, 40, 4) });

        inventory.GetCount(Fish).ShouldBe(140);
    }

    [Fact]
    public void 덜_찬_칸이_여럿이면_칸_번호가_앞선_것부터_채운다()
    {
        var inventory = new Inventory(capacity: 5);
        inventory.Load(new[] { new Item(Fish, 90, 3), new Item(Fish, 80, 1) });

        // 칸 1(80)에 20 → 남은 5는 칸 3(90)에
        inventory.AddItem(Fish, 25, maxStack: 100, out _)
            .Select(c => (c.Slot, c.Count)).ShouldBe(new[] { (1, 100), (3, 95) });
    }

    [Fact]
    public void 차감은_덜_찬_칸부터_뺀다()
    {
        var inventory = new Inventory(capacity: 5);
        inventory.Load(new[] { new Item(Fish, 100, 0), new Item(Fish, 30, 1) });

        // 덜 찬 칸 1(30)을 비우고 나머지 10은 칸 0에서 — 칸이 하나 빈다
        inventory.TryRemoveItems(new Dictionary<int, int> { [Fish] = 40 }, out var changes).ShouldBeTrue();

        changes.Select(c => (c.Slot, c.Count, c.Kind))
            .ShouldBe(new[] { (1, 0, EItemChangeKind.Remove), (0, 90, EItemChangeKind.Update) });
    }

    [Fact]
    public void 칸들을_합쳐도_모자라면_아무것도_빼지_않는다()
    {
        var inventory = new Inventory(capacity: 5);
        inventory.Load(new[] { new Item(Fish, 100, 0), new Item(Fish, 30, 1) });

        inventory.TryRemoveItems(new Dictionary<int, int> { [Fish] = 131 }, out _).ShouldBeFalse();
        inventory.GetCount(Fish).ShouldBe(130);
    }

    [Fact]
    public void 칸_수_밖의_칸은_로그인_때_첫_빈_칸으로_옮긴다()
    {
        var inventory = new Inventory(capacity: 3);
        inventory.Load(new[] { new Item(Fish, 1, 0), new Item(Carp, 1, -7) });

        // 마이그레이션이 겹친 행을 음수 칸으로 비켜 둔다 → 칸 1로
        inventory.Snapshot().Single(i => i.ItemId == Carp).Slot.ShouldBe(1);
    }

    [Fact]
    public void 받을_수_있는_수량은_덜_찬_칸의_자리와_빈_칸을_더한_것이다()
    {
        var inventory = new Inventory(capacity: 4);
        inventory.Load(new[] { new Item(Fish, 70, 0), new Item(Carp, 1, 1) });

        // 칸 0의 30 + 빈 칸 2개 × 100 = 230
        inventory.RoomFor(Fish, maxStack: 100).ShouldBe(230);
    }

    [Fact]
    public void 다시_쓰면_바뀐_칸만_변경분으로_나온다()
    {
        var inventory = new Inventory(capacity: 5);
        inventory.Load(new[] { new Item(Fish, 10, 0), new Item(Carp, 3, 1), new Item(Fish, 5, 2) });

        // 칸 2를 칸 0에 합쳤다 — 칸 1은 그대로라 변경분에 없다
        var changes = inventory.Rewrite(new[] { new Item(Fish, 15, 0), new Item(Carp, 3, 1) });

        changes.Select(c => (c.Slot, c.ItemId, c.Count))
            .ShouldBe(new[] { (0, Fish, 15), (2, Fish, 0) }, ignoreOrder: true);
    }
}
