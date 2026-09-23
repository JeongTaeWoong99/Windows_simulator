using GameData;

namespace WSGameServer;

/// <summary>
/// <see cref="EnchantCatalog"/> — 등급별 옵션 풀 · 등급 상승 확률 · 아이템 동작.
/// 확률이 코드가 아니라 데이터에 있으므로, 여기서 보는 것은 "표를 그대로 읽었는가"다.
/// </summary>
public class EnchantCatalogTest
{
    private static readonly EnchantOptionTableRow[] Options =
    {
        new() { EnchantOptionTID = 101, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed,        Industry = IndustryType.None,    Value = 20, Weight = 300 },
        new() { EnchantOptionTID = 103, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed,        Industry = IndustryType.Fishing, Value = 40, Weight = 120 },
        new() { EnchantOptionTID = 107, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None,    Value = 30, Weight = 100 },
        new() { EnchantOptionTID = 201, Grade = GlobalRarity.Epic, OptionType = EnchantOptionType.Speed,        Industry = IndustryType.None,    Value = 50, Weight = 300 },
    };

    private static readonly EnchantGradeTableRow[] Grades =
    {
        new() { Grade = GlobalRarity.Rare,      UpPermille = 50 },
        new() { Grade = GlobalRarity.Epic,      UpPermille = 5 },
        new() { Grade = GlobalRarity.Legendary, UpPermille = 0 },
    };

    private static readonly EnchantItemTableRow[] Items =
    {
        new() { ItemTID = 9001, Action = EnchantAction.Grant,      SuccessPermille = 500 },
        new() { ItemTID = 9003, Action = EnchantAction.GradeUp,    SuccessPermille = 1000 },
        new() { ItemTID = 9004, Action = EnchantAction.ExpandLine, SuccessPermille = 300 },
    };

    private static EnchantCatalog Loaded()
    {
        var catalog = new EnchantCatalog();
        catalog.Load(Options, Grades, Items);
        return catalog;
    }

    [Fact]
    public void 등급별로_그_등급의_옵션만_뽑는다()
    {
        var catalog = Loaded();

        var rolled = catalog.RollOptions(GlobalRarity.Rare, 2, new Random(1));

        rolled.Count.ShouldBe(2);
        rolled.ShouldAllBe(o => o.Grade == GlobalRarity.Rare);
    }

    [Fact]
    public void 같은_줄이_겹쳐_나올_수_있다()
    {
        // 후보를 한 줄로 줄이면 2줄 모두 그 줄이어야 한다 — 중복 허용이 설계다.
        var catalog = new EnchantCatalog();
        catalog.Load(new[] { Options[0] }, Grades, Items);

        var rolled = catalog.RollOptions(GlobalRarity.Rare, 2, new Random(1));

        rolled.Select(o => o.EnchantOptionTID).ShouldBe(new[] { 101, 101 });
    }

    [Fact]
    public void 등급_상승_확률은_현재_등급이_정한다()
    {
        var catalog = Loaded();

        catalog.UpPermilleOf(GlobalRarity.Rare).ShouldBe(50);
        catalog.UpPermilleOf(GlobalRarity.Epic).ShouldBe(5);
        catalog.UpPermilleOf(GlobalRarity.Legendary).ShouldBe(0);
    }

    [Fact]
    public void 다음_등급은_Legendary에서_멈춘다()
    {
        EnchantCatalog.NextGrade(GlobalRarity.Rare).ShouldBe(GlobalRarity.Epic);
        EnchantCatalog.NextGrade(GlobalRarity.Epic).ShouldBe(GlobalRarity.Legendary);
        EnchantCatalog.NextGrade(GlobalRarity.Legendary).ShouldBe(GlobalRarity.Legendary);
    }

    [Fact]
    public void 아이템의_동작과_확률을_돌려준다()
    {
        var catalog = Loaded();

        catalog.TryGetItem(9004, out var row).ShouldBeTrue();
        row.Action.ShouldBe(EnchantAction.ExpandLine);
        row.SuccessPermille.ShouldBe(300);

        catalog.TryGetItem(1, out _).ShouldBeFalse();
    }

    [Fact]
    public void EnchantOptionTID가_중복되면_예외다()
    {
        var catalog = new EnchantCatalog();
        var dup = new[] { Options[0], Options[0] };

        Should.Throw<InvalidOperationException>(() => catalog.Load(dup, Grades, Items));
    }

    [Fact]
    public void 후보가_없는_등급을_뽑으면_예외다()
    {
        var catalog = Loaded();

        Should.Throw<InvalidOperationException>(() => catalog.RollOptions(GlobalRarity.Legendary, 2, new Random(1)));
    }
}
