using MikaProtocol;
using WSGameServer;

namespace WSGameServer.Tests.InventoryTests;

/// <summary>
/// 차감이 지키는 것: <b>전부 되거나 전혀 안 된다.</b>
/// 판매는 골드 지급과 짝이라, 부분 차감이 남으면 지급이 실패했을 때 아이템만 사라진다.
/// </summary>
public class InventoryRemoveTest
{
    [Fact]
    public void 보유량보다_많이_빼려_하면_아무것도_바뀌지_않는다()
    {
        var inventory = new Inventory();
        inventory.AddItem(101, 3);
        inventory.AddItem(202, 5);

        // 101은 3개뿐인데 4개를 요구한다 — 뺄 수 있는 202까지 통째로 거절돼야 한다.
        var ok = inventory.TryRemoveItems(new Dictionary<int, int> { [101] = 4, [202] = 1 }, out _);

        ok.ShouldBeFalse();
        var snapshot = inventory.Snapshot();
        snapshot.Single(i => i.ItemId == 101).Count.ShouldBe(3);
        snapshot.Single(i => i.ItemId == 202).Count.ShouldBe(5);
    }

    [Fact]
    public void 전부_빼면_종류는_Remove이고_목록에서_사라진다()
    {
        var inventory = new Inventory();
        inventory.AddItem(101, 3);

        inventory.TryRemoveItems(new Dictionary<int, int> { [101] = 3 }, out var changes).ShouldBeTrue();

        changes.Single().Kind.ShouldBe(EItemChangeKind.Remove);
        changes.Single().Count.ShouldBe(0);

        // 0개짜리로 남으면 다음 로그인·스냅샷에 빈 칸으로 실려 나간다.
        inventory.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void 일부만_빼면_종류는_Update이고_남은_총량이_온다()
    {
        var inventory = new Inventory();
        inventory.AddItem(101, 7);

        inventory.TryRemoveItems(new Dictionary<int, int> { [101] = 2 }, out var changes).ShouldBeTrue();

        // 7 - 2 = 5. 델타(2)를 실어 보내면 클라 덮어쓰기에서 보유량이 2로 깎인다(이슈 #8과 같은 함정).
        changes.Single().Count.ShouldBe(5);
        changes.Single().Kind.ShouldBe(EItemChangeKind.Update);
    }

    [Fact]
    public void 수량이_0_이하인_요청은_거절된다()
    {
        var inventory = new Inventory();
        inventory.AddItem(101, 3);

        inventory.TryRemoveItems(new Dictionary<int, int> { [101] = 0 }, out _).ShouldBeFalse();
        inventory.Snapshot().Single().Count.ShouldBe(3);
    }

    [Fact]
    public void 가지고_있지_않은_아이템은_거절된다()
    {
        var inventory = new Inventory();
        inventory.AddItem(101, 3);

        inventory.TryRemoveItems(new Dictionary<int, int> { [999] = 1 }, out _).ShouldBeFalse();
        inventory.Snapshot().Single().Count.ShouldBe(3);
    }
}
