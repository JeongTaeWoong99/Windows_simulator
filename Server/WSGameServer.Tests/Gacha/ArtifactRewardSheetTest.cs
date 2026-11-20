using GameData;

namespace WSGameServer;

/// <summary>보상 풀에서 아티팩트 합계 확률과 1회 지급 수량을 검증한다.</summary>
public class ArtifactRewardSheetTest
{
    public ArtifactRewardSheetTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public void 아티팩트는_한_번에_한_개이고_풀별_합계_확률은_최대_일_퍼센트다()
    {
        var artifactRows = GameTable.GachaArtifactTable.All;
        artifactRows.ShouldAllBe(row => row.Count == 1 && row.Weight > 0);

        foreach (var pool in artifactRows.GroupBy(row => row.GachaId))
        {
            long artifactWeight = pool.Sum(row => (long)row.Weight);
            (artifactWeight * 100).ShouldBeLessThanOrEqualTo(TotalWeightOf(pool.Key));
        }
    }

    [Theory]
    [InlineData(24, 8, new[] { 9, 12, 15, 18, 21 })]
    [InlineData(25, 10, new[] { 10, 13, 16, 19, 22 })]
    [InlineData(26, 8, new[] { 27 })]
    [InlineData(27, 8, new[] { 28 })]
    [InlineData(28, 8, new[] { 29 })]
    public void 신규_아티팩트는_정한_획득처에서_지정_확률로_나온다(int artifactTid, int chancePermille, int[] poolIds)
    {
        var entries = GameTable.GachaArtifactTable.All.Where(row => row.ArtifactTID == artifactTid).ToArray();
        entries.Select(row => row.GachaId).ShouldBe(poolIds, ignoreOrder: true);
        foreach (var entry in entries)
        {
            (entry.Weight * 1_000L).ShouldBe(TotalWeightOf(entry.GachaId) * chancePermille);
        }
    }

    private static long TotalWeightOf(int poolId)
    {
        return GameTable.GachaItemTable.All.Where(row => row.GachaId == poolId).Sum(row => (long)row.Weight)
             + GameTable.GachaEquipTable.All.Where(row => row.GachaId == poolId).Sum(row => (long)row.Weight)
             + GameTable.GachaCharacterTable.All.Where(row => row.GachaId == poolId).Sum(row => (long)row.Weight)
             + GameTable.GachaGoldTable.All.Where(row => row.GachaId == poolId).Sum(row => (long)row.Weight)
             + GameTable.GachaArtifactTable.All.Where(row => row.GachaId == poolId).Sum(row => (long)row.Weight);
    }
}
