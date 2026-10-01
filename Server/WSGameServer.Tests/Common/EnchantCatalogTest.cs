using GameData;

namespace WSGameServer;

/// <summary>
/// <see cref="EnchantCatalog"/> — 등급별 옵션 풀 · 장비 등급별 칸 수 · 큐브별 상승 확률.
/// 확률이 코드가 아니라 데이터에 있으므로, 여기서 보는 것은 "표를 그대로 읽었는가"와 기동 검증이다.
/// </summary>
public class EnchantCatalogTest
{
    private static readonly GlobalRarity[] AllGrades =
    {
        GlobalRarity.Common, GlobalRarity.Uncommon, GlobalRarity.Rare,
        GlobalRarity.Epic, GlobalRarity.Legendary, GlobalRarity.Mythic,
    };

    // 등급마다 낚시 속도 한 줄. TID = 1000 + 등급×100 + 3 (실데이터 규칙과 같다).
    private static readonly EnchantOptionTableRow[] Options = AllGrades
        .Select(g => new EnchantOptionTableRow
        {
            EnchantOptionTID = 1000 + (int)g * 100 + 3, Grade = g, OptionType = EnchantOptionType.Speed,
            Industry = IndustryType.Fishing, Value = 10 * (int)g, Weight = 120,
        })
        .ToArray();

    private static readonly EnchantGradeTableRow[] Grades =
    {
        new() { Grade = GlobalRarity.Common,    SlotCount = 1, UpPermyriad = 1600 },
        new() { Grade = GlobalRarity.Uncommon,  SlotCount = 1, UpPermyriad = 800 },
        new() { Grade = GlobalRarity.Rare,      SlotCount = 2, UpPermyriad = 400 },
        new() { Grade = GlobalRarity.Epic,      SlotCount = 2, UpPermyriad = 40 },
        new() { Grade = GlobalRarity.Legendary, SlotCount = 3, UpPermyriad = 4 },
        new() { Grade = GlobalRarity.Mythic,    SlotCount = 3, UpPermyriad = 0 },
    };

    private const int CubeA = 100015;
    private const int CubeB = 100018;

    private static readonly EnchantItemTableRow[] Items =
    {
        new() { ItemTID = CubeA, UpRatePermille = 1000 },
        new() { ItemTID = CubeB, UpRatePermille = 1250 },
    };

    private static EnchantCatalog Loaded()
    {
        var catalog = new EnchantCatalog();
        catalog.Load(Options, Grades, Items);
        return catalog;
    }

    private static EnchantItemTableRow Cube(EnchantCatalog catalog, int tid)
    {
        catalog.TryGetItem(tid, out var row).ShouldBeTrue();
        return row;
    }

    [Theory]
    [InlineData(GlobalRarity.Common, 1)]
    [InlineData(GlobalRarity.Uncommon, 1)]
    [InlineData(GlobalRarity.Rare, 2)]
    [InlineData(GlobalRarity.Epic, 2)]
    [InlineData(GlobalRarity.Legendary, 3)]
    [InlineData(GlobalRarity.Mythic, 3)]
    public void 장비_등급이_칸_수를_정한다(GlobalRarity grade, int expected)
    {
        Loaded().SlotCountOf(grade).ShouldBe(expected);
    }

    [Fact]
    public void 표에_없는_등급은_칸이_없다()
    {
        Loaded().SlotCountOf(GlobalRarity.None).ShouldBe(0);
    }

    [Fact]
    public void 큐브_A는_기본_상승_확률을_그대로_쓴다()
    {
        var catalog = Loaded();

        catalog.RankUpPermyriadOf(GlobalRarity.Common, Cube(catalog, CubeA)).ShouldBe(1600);
        catalog.RankUpPermyriadOf(GlobalRarity.Legendary, Cube(catalog, CubeA)).ShouldBe(4);
    }

