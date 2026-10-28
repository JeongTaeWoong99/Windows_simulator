using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 거점 건물 · 건물 트리 노드(특성) 보관소(T-183). 노드(<c>UserTraitTable</c>)는 한 건물에 속하고 기본 레벨에서 최대 레벨까지 오른다.
/// 건물 레벨 비용은 <c>BuildingLevelTable</c>이다. 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부른다.
/// </summary>
public sealed class UserTraitCatalog : Singleton<UserTraitCatalog>
{
    private readonly Dictionary<int, UserTraitTableRow> _byTid = new();
    private readonly Dictionary<IndustryType, UserTraitTableRow> _industryUnlocks = new();
    private readonly List<UserTraitTableRow> _speedAdds = new();
    private readonly List<UserTraitTableRow> _yieldAdds = new();
    private readonly Dictionary<int, BuildingTableRow> _buildings = new();
    private readonly Dictionary<(int Building, int Level), BuildingLevelTableRow> _buildingLevels = new();
    private readonly Dictionary<int, List<UserTraitTableRow>> _nodesByBuilding = new();

    public int Count => _byTid.Count;

    public IEnumerable<UserTraitTableRow> All => _byTid.Values;

    public IEnumerable<BuildingTableRow> Buildings => _buildings.Values;

    /// <summary>속도 가산 특성 전부. 슬롯 속도를 낼 때 레벨만큼 더한다.</summary>
    public IReadOnlyList<UserTraitTableRow> SpeedAdds => _speedAdds;

