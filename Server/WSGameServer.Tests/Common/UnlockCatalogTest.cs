using GameData;

namespace WSGameServer;

/// <summary>
/// <see cref="UnlockCatalog"/> 검증 — <c>UnlockTable</c>·<c>WorkSlotTable</c> 인덱스와 <b>로드 검증 3종</b>(해금 2.5).
/// 깨지면: 영영 못 여는 해금이 조용히 서버에 실리거나, 슬롯 하나를 열었는데 다른 콘텐츠까지 열린다.
/// </summary>
public class UnlockCatalogTest
{
    private static UnlockTableRow Unlock(int tid, int gold = 0, params int[] required) => new()
    {
        UnlockTID = tid, Name = $"해금 {tid}", Gold = gold, RequiredUnlockTIDs = required,
    };

    private static WorkSlotTableRow Slot(int index, int unlockTid) => new()
    {
        WorkSlotTID = index, UnlockTID = unlockTid,
    };

    [Fact]
    public void 해금_TID로_행을_조회한다()
    {
        var catalog = new UnlockCatalog();
        catalog.Load(new[] { Unlock(1002, 500), Unlock(1003, 1500, 1002) },
                     new[] { Slot(0, 0), Slot(1, 0), Slot(2, 1002), Slot(3, 1003) });

        catalog.Count.ShouldBe(2);
        catalog.TryGetUnlock(1003, out var row).ShouldBeTrue();
        row.Gold.ShouldBe(1500);
        catalog.TryGetUnlock(9999, out _).ShouldBeFalse();
    }

    [Fact]
    public void 해금이_어느_작업슬롯_칸인지_찾는다()
    {
        var catalog = new UnlockCatalog();
        catalog.Load(new[] { Unlock(1002) }, new[] { Slot(0, 0), Slot(2, 1002) });

        catalog.TryGetWorkSlotOf(1002, out var slotIndex).ShouldBeTrue();
        slotIndex.ShouldBe(2);

        // 0은 "항상 열림"이지 해금이 아니다 — 어느 칸도 가리키지 않는다.
        catalog.TryGetWorkSlotOf(0, out _).ShouldBeFalse();
    }

    [Fact]
    public void 작업슬롯_칸은_번호_순으로_나온다()
    {
        var catalog = new UnlockCatalog();
        catalog.Load(new[] { Unlock(1002), Unlock(1003) }, new[] { Slot(3, 1003), Slot(0, 0), Slot(2, 1002) });

        catalog.WorkSlots.Select(s => s.WorkSlotTID).ShouldBe(new[] { 0, 2, 3 });
    }

    [Fact]
    public void 선행이_순환하면_로드를_막는다()
    {
        // A→B→A는 둘 다 영영 못 연다. 조용히 돌면 유저가 눌러 봐야 UnlockLocked뿐이다.
        var catalog = new UnlockCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Unlock(1002, 0, 1003), Unlock(1003, 0, 1002) },
                         new[] { Slot(2, 1002), Slot(3, 1003) }));
    }

    [Fact]
    public void 자기_자신을_선행으로_두면_로드를_막는다()
    {
        var catalog = new UnlockCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Unlock(1002, 0, 1002) }, new[] { Slot(2, 1002) }));
    }

    [Fact]
    public void 없는_해금을_선행으로_두면_로드를_막는다()
    {
        var catalog = new UnlockCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Unlock(1002, 0, 9999) }, new[] { Slot(2, 1002) }));
    }

    [Fact]
    public void 한_해금을_두_칸이_참조하면_로드를_막는다()
    {
        // 1:1 위반 — 한 번 사서 두 칸이 열리면 골드 sink가 반값이 된다.
        var catalog = new UnlockCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Unlock(1002) }, new[] { Slot(2, 1002), Slot(3, 1002) }));
    }

    [Fact]
    public void 칸이_없는_해금을_참조하면_로드를_막는다()
    {
        var catalog = new UnlockCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Unlock(1002) }, new[] { Slot(2, 9999) }));
    }

    [Fact]
    public void 아무_콘텐츠도_참조하지_않는_해금은_경고만_하고_통과한다()
    {
        // 오타로 죽은 행일 수 있지만 기동을 막을 일은 아니다 — 다른 콘텐츠가 나중에 참조할 수도 있다.
        var catalog = new UnlockCatalog();

        Should.NotThrow(() => catalog.Load(new[] { Unlock(1002), Unlock(2001) }, new[] { Slot(2, 1002) }));
        catalog.Count.ShouldBe(2);
    }

    [Fact]
    public void 실데이터는_해금_51개_작업슬롯_8칸이_등록된다()
    {
        GameTableFixture.EnsureLoaded();
        var catalog = new UnlockCatalog();
        catalog.LoadAll();

        // 작업슬롯 6 + 특성 노드 45(산업 레벨 5×4 · 속도 5×5)
        catalog.Count.ShouldBe(51);
        catalog.WorkSlots.Count.ShouldBe(8);

        // 상한 칸(7번)은 1007이 연다. 시작 2칸(0·1번)은 해금 0이다.
        catalog.TryGetWorkSlotOf(1007, out var last).ShouldBeTrue();
        last.ShouldBe(7);
        catalog.WorkSlots.Count(s => s.UnlockTID == 0).ShouldBe(2);
    }
}
