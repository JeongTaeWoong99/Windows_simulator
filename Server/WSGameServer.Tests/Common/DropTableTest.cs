using GameData;
using WSGameServer;

namespace WSGameServer;

/// <summary>
/// <see cref="DropTable"/>·<see cref="DropTableCatalog"/> 검증.
/// 추첨의 정확성은 <see cref="WeightedPickerTest"/>가 담당하고, 여기서는
/// <b>Row → ItemTID 정규화</b>와 <b>여러 테이블을 다루는 부분</b>을 본다.
/// </summary>
public class DropTableTest
{
    /// <summary>실제 드롭 시트와 같은 모양의 테스트용 Row.</summary>
    private sealed record Row(int DropTID, int ItemTID, int Weight);

    private static readonly Row[] FishingRows =
    {
        new(10001, 1001, 700),
        new(10002, 1002, 300),
    };

    private static DropTable BuildTable() =>
        DropTable.From("FishingBasicTable", FishingRows, r => r.ItemTID, r => r.Weight);

    /// <summary>지정한 값만 내놓는 난수원.</summary>
    private static Random FixedRoll(int value)
    {
        var random = new Mock<Random>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(value);
        return random.Object;
    }

    // ─────────────────────────── DropTable ───────────────────────────

    [Fact]
    public void Row에서_ItemTID와_Weight만_뽑아_정규화한다()
    {
        var table = BuildTable();

        table.Count.ShouldBe(2);
        table.TotalWeight.ShouldBe(1000);

        // DropTID는 시트의 키일 뿐 드롭 결과와 무관하다 — 결과는 ItemTID다.
        table.Roll(FixedRoll(0)).ShouldBe(1001);
        table.Roll(FixedRoll(699)).ShouldBe(1001);
        table.Roll(FixedRoll(700)).ShouldBe(1002);
        table.Roll(FixedRoll(999)).ShouldBe(1002);
    }

    [Fact]
    public void RollMany는_ItemTID별_개수로_집계한다()
    {
        var gained = BuildTable().RollMany(1000, new Random(42));

        // 판정 수는 보존돼야 한다 — 정산에서 이 합이 곧 지급 개수다.
        gained.Values.Sum().ShouldBe(1000);
        gained.Keys.ShouldBeSubsetOf(new[] { 1001, 1002 });
    }

    [Fact]
    public void RollManyInto는_기존_집계에_더한다()
    {
        var table  = BuildTable();
        var gained = new Dictionary<int, int> { [9999] = 5 };   // 다른 층에서 이미 쌓인 결과

        table.RollManyInto(100, gained, new Random(42));

        gained[9999].ShouldBe(5);              // 기존 항목은 건드리지 않는다
        gained.Values.Sum().ShouldBe(105);     // 5 + 100
    }

    [Fact]
    public void 같은_아이템이_여러_행에_있어도_하나로_합산된다()
    {
        // 같은 ItemTID를 다른 DropTID로 두 번 넣는 경우(등급 없이 확률만 쪼갤 때 생긴다)
        var rows = new[] { new Row(1, 500, 50), new Row(2, 500, 50) };
        var table = DropTable.From("중복", rows, r => r.ItemTID, r => r.Weight);

        var gained = table.RollMany(10, new Random(1));

        gained.Count.ShouldBe(1);
        gained[500].ShouldBe(10);
    }

    [Fact]
    public void 이름이_비면_거부한다()
    {
        Should.Throw<ArgumentException>(() =>
            DropTable.From("", FishingRows, r => r.ItemTID, r => r.Weight));
    }

    // ────────────────────────── DropTableCatalog ──────────────────────────
    // Singleton이지만 new()가 가능하므로 테스트마다 독립 인스턴스를 쓴다.
    // Instance를 공유하면 테스트 순서에 따라 등록 상태가 새어 나간다.

    /// <summary>실제 드롭 시트와 같은 모양(레벨 컬럼 포함)의 테스트용 Row.</summary>
    private sealed record LevelRow(int DropTID, int IndustryLevel, int ItemTID, int Weight);

