using GameData;

namespace WSGameServer;

/// <summary>테스트 전용 건물 표 — 엑셀 값에 기대지 않는다.</summary>
internal static class TestBuildings
{
    public const int Office   = 1;
    public const int Workshop = 2;

    public static BuildingTableRow Building(int tid, int maxLevel)
        => new() { BuildingTID = tid, Name = $"건물 {tid}", MaxLevel = maxLevel };

    /// <summary>비용 없는 레벨 행 Lv1~최대. 트리 테스트는 레벨 비용을 보지 않는다.</summary>
    public static IEnumerable<BuildingLevelTableRow> FreeLevels(int tid, int maxLevel)
        => Enumerable.Range(1, maxLevel).Select(level => new BuildingLevelTableRow
        {
            BuildingLevelTID = tid * 1000 + level, BuildingTID = tid, Level = level,
        });

    /// <summary>관리소 · 작업장 두 채, 최대 Lv10, 비용 없음.</summary>
    public static (BuildingTableRow[] Buildings, BuildingLevelTableRow[] Levels) TwoFree()
        => (new[] { Building(Office, 10), Building(Workshop, 10) },
            FreeLevels(Office, 10).Concat(FreeLevels(Workshop, 10)).ToArray());
}
