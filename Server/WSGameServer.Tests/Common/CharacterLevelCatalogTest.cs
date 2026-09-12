using GameData;

namespace WSGameServer;

/// <summary>
/// 캐릭터 레벨 곡선 보관소 검증. <b>마지막 행이 곧 만렙</b>이라는 규약이 여기서 지켜진다 —
/// 어긋나면 만렙 판정이 밀려 레벨이 테이블 밖으로 올라간다.
/// </summary>
public class CharacterLevelCatalogTest
{
    private static CharacterLevelTableRow Row(int level, int requiredExp)
        => new() { CharacterLevelTID = level, RequiredExp = requiredExp };

    [Fact]
    public void 마지막_행이_만렙이다()
    {
        var catalog = new CharacterLevelCatalog();

        // 순서를 섞어 넣어도 최댓값이 만렙이어야 한다.
        catalog.Load(new[] { Row(3, 12), Row(1, 0), Row(5, 17), Row(2, 10), Row(4, 14) });

        catalog.MaxLevel.ShouldBe(5);
    }

    [Fact]
    public void 레벨에_도달하는_데_필요한_경험치를_돌려준다()
    {
        var catalog = new CharacterLevelCatalog();
        catalog.Load(new[] { Row(1, 0), Row(2, 10) });

        catalog.TryGetRequiredExp(2, out var required).ShouldBeTrue();
        required.ShouldBe(10);
        catalog.TryGetRequiredExp(3, out _).ShouldBeFalse();
    }

    [Fact]
    public void 행이_없으면_만렙은_1이다()
    {
        // 곡선이 비어 있으면 아무도 성장하지 않는다 — 조용히 무한 레벨업이 되는 것보다 낫다.
        new CharacterLevelCatalog().MaxLevel.ShouldBe(1);
    }

    [Fact]
    public void 레벨이_중복되면_예외를_던진다()
    {
        var catalog = new CharacterLevelCatalog();

        Should.Throw<InvalidOperationException>(() => catalog.Load(new[] { Row(2, 10), Row(2, 99) }));
    }
}