    [Fact]
    public void 큐브_B는_상승_확률이_1_25배다()
    {
        var catalog = Loaded();

        // 1600 × 1.25 = 2000 · 40 × 1.25 = 50 · 4 × 1.25 = 5
        catalog.RankUpPermyriadOf(GlobalRarity.Common, Cube(catalog, CubeB)).ShouldBe(2000);
        catalog.RankUpPermyriadOf(GlobalRarity.Epic, Cube(catalog, CubeB)).ShouldBe(50);
        catalog.RankUpPermyriadOf(GlobalRarity.Legendary, Cube(catalog, CubeB)).ShouldBe(5);
    }

    [Fact]
    public void 최고_등급은_어떤_큐브로도_오르지_않는다()
    {
        // 표 값이 0이 아니어도 신화 위에는 갈 곳이 없다.
        var catalog = new EnchantCatalog();
        var grades = Grades.Select(g => g.Grade == GlobalRarity.Mythic
            ? new EnchantGradeTableRow { Grade = g.Grade, SlotCount = g.SlotCount, UpPermyriad = 10000 }
            : g).ToArray();
        catalog.Load(Options, grades, Items);

        catalog.RankUpPermyriadOf(GlobalRarity.Mythic, Cube(catalog, CubeB)).ShouldBe(0);
    }

    [Fact]
    public void 다음_등급은_한_단계씩_오르고_신화에서_멈춘다()
    {
        EnchantCatalog.NextGrade(GlobalRarity.Common).ShouldBe(GlobalRarity.Uncommon);
        EnchantCatalog.NextGrade(GlobalRarity.Legendary).ShouldBe(GlobalRarity.Mythic);
        EnchantCatalog.NextGrade(GlobalRarity.Mythic).ShouldBe(GlobalRarity.Mythic);
    }

    [Fact]
    public void 그_등급의_풀에서_칸_수만큼_뽑고_겹쳐도_된다()
    {
        // 영웅 풀은 한 줄뿐이라 3칸 모두 그 줄이어야 한다 — 중복 허용이 설계다.
        Loaded().RollOptions(GlobalRarity.Epic, 3, new Random(1))
            .Select(o => o.EnchantOptionTID).ShouldBe(new[] { 1403, 1403, 1403 });
    }

    [Fact]
    public void 칸이_있는데_풀이_빠진_등급이_있으면_예외다()
    {
        // 신화 옵션이 없으면 전설 칸이 오르는 순간 소모 뒤에 예외가 난다 — 기동에서 막는다.
        var catalog = new EnchantCatalog();
        var noMythic = Options.Where(o => o.Grade != GlobalRarity.Mythic).ToArray();

        Should.Throw<InvalidOperationException>(() => catalog.Load(noMythic, Grades, Items));
    }

    [Fact]
    public void 칸_수가_상한을_넘으면_예외다()
    {
        var catalog = new EnchantCatalog();
        var grades = new[] { new EnchantGradeTableRow { Grade = GlobalRarity.Mythic, SlotCount = 4, UpPermyriad = 0 } };

        Should.Throw<InvalidOperationException>(() => catalog.Load(Options, grades, Items));
    }

    [Fact]
    public void EnchantOptionTID가_중복되면_예외다()
    {
        var catalog = new EnchantCatalog();
        var dup = Options.Append(Options[0]).ToArray();

        Should.Throw<InvalidOperationException>(() => catalog.Load(dup, Grades, Items));
    }

    [Fact]
    public void 큐브_TID가_중복되면_예외다()
    {
        var catalog = new EnchantCatalog();
        var dup = Items.Append(Items[0]).ToArray();

        Should.Throw<InvalidOperationException>(() => catalog.Load(Options, Grades, dup));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void 값이_0_이하인_옵션이_있으면_예외다(int value)
    {
        // 음수 칸은 경험치 배율(1000 + 가산)을 0 이하로 끌어내릴 수 있다 — 기동에서 막는다.
        var catalog = new EnchantCatalog();
        var bad = new EnchantOptionTableRow { EnchantOptionTID = 1199, Grade = GlobalRarity.Common, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None, Value = value, Weight = 100 };

        Should.Throw<InvalidOperationException>(() => catalog.Load(Options.Append(bad).ToArray(), Grades, Items));
    }
}
