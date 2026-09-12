using GameData;

namespace WSGameServer;

/// <summary>
/// 캐릭터 경험치 → 레벨업 규칙 검증 (캐릭터 기획 5.2).
///
/// <b>Exp는 "현재 레벨에서 쌓은 양"이고 레벨업하면 필요치를 뺀 나머지가 이월된다.</b>
/// 여기서 어긋나면 같은 판정 횟수로 유저마다 다른 레벨이 나오거나, 만렙 위로 값이 새어 나간다.
/// </summary>
public class CharacterGrowthTest
{
    private static CharacterLevelCatalog Curve()
    {
        // Lv2 10 · Lv3 12 · Lv4 14. 마지막 행(Lv4)이 곧 만렙이다.
        var curve = new CharacterLevelCatalog();
        curve.Load(new[]
        {
            new CharacterLevelTableRow { CharacterLevelTID = 1, RequiredExp = 0 },
            new CharacterLevelTableRow { CharacterLevelTID = 2, RequiredExp = 10 },
            new CharacterLevelTableRow { CharacterLevelTID = 3, RequiredExp = 12 },
            new CharacterLevelTableRow { CharacterLevelTID = 4, RequiredExp = 14 },
        });
        return curve;
    }

    private static Character Make(int level = 1, int exp = 0)
        => new(1, new CharacterTableRow { CharacterTID = 1001, Name = "테스트" }, level, exp);

    [Fact]
    public void 경험치를_채우면_레벨이_오르고_남은_경험치는_이월된다()
    {
        var character = Make();

        // 13 = Lv2 필요치 10 + 3. 이월이 없으면 Exp가 0 또는 13으로 나온다.
        character.GainExp(13, Curve()).ShouldBe(1);

        character.Level.ShouldBe(2);
        character.Exp.ShouldBe(3);
    }

    [Fact]
    public void 한_번에_여러_레벨이_오를_수_있다()
    {
        var character = Make();

        // 25 → Lv2(10) 남은 15 → Lv3(12) 남은 3. 한 정산에 판정이 여러 번 쌓이는 경우다.
        character.GainExp(25, Curve()).ShouldBe(2);

        character.Level.ShouldBe(3);
        character.Exp.ShouldBe(3);
    }

    [Fact]
    public void 만렙에서는_경험치를_쌓지_않는다()
    {
        var character = Make(level: 4);

        character.GainExp(100, Curve()).ShouldBe(0);

        character.Level.ShouldBe(4);
        character.Exp.ShouldBe(0);
    }

    [Fact]
    public void 만렙에_닿으면_남은_조각은_버린다()
    {
        var character = Make(level: 3);

        // 20 → Lv4(14) 남은 6. 상한 위로 쌓이는 값은 의미가 없으니 0이어야 한다.
        character.GainExp(20, Curve()).ShouldBe(1);

        character.Level.ShouldBe(4);
        character.Exp.ShouldBe(0);
    }

    [Fact]
    public void 필요치에_못_미치면_레벨은_그대로고_경험치만_쌓인다()
    {
        var character = Make(exp: 4);

        character.GainExp(5, Curve()).ShouldBe(0);

        character.Level.ShouldBe(1);
        character.Exp.ShouldBe(9);
    }

    [Fact]
    public void 영_이하의_경험치는_무시한다()
    {
        var character = Make(exp: 4);

        character.GainExp(0,  Curve()).ShouldBe(0);
        character.GainExp(-5, Curve()).ShouldBe(0);

        character.Exp.ShouldBe(4);
    }
}
