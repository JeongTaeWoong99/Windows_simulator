using GameData;

namespace WSGameServer;

/// <summary>기본 적성이 상한을 넘는 캐릭터 행은 기동을 막아야 한다 — 통과시키면 그 캐릭터는 영영 포인트를 못 찍는다.</summary>
public class CharacterTableValidatorTest
{
    [Fact]
    public void 기본_적성이_상한을_넘는_행이_있으면_예외다()
    {
        var rows = new[]
        {
            new CharacterTableRow { CharacterTID = 1, Fishing = 3, FishingCap = 2 },
        };

        var e = Should.Throw<InvalidOperationException>(() => CharacterTableValidator.Validate(rows));
        e.Message.ShouldContain("CharacterTID 1 Fishing");
    }

    [Fact]
    public void 전부_상한_이하면_통과한다()
    {
        var rows = new[]
        {
            new CharacterTableRow { CharacterTID = 1, Fishing = 2, FishingCap = 2, Farming = 0, FarmingCap = 1 },
        };

        Should.NotThrow(() => CharacterTableValidator.Validate(rows));
    }

    [Fact]
    public void 실제_엑셀_데이터는_통과한다()
    {
        GameTableFixture.EnsureLoaded();

        Should.NotThrow(() => CharacterTableValidator.Validate(GameTable.CharacterTable.All));
    }
}
