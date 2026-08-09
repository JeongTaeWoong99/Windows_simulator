using GameData;

namespace WSGameServer;

/// <summary>
/// <see cref="IndustryLevelCatalog"/> 검증 — (산업, 레벨) → <c>IndustryLevelTable</c> 행 조회와
/// <b>판정 비용의 단위 환산</b>(엑셀 초×천분율 → 서버 밀리초×천분율)을 본다.
/// 깨지면: 판정 주기가 레벨과 무관해지거나, 단위가 어긋나 1000배 빠르게/느리게 돈다.
/// </summary>
public class IndustryLevelCatalogTest
{
    private static IndustryLevelTableRow Row(IndustryType industry, int level, int requiredScore) => new()
    {
        IndustryLevelTID = (int)industry * 100 + level,
        IndustryType     = industry,
        Level            = level,
        Name             = "테스트",
        RequiredScore    = requiredScore,
    };

    [Fact]
    public void 산업과_레벨로_행을_조회한다()
    {
        var catalog = new IndustryLevelCatalog();
        catalog.Load(new[] { Row(IndustryType.Fishing, 1, 30_000), Row(IndustryType.Fishing, 2, 90_000) });

        catalog.Count.ShouldBe(2);
        catalog.Get(IndustryType.Fishing, 1).RequiredScore.ShouldBe(30_000);
        catalog.Get(IndustryType.Fishing, 2).RequiredScore.ShouldBe(90_000);
    }

    [Fact]
    public void 판정_비용은_필요_점수의_밀리초_환산이다()
    {
        // 엑셀 RequiredScore는 초×천분율, 서버 누적은 밀리초×천분율 — ×1000이 단위 환산의 전부다.
        // Lv1 30,000점 = 기존 상수 30초(30,000,000)와 정확히 같아야 한다 (산업레벨.md 2.4).
        var catalog = new IndustryLevelCatalog();
        catalog.Load(new[] { Row(IndustryType.Fishing, 1, 30_000), Row(IndustryType.Fishing, 5, 2_430_000) });

        catalog.GetJudgeCostUnits(IndustryType.Fishing, 1).ShouldBe(30_000_000L);
        catalog.GetJudgeCostUnits(IndustryType.Fishing, 1).ShouldBe(WorkStationSlot.JudgeCost);

        // Lv5는 24억 3천만 — int(21억)를 넘는 값이라 long 환산이 여기서 검증된다.
        catalog.GetJudgeCostUnits(IndustryType.Fishing, 5).ShouldBe(2_430_000_000L);
    }

    [Fact]
    public void 같은_산업_같은_레벨을_두_번_넣으면_막는다()
    {
        var catalog = new IndustryLevelCatalog();

        // 중복을 허용하면 어느 행의 필요 점수가 이기는지가 로드 순서에 달린다.
        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Row(IndustryType.Fishing, 1, 30_000), Row(IndustryType.Fishing, 1, 90_000) }));
    }

    [Fact]
    public void 없는_조합을_조회하면_예외를_던진다()
    {
        var catalog = new IndustryLevelCatalog();
        catalog.Load(new[] { Row(IndustryType.Fishing, 1, 30_000) });

        // 조용히 기본값을 주면 레벨을 잘못 들고 와도 30초로 돌아 아무도 모른다.
        Should.Throw<KeyNotFoundException>(() => catalog.Get(IndustryType.Fishing, 2));
        catalog.TryGet(IndustryType.Fishing, 2, out _).ShouldBeFalse();
    }

    [Fact]
    public void 실데이터는_산업_5종_레벨_5개_행_25개가_등록된다()
    {
        GameTableFixture.EnsureLoaded();
        var catalog = new IndustryLevelCatalog();
        catalog.LoadAll();

        catalog.Count.ShouldBe(25);

        // Lv1의 판정 비용은 기존 전역 상수(30초)와 같다 — "30초 확정은 Lv1의 값으로 살아남았다".
        catalog.GetJudgeCostUnits(IndustryType.Fishing, 1).ShouldBe(WorkStationSlot.JudgeCost);
    }
}
