using GameData;

namespace WSGameServer;

/// <summary>
/// 적성 포인트 규칙 검증 (캐릭터 기획 5.4). <b>실효 적성 = 기본값 + 찍은 보너스</b>이고,
/// 남은 포인트 = 레벨로 번 총합 − 찍은 합이다. 여기서 어긋나면 상한을 넘거나 공짜 포인트가 생긴다.
/// </summary>
public class CharacterAptitudeTest
{
    // Lv10·Lv20에 1포인트씩. 만렙 20.
    private static CharacterLevelCatalog Curve()
    {
        var rows = new List<CharacterLevelTableRow>();
        for (var lv = 1; lv <= 20; lv++)
        {
            rows.Add(new CharacterLevelTableRow { CharacterLevelTID = lv, RequiredExp = lv == 1 ? 0 : 10, AptitudePoint = lv % 10 == 0 ? 1 : 0 });
        }

        var curve = new CharacterLevelCatalog();
        curve.Load(rows);
        return curve;
    }

    // 낚시 기본 3 · 상한 5, 농사 기본 0 · 상한 2, 채굴 기본 4 · 상한 4(이미 상한).
    private static CharacterTableRow Row() => new()
    {
        CharacterTID = 1001, Name = "테스트",
        Fishing = 3, FishingCap = 5,
        Farming = 0, FarmingCap = 2,
        Mining  = 4, MiningCap  = 4,
    };

    private static Character Make(int level, AptitudeBonus bonus = default)
        => new(1, Row(), level, exp: 0, bonus);

    [Fact]
    public void 실효_적성은_기본값에_찍은_보너스를_더한_값이다()
    {
        var character = Make(level: 1, new AptitudeBonus(Fishing: 2));

        character.GetAptitude(IndustryType.Fishing).ShouldBe(5);
        character.GetAptitude(IndustryType.Farming).ShouldBe(0);
    }

    [Fact]
    public void 포인트는_10레벨마다_하나씩_쌓인다()
    {
        // Lv19는 1개(Lv10), Lv20은 2개. 레벨마다 주면 19·20이 된다.
        Make(level: 19).RemainingPoints(Curve()).ShouldBe(1);
        Make(level: 20).RemainingPoints(Curve()).ShouldBe(2);
        Make(level: 9).RemainingPoints(Curve()).ShouldBe(0);
    }

    [Fact]
    public void 찍은_만큼_남은_포인트가_줄어든다()
    {
        // Lv20 = 2포인트, 낚시에 1·농사에 1을 찍었으면 0.
        Make(level: 20, new AptitudeBonus(Fishing: 1, Farming: 1)).RemainingPoints(Curve()).ShouldBe(0);
    }

    [Fact]
    public void 포인트를_찍으면_그_산업만_1_오른다()
    {
        var character = Make(level: 10);

        character.TryRaiseAptitude(IndustryType.Fishing, Curve()).ShouldBe(AptitudeRaiseResult.Ok);

        character.GetAptitude(IndustryType.Fishing).ShouldBe(4);
        character.GetAptitude(IndustryType.Farming).ShouldBe(0);
        character.RemainingPoints(Curve()).ShouldBe(0);
    }

    [Fact]
    public void 남은_포인트가_없으면_올리지_못한다()
    {
        var character = Make(level: 9);

        character.TryRaiseAptitude(IndustryType.Fishing, Curve()).ShouldBe(AptitudeRaiseResult.NoPoint);
        character.GetAptitude(IndustryType.Fishing).ShouldBe(3);
    }

    [Fact]
    public void 상한에_닿은_산업은_올리지_못한다()
    {
        // 포인트는 있지만(Lv10) 채굴은 기본 4 = 상한 4.
        var character = Make(level: 10);

        character.TryRaiseAptitude(IndustryType.Mining, Curve()).ShouldBe(AptitudeRaiseResult.AtCap);
        character.GetAptitude(IndustryType.Mining).ShouldBe(4);
        character.RemainingPoints(Curve()).ShouldBe(1);
    }

    [Fact]
    public void 적성_0인_산업도_1로_올릴_수_있다()
    {
        var character = Make(level: 10);

        character.TryRaiseAptitude(IndustryType.Farming, Curve()).ShouldBe(AptitudeRaiseResult.Ok);

        character.GetAptitude(IndustryType.Farming).ShouldBe(1);
        character.CanWork(IndustryType.Farming).ShouldBeTrue();
    }

    [Fact]
    public void 상한은_테이블_값이다()
    {
        Make(level: 1).GetAptitudeCap(IndustryType.Fishing).ShouldBe(5);
        Make(level: 1).GetAptitudeCap(IndustryType.Farming).ShouldBe(2);
    }
}