    /// <summary>산출량 가산 특성 전부. 판정의 자원 개수를 낼 때 레벨만큼 더한다.</summary>
    public IReadOnlyList<UserTraitTableRow> YieldAdds => _yieldAdds;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.UserTraitTable.All, GameTable.BuildingTable.All, GameTable.BuildingLevelTable.All);
    }

    /// <summary>인덱스를 만들고 검증한다. 데이터 오류면 예외 — 조용히 돌면 오를 수 없는 건물 · 찍을 수 없는 노드가 생긴다.</summary>
    public void Load(
        IEnumerable<UserTraitTableRow>     traits,
        IEnumerable<BuildingTableRow>      buildings,
        IEnumerable<BuildingLevelTableRow> buildingLevels)
    {
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(buildingLevels);

        _byTid.Clear();
        _industryUnlocks.Clear();
        _speedAdds.Clear();
        _yieldAdds.Clear();
        _buildings.Clear();
        _buildingLevels.Clear();
        _nodesByBuilding.Clear();

        LoadBuildings(buildings, buildingLevels);

        foreach (var row in traits)
        {
            if (!_byTid.TryAdd(row.UserTraitTID, row))
            {
                throw new InvalidOperationException($"UserTraitTable에 UserTraitTID가 중복됐습니다: {row.UserTraitTID}");
            }

            if (!_buildings.ContainsKey(row.BuildingTID))
            {
                throw new InvalidOperationException($"특성 {row.UserTraitTID}의 건물 {row.BuildingTID}이(가) BuildingTable에 없습니다");
            }

            _nodesByBuilding[row.BuildingTID].Add(row);

            switch (row.EffectType)
            {
                case UserTraitEffect.SpeedAdd:
                    _speedAdds.Add(row);
                    break;
                case UserTraitEffect.YieldAdd:
                    _yieldAdds.Add(row);
                    break;
                case UserTraitEffect.IndustryUnlock when !_industryUnlocks.TryAdd(row.Industry, row):
                    throw new InvalidOperationException($"산업 {row.Industry}의 개척 특성이 둘입니다: {row.UserTraitTID}");
            }
        }

        ValidateRequirements();
    }

    // 건물마다 Lv1부터 최대 레벨까지 비용 행이 빠짐없이 있어야 한다. 자원은 TID·수량 두 배열의 짝이다.
    private void LoadBuildings(IEnumerable<BuildingTableRow> buildings, IEnumerable<BuildingLevelTableRow> levels)
    {
        foreach (var row in buildings)
        {
            if (!_buildings.TryAdd(row.BuildingTID, row))
            {
                throw new InvalidOperationException($"BuildingTable에 BuildingTID가 중복됐습니다: {row.BuildingTID}");
            }

            _nodesByBuilding[row.BuildingTID] = new List<UserTraitTableRow>();
        }

        foreach (var row in levels)
        {
            if (!_buildings.ContainsKey(row.BuildingTID))
            {
                throw new InvalidOperationException($"BuildingLevelTable {row.BuildingLevelTID}의 건물 {row.BuildingTID}이(가) BuildingTable에 없습니다");
            }

            if (row.ItemTIDs.Length != row.ItemCounts.Length || row.ItemCounts.Any(c => c <= 0))
            {
                throw new InvalidOperationException($"BuildingLevelTable {row.BuildingLevelTID}의 ItemTIDs·ItemCounts 짝이 어긋났거나 0 이하 수량이 있습니다");
            }

            if (!_buildingLevels.TryAdd((row.BuildingTID, row.Level), row))
            {
                throw new InvalidOperationException($"건물 {row.BuildingTID}의 Lv{row.Level} 행이 둘입니다");
            }
        }

        foreach (var building in _buildings.Values)
        {
            for (var level = 1; level <= building.MaxLevel; level++)
            {
                if (!_buildingLevels.ContainsKey((building.BuildingTID, level)))
                {
                    throw new InvalidOperationException($"건물 {building.BuildingTID}의 Lv{level} 비용 행이 없습니다");
                }
            }
        }
    }

    // 선행 노드는 같은 건물 안에 있어야 하고 순환하면 안 된다 — 어느 쪽이든 영영 못 찍는 노드가 생긴다.
    private void ValidateRequirements()
    {
        var visiting = new HashSet<int>();
        var done     = new HashSet<int>();

        foreach (var tid in _byTid.Keys)
        {
            Visit(tid);
        }

        return;

        void Visit(int tid)
        {
            if (done.Contains(tid))
            {
                return;
            }

            if (!visiting.Add(tid))
            {
                throw new InvalidOperationException($"특성 선행이 순환합니다: {tid} (RequiredTraitTIDs를 확인하세요)");
            }

            var node = _byTid[tid];
            foreach (var required in node.RequiredTraitTIDs)
            {
                if (!_byTid.TryGetValue(required, out var pre))
                {
                    throw new InvalidOperationException($"특성 {tid}의 선행 {required}이(가) 테이블에 없습니다");
                }

                if (pre.BuildingTID != node.BuildingTID)
                {
                    throw new InvalidOperationException($"특성 {tid}의 선행 {required}이(가) 다른 건물에 있습니다");
                }

                Visit(required);
            }

            visiting.Remove(tid);
            done.Add(tid);
        }
    }

    public bool TryGet(int userTraitTid, out UserTraitTableRow row)
        => _byTid.TryGetValue(userTraitTid, out row!);

    public bool TryGetBuilding(int buildingTid, out BuildingTableRow row)
        => _buildings.TryGetValue(buildingTid, out row!);

    /// <summary>이 레벨에 <b>도달하는</b> 비용 행(Lv−1 → Lv).</summary>
    public bool TryGetBuildingLevel(int buildingTid, int level, out BuildingLevelTableRow row)
        => _buildingLevels.TryGetValue((buildingTid, level), out row!);

    /// <summary>이 건물의 트리 노드 전부. 없는 건물이면 빈 목록.</summary>
    public IReadOnlyList<UserTraitTableRow> NodesOf(int buildingTid)
    {
        if (!_nodesByBuilding.TryGetValue(buildingTid, out var nodes))
        {
            return Array.Empty<UserTraitTableRow>();
        }

        return nodes;
    }

    /// <summary>이 산업의 개척 특성. 특성 레벨이 곧 열린 산업 레벨이다.</summary>
    public bool TryGetIndustryUnlock(IndustryType industry, out UserTraitTableRow row)
        => _industryUnlocks.TryGetValue(industry, out row!);
}