    [Fact]
    public void 산업과_레벨별로_테이블을_등록하고_조회한다()
    {
        var catalog = new DropTableCatalog();

        catalog.Register(IndustryType.Fishing, level: 1, BuildTable());

        catalog.Count.ShouldBe(1);
        catalog.Get(IndustryType.Fishing, 1).Name.ShouldBe("FishingBasicTable");
        catalog.Get(IndustryType.Fishing, 1).Roll(FixedRoll(0)).ShouldBe(1001);
    }

    [Fact]
    public void 시트_행을_레벨로_갈라_레벨마다_다른_테이블이_된다()
    {
        // 실제 시트 모양 — 한 테이블에 레벨별 행이 섞여 있다(병합 규약).
        var rows = new[]
        {
            new LevelRow(200101, 1, 1001, 700),
            new LevelRow(200102, 1, 1002, 300),
            new LevelRow(200201, 2, 1011, 700),
        };

        var catalog = new DropTableCatalog();
        catalog.Register(IndustryType.Fishing, "FishingBasicTable", rows,
                         r => r.IndustryLevel, r => r.ItemTID, r => r.Weight);

        catalog.Count.ShouldBe(2);

        // Lv1을 돌리면 Lv2 아이템(1011)은 절대 나오지 않는다 — 이 격리가 이 구조의 전부다.
        catalog.Get(IndustryType.Fishing, 1).TotalWeight.ShouldBe(1000);
        catalog.Get(IndustryType.Fishing, 1).Roll(FixedRoll(999)).ShouldBe(1002);
        catalog.Get(IndustryType.Fishing, 2).TotalWeight.ShouldBe(700);
        catalog.Get(IndustryType.Fishing, 2).Roll(FixedRoll(0)).ShouldBe(1011);
    }

    [Fact]
    public void 같은_산업_같은_레벨을_두_번_등록하면_막는다()
    {
        var catalog = new DropTableCatalog();
        catalog.Register(IndustryType.Fishing, level: 1, BuildTable());

        // 덮어쓰기를 허용하면 등록 순서에 따라 확률이 조용히 바뀐다.
        Should.Throw<InvalidOperationException>(() =>
            catalog.Register(IndustryType.Fishing, level: 1, BuildTable()));

        // 같은 산업이라도 레벨이 다르면 별개 테이블이다.
        catalog.Register(IndustryType.Fishing, level: 2, BuildTable());
        catalog.Count.ShouldBe(2);
    }

    [Fact]
    public void 없는_산업이나_레벨을_조회하면_예외를_던진다()
    {
        var catalog = new DropTableCatalog();
        catalog.Register(IndustryType.Fishing, level: 1, BuildTable());

        // 조용히 null을 주면 드롭이 비어도 아무도 모른다.
        Should.Throw<KeyNotFoundException>(() => catalog.Get(IndustryType.Mining, 1));
        Should.Throw<KeyNotFoundException>(() => catalog.Get(IndustryType.Fishing, 2));

        catalog.TryGet(IndustryType.Mining, 1, out _).ShouldBeFalse();
        catalog.TryGet(IndustryType.Fishing, 2, out _).ShouldBeFalse();
        catalog.TryGet(IndustryType.Fishing, 1, out _).ShouldBeTrue();
    }

    [Fact]
    public void 산업이_늘어도_서로_영향을_주지_않는다()
    {
        var catalog = new DropTableCatalog();

        catalog.Register(IndustryType.Fishing, level: 1, DropTable.From(
            "FishingBasicTable", new[] { new Row(1, 1001, 100) }, r => r.ItemTID, r => r.Weight));
        catalog.Register(IndustryType.Mining, level: 1, DropTable.From(
            "MiningBasicTable", new[] { new Row(1, 2001, 100) }, r => r.ItemTID, r => r.Weight));

        catalog.Count.ShouldBe(2);
        catalog.Get(IndustryType.Fishing, 1).Roll(FixedRoll(0)).ShouldBe(1001);
        catalog.Get(IndustryType.Mining, 1).Roll(FixedRoll(0)).ShouldBe(2001);
        catalog.All.Select(x => x.Industry).ShouldBe(new[] { IndustryType.Fishing, IndustryType.Mining }, ignoreOrder: true);
    }
}
