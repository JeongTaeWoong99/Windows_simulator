using GameData;

namespace WSGameServer;

/// <summary>
/// 유물·아티팩트 시트(T-150) 검증 — 엑셀만 고쳐도 깨지면 안 되는 규칙을 실데이터로 본다.
/// </summary>
public class RelicSheetTest
{
    private const int SpecialRelicTid = 100067;

    public RelicSheetTest() => GameTableFixture.EnsureLoaded();

    private static List<ItemTableRow> Relics => GameTable.ItemTable.All.Where(r => r.ItemType == ItemType.Relic).ToList();

    [Fact]
    public void 유물은_희귀도마다_하나이고_전부_감정_비용과_풀이_있다()
    {
        Relics.Select(r => r.GlobalRarity).ShouldBe(new[]
        {
            GlobalRarity.Common, GlobalRarity.Uncommon, GlobalRarity.Rare, GlobalRarity.Epic,
            GlobalRarity.Legendary, GlobalRarity.Mythic, GlobalRarity.Special,
        }, ignoreOrder: true);

        Relics.ShouldAllBe(r => r.OpenCostGold > 0 && GameTable.GachaInfoTable.Contains(r.OpenGachaId));
    }

    [Fact]
    public void Special_유물이_가장_비싸다()
    {
        Relics.MaxBy(r => r.OpenCostGold)!.ItemTID.ShouldBe(SpecialRelicTid);
    }

    [Fact]
    public void 발굴은_레벨마다_상자_대신_유물만_나오고_Special은_판정에서_나오지_않는다()
    {
        var rows = GameTable.CommonRewardOverrideTable.All.Where(r => r.IndustryType == IndustryType.Excavation).ToList();

        rows.Select(r => r.IndustryLevel).Distinct().OrderBy(l => l).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        rows.ShouldAllBe(r => GameTable.ItemTable[r.ItemTID].ItemType == ItemType.Relic);
        rows.ShouldNotContain(r => r.ItemTID == SpecialRelicTid);
    }

    [Fact]
    public void Special_유물은_다른_유물을_감정할_때만_나오고_자기_풀에는_없다()
    {
        var special = GameTable.ItemTable[SpecialRelicTid];
        var fromPools = GameTable.GachaItemTable.All.Where(r => r.ItemTID == SpecialRelicTid).Select(r => r.GachaId).ToHashSet();

        fromPools.ShouldBe(Relics.Where(r => r.ItemTID != SpecialRelicTid).Select(r => r.OpenGachaId).ToHashSet(), ignoreOrder: true);
        fromPools.ShouldNotContain(special.OpenGachaId);
    }

    [Fact]
    public void 감정_풀에는_열_수_있는_아이템이_없어_고리가_한_번에_끝난다()
    {
        var pools = Relics.Select(r => r.OpenGachaId).ToHashSet();

        GameTable.GachaItemTable.All
            .Where(r => pools.Contains(r.GachaId) && r.ItemTID != SpecialRelicTid)
            .ShouldAllBe(r => GameTable.ItemTable[r.ItemTID].OpenGachaId == 0);
    }

    [Fact]
    public void 감정_풀의_아티팩트는_유물과_같은_등급이다()
    {
        var gradeOfPool = Relics.ToDictionary(r => r.OpenGachaId, r => r.GlobalRarity);

        GameTable.GachaArtifactTable.All.ShouldNotBeEmpty();
        GameTable.GachaArtifactTable.All
            .ShouldAllBe(r => gradeOfPool.ContainsKey(r.GachaId) && GameTable.ArtifactTable[r.ArtifactTID].GlobalRarity == gradeOfPool[r.GachaId]);
    }

    [Fact]
    public void 아티팩트_번호는_1부터_실린_순서로_늘어난다()
    {
        // 뺀 번호는 결번으로 남으므로 빈틈은 허용한다 — 순서만 지킨다(유물감정.md 3.2).
        var tids = GameTable.ArtifactTable.All.Select(a => a.ArtifactTID).ToList();

        tids[0].ShouldBe(1);
        tids.ShouldBeInOrder(SortDirection.Ascending);
    }
}
