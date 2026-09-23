using GameData;

namespace WSGameServer;

/// <summary>
/// 인챈트 — 효과 합산. Equip이 옵션 Row를 들고 기본값 위에 더할 뿐, 부여·재롤은 다루지 않는다(Task 6).
/// </summary>
public class UserEnchantTest
{
    private static readonly EnchantOptionTableRow FishSpeed =
        new() { EnchantOptionTID = 103, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = 40, Weight = 120 };

    private static readonly EnchantOptionTableRow AllSpeed =
        new() { EnchantOptionTID = 101, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 20, Weight = 300 };

    private static readonly EnchantOptionTableRow FarmSpeed =
        new() { EnchantOptionTID = 102, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Farming, Value = 40, Weight = 120 };

    private static readonly EnchantOptionTableRow Exp =
        new() { EnchantOptionTID = 107, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None, Value = 30, Weight = 100 };

    private static Equip RodWith(params EnchantOptionTableRow[] options)
    {
        // 낚시 무기 +30%. 인챈트 줄이 그 위에 더해진다.
        var row = new EquipTableRow
        {
            EquipTID = 1002, Name = "대", EquipKind = EquipKind.Weapon,
            Industry = IndustryType.Fishing, SpeedAddPermille = 300,
        };

        var equip = new Equip(11, row, 0);
        equip.SetEnchant(GlobalRarity.Rare, options);
        return equip;
    }

    [Fact]
    public void 산업이_일치하는_줄과_전_산업_줄이_기본값에_더해진다()
    {
        var equip = RodWith(FishSpeed, AllSpeed);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40 + 20);
    }

    [Fact]
    public void 산업이_다른_줄은_더해지지_않는다()
    {
        var equip = RodWith(FarmSpeed, AllSpeed);

        // 장비 자체가 낚시 전용이라 농사 슬롯에서는 기본값도 0이다.
        equip.SpeedAddPermilleFor(IndustryType.Farming).ShouldBe(40 + 20);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 20);
    }

    [Fact]
    public void 경험치_줄은_속도에_섞이지_않는다()
    {
        var equip = RodWith(Exp, FishSpeed);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40);
        equip.ExpAddPermille.ShouldBe(30);
    }

    [Fact]
    public void 인챈트가_없으면_기본값만_남는다()
    {
        var row = new EquipTableRow
        {
            EquipTID = 1002, Name = "대", EquipKind = EquipKind.Weapon,
            Industry = IndustryType.Fishing, SpeedAddPermille = 300,
        };
        var equip = new Equip(11, row, 0);

        equip.EnchantGrade.ShouldBe(GlobalRarity.None);
        equip.EnchantLineCount.ShouldBe(0);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300);
        equip.ExpAddPermille.ShouldBe(0);
    }
}
