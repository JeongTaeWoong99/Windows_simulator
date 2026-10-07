using GameData;

namespace WSGameServer;

/// <summary>
/// 응축 ★표. 행의 <c>RequiredCount</c>는 그 단계의 몫이고 ★은 <b>누적</b>으로 판정한다 —
/// 단계 몫과 누적을 헷갈리면 ★2부터 기준이 통째로 어긋난다.
/// </summary>
public class CharacterStarCatalogTest
{
    // 실데이터와 같은 모양 — 8 · 14 · 22 · 32 → 누적 8 · 22 · 44 · 76
    private static CharacterStarCatalog Catalog()
    {
        var catalog = new CharacterStarCatalog();
        catalog.Load(new[]
        {
            new CharacterStarTableRow { CharacterStarTID = 1, RequiredCount = 8,  SpeedAddPermille = 100 },
            new CharacterStarTableRow { CharacterStarTID = 2, RequiredCount = 14, SpeedAddPermille = 200 },
            new CharacterStarTableRow { CharacterStarTID = 3, RequiredCount = 22, SpeedAddPermille = 300 },
            new CharacterStarTableRow { CharacterStarTID = 4, RequiredCount = 32, SpeedAddPermille = 500 },
        });
        return catalog;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(21, 1)]
    [InlineData(22, 2)]
    [InlineData(44, 3)]
    [InlineData(75, 3)]
    [InlineData(76, 4)]
    public void 별은_누적_재료_수로_판정한다(int condenseCount, int star)
    {
        Catalog().StarAt(condenseCount).ShouldBe(star);
    }

    [Fact]
    public void 최고_누적은_단계_몫의_합이다()
    {
        Catalog().MaxCount.ShouldBe(76);
    }

    [Fact]
    public void 별0의_속도_가산은_0이다()
    {
        Catalog().SpeedAddAt(0).ShouldBe(0);
    }

    [Fact]
    public void 별4의_속도_가산은_행의_총량이다()
    {
        // 누적하지 않는다 — 100 + 200 + 300 + 500이 아니다
        Catalog().SpeedAddAt(4).ShouldBe(500);
    }

    [Fact]
    public void 별이_빠지면_기동이_실패한다()
    {
        var catalog = new CharacterStarCatalog();

        Should.Throw<InvalidOperationException>(() => catalog.Load(new[]
        {
            new CharacterStarTableRow { CharacterStarTID = 1, RequiredCount = 8, SpeedAddPermille = 100 },
            new CharacterStarTableRow { CharacterStarTID = 3, RequiredCount = 22, SpeedAddPermille = 300 },
        }));
    }
}
