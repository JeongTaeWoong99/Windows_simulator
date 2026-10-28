using GameData;

namespace WSGameServer;

/// <summary>
/// 건물 · 트리 노드 표 검증(T-183) — 건물 레벨 비용 행이 빠지면 오를 수 없는 레벨이,
/// 선행이 다른 건물을 가리키거나 순환하면 영영 못 찍는 노드가 생긴다. 둘 다 기동에서 막는다.
/// </summary>
public class UserTraitCatalogTest
{
    public UserTraitCatalogTest() => GameTableFixture.EnsureLoaded();

    private static UserTraitTableRow Node(int tid, int buildingTid, params int[] required) => new()
    {
        UserTraitTID = tid, Name = $"노드 {tid}", TraitPoint = 1, EffectType = UserTraitEffect.SpeedAdd,
        Industry = IndustryType.Excavation, EffectValue = 50, BaseLevel = 0, MaxLevel = 2,
        BuildingTID = buildingTid, RequiredTraitTIDs = required,
    };

    private static void Load(params UserTraitTableRow[] nodes)
    {
        var (buildings, levels) = TestBuildings.TwoFree();
        new UserTraitCatalog().Load(nodes, buildings, levels);
    }

    [Fact]
    public void 실데이터는_건물_5채_노드_16개가_작업장에_있고_산업마다_개척이_하나다()
    {
        var catalog = new UserTraitCatalog();

        catalog.LoadAll();

        catalog.Count.ShouldBe(16);
        catalog.Buildings.Count().ShouldBe(5);
        catalog.NodesOf(TestBuildings.Workshop).Count.ShouldBe(16);
        foreach (var industry in new[] { IndustryType.Farming, IndustryType.Excavation, IndustryType.Mining, IndustryType.Logging, IndustryType.Hunting })
        {
            catalog.TryGetIndustryUnlock(industry, out var pioneer).ShouldBeTrue();
            pioneer.BaseLevel.ShouldBe(1);
            pioneer.MaxLevel.ShouldBe(5);
        }
    }

    [Fact]
    public void 실데이터의_작업장은_Lv150까지_비용이_있다()
    {
        var catalog = new UserTraitCatalog();

        catalog.LoadAll();

        catalog.TryGetBuilding(TestBuildings.Workshop, out var workshop).ShouldBeTrue();
        workshop.MaxLevel.ShouldBe(150);
        catalog.TryGetBuildingLevel(TestBuildings.Workshop, 150, out _).ShouldBeTrue();
    }

    [Fact]
    public void 건물_레벨_비용_행이_빠지면_로드에서_멈춘다()
    {
        var catalog = new UserTraitCatalog();

        Should.Throw<InvalidOperationException>(() => catalog.Load(
            Array.Empty<UserTraitTableRow>(),
            new[] { TestBuildings.Building(TestBuildings.Office, 3) },
            TestBuildings.FreeLevels(TestBuildings.Office, 2)));
    }

    [Fact]
    public void 노드의_건물이_건물_표에_없으면_로드에서_멈춘다()
    {
        Should.Throw<InvalidOperationException>(() => Load(Node(201, buildingTid: 9)));
    }

    [Fact]
    public void 선행_노드가_다른_건물에_있으면_로드에서_멈춘다()
    {
        Should.Throw<InvalidOperationException>(() => Load(
            Node(201, TestBuildings.Office),
            Node(202, TestBuildings.Workshop, required: 201)));
    }

    [Fact]
    public void 선행이_순환하면_로드에서_멈춘다()
    {
        Should.Throw<InvalidOperationException>(() => Load(
            Node(201, TestBuildings.Workshop, required: 202),
            Node(202, TestBuildings.Workshop, required: 201)));
    }

    [Fact]
    public void 없는_노드를_선행으로_두면_로드에서_멈춘다()
    {
        Should.Throw<InvalidOperationException>(() => Load(Node(201, TestBuildings.Workshop, required: 999)));
    }
}
