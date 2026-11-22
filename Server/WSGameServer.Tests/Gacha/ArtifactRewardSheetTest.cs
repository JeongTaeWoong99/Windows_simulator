using GameData;

namespace WSGameServer;

/// <summary>보상 풀에서 아티팩트 합계 확률과 1회 지급 수량을 검증한다.</summary>
public class ArtifactRewardSheetTest
{
    public ArtifactRewardSheetTest() => GameTableFixture.EnsureLoaded();

    /// <summary>테마 상자 6종의 열기 풀 (레벨테마 7장 — 아티팩트 칸 1.01%).</summary>
    private static readonly int[] ThemeBoxPools = { 9, 12, 15, 18, 21, 37 };

    [Fact]
    public void 아티팩트는_한_번에_한_개이고_테마_상자_밖의_풀별_합계_확률은_최대_일_퍼센트다()
    {
        var artifactRows = GameTable.GachaArtifactTable.All;
        artifactRows.ShouldAllBe(row => row.Count == 1 && row.Weight > 0);

        foreach (var pool in artifactRows.GroupBy(row => row.GachaId).Where(g => !ThemeBoxPools.Contains(g.Key)))
        {
            long artifactWeight = pool.Sum(row => (long)row.Weight);
            (artifactWeight * 100).ShouldBeLessThanOrEqualTo(TotalWeightOf(pool.Key));
        }
    }

    [Fact]
    public void 테마_상자는_장인의_나무_자와_은빛_수확패를_반씩_합계_1_01퍼센트로_준다()
    {
        // 칸 1.01% = 10억 중 10,100,000 → 반씩 5,050,000 (레벨테마 7.1)
        foreach (var poolId in ThemeBoxPools)
        {
            TotalWeightOf(poolId).ShouldBe(1_000_000_000L, $"풀 {poolId}");
            GameTable.GachaArtifactTable.All.Where(row => row.GachaId == poolId)
                .Select(row => (row.ArtifactTID, row.Weight)).OrderBy(x => x.ArtifactTID).ToArray()
                .ShouldBe(new[] { (24, 5_050_000), (25, 5_050_000) }, $"풀 {poolId}");
        }
    }

    [Theory]
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
