using GameData;

namespace WSGameServer;

/// <summary>레벨형 특성 표 검증 — 기본 레벨 다음부터 최대 레벨까지 레벨 행이 빠지면 오를 수 없는 레벨이 생긴다.</summary>
public class UserTraitCatalogTest
{
    public UserTraitCatalogTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public void 실데이터는_특성_16개가_빠짐없이_등록되고_산업마다_개척이_하나다()
    {
        var catalog = new UserTraitCatalog();

        catalog.LoadAll();

        catalog.Count.ShouldBe(16);
        foreach (var industry in new[] { IndustryType.Farming, IndustryType.Fishing, IndustryType.Mining, IndustryType.Logging, IndustryType.Hunting })
        {
            catalog.TryGetIndustryUnlock(industry, out var pioneer).ShouldBeTrue();
            pioneer.BaseLevel.ShouldBe(1);
            pioneer.MaxLevel.ShouldBe(5);
        }
    }

    [Fact]
    public void 레벨_행이_빠지면_로드에서_멈춘다()
    {
        var catalog = new UserTraitCatalog();
        var trait = new UserTraitTableRow { UserTraitTID = 202, Name = "낚시 속도", TraitPoint = 1, EffectType = UserTraitEffect.SpeedAdd, Industry = IndustryType.Fishing, EffectValue = 50, BaseLevel = 0, MaxLevel = 2 };

        Should.Throw<InvalidOperationException>(() => catalog.Load(
            new[] { trait },
            new[] { new UserTraitLevelTableRow { UserTraitLevelTID = 20201, UserTraitTID = 202, Level = 1 } }));
    }
}
