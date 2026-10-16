using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>건물 노드 하나의 효과. 대상 산업은 노드가 속한 건물의 산업이다.</summary>
public readonly record struct BuildingNodeEffect(IndustryType Industry, BuildingEffectType EffectType, int EffectValue);

/// <summary>
/// <c>UnlockTable</c>·<c>WorkSlotTable</c>·건물 표 보관소. 해금 TID로 조건 행을, 작업슬롯 칸을, 건물 노드 효과를 찾아 준다.
///
/// <para>
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 조회만 한다.
/// 로드하면서 <b>데이터 오류 3종</b>을 검사한다(해금 기획 2.5) — 선행 순환·자기 참조, 한 해금을 두 콘텐츠가 참조,
/// 어느 콘텐츠도 참조하지 않는 해금. 앞 둘은 기동을 막고 마지막은 경고만 남긴다.
/// </para>
/// </summary>
public sealed class UnlockCatalog : Singleton<UnlockCatalog>
{
    private readonly Dictionary<int, UnlockTableRow> _unlocks = new();
    private readonly List<WorkSlotTableRow> _workSlots = new();
    private readonly Dictionary<int, int> _workSlotByUnlock = new();
    private readonly Dictionary<int, BuildingNodeEffect> _buildingNodes = new();

    /// <summary>등록된 해금 수.</summary>
    public int Count => _unlocks.Count;

    /// <summary>작업슬롯 칸 전부, 칸 번호 순. 행 수가 곧 슬롯 상한이다.</summary>
    public IReadOnlyList<WorkSlotTableRow> WorkSlots => _workSlots;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        // 작업슬롯 말고도 해금을 참조하는 콘텐츠. 산업 레벨은 특성(개척)으로 열려 지금은 0이다(T-108).
        var otherReferences = GameTable.IndustryLevelTable.All.Select(r => r.UnlockTID);

        Load(GameTable.UnlockTable.All, GameTable.WorkSlotTable.All, otherReferences,
             GameTable.BuildingTable.All, GameTable.BuildingNodeTable.All);
    }

    /// <summary>행 목록으로 인덱스를 만들고 검증한다. 데이터 오류면 예외 — 조용히 돌면 영영 못 여는 해금이 생긴다.</summary>
    /// <param name="otherReferences">작업슬롯 외 콘텐츠가 참조하는 UnlockTID — 미참조 경고에서 뺀다.</param>
    public void Load(
        IEnumerable<UnlockTableRow>   unlockRows,
        IEnumerable<WorkSlotTableRow> workSlotRows,
        IEnumerable<int>?             otherReferences = null,
        IEnumerable<BuildingTableRow>?     buildingRows     = null,
        IEnumerable<BuildingNodeTableRow>? buildingNodeRows = null)
    {
        ArgumentNullException.ThrowIfNull(unlockRows);
        ArgumentNullException.ThrowIfNull(workSlotRows);

        _unlocks.Clear();
        _workSlots.Clear();
        _workSlotByUnlock.Clear();
        _buildingNodes.Clear();

        foreach (var row in unlockRows)
        {
            if (!_unlocks.TryAdd(row.UnlockTID, row))
            {
                throw new InvalidOperationException($"UnlockTable에 UnlockTID가 중복됐습니다: {row.UnlockTID}");
            }
        }

        _workSlots.AddRange(workSlotRows.OrderBy(r => r.WorkSlotTID));

        ValidateRequirements();
        ValidateItemCosts();
        IndexWorkSlots();
        IndexBuildingNodes(buildingRows ?? Array.Empty<BuildingTableRow>(), buildingNodeRows ?? Array.Empty<BuildingNodeTableRow>());
        WarnUnreferenced(otherReferences?.ToHashSet() ?? new HashSet<int>());
    }

    public bool TryGetUnlock(int unlockTid, out UnlockTableRow row)
        => _unlocks.TryGetValue(unlockTid, out row!);

    /// <summary>이 해금이 여는 작업슬롯 칸 번호. 슬롯이 아닌 해금(산업 레벨 등)이나 0이면 false.</summary>
    public bool TryGetWorkSlotOf(int unlockTid, out int slotIndex)
        => _workSlotByUnlock.TryGetValue(unlockTid, out slotIndex);

    /// <summary>이 해금이 건물 레벨·노드면 그 효과. 레벨 행은 효과가 <c>None</c>이다.</summary>
    public bool TryGetBuildingNode(int unlockTid, out BuildingNodeEffect node)
        => _buildingNodes.TryGetValue(unlockTid, out node);

    // 선행 그래프를 깊이 우선으로 돌며 순환·자기 참조·없는 TID를 잡는다.
    private void ValidateRequirements()
    {
        var visiting = new HashSet<int>();
        var done     = new HashSet<int>();

        foreach (var tid in _unlocks.Keys)
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
                throw new InvalidOperationException($"UnlockTable 선행이 순환합니다: {tid} (RequiredUnlockTIDs를 확인하세요)");
            }

            foreach (var required in _unlocks[tid].RequiredUnlockTIDs)
            {
                if (!_unlocks.ContainsKey(required))
                {
                    throw new InvalidOperationException($"UnlockTable {tid}의 선행 {required}이(가) 테이블에 없습니다");
                }

                Visit(required);
            }

            visiting.Remove(tid);
            done.Add(tid);
        }
    }

    // 콘텐츠(작업슬롯)가 참조하는 해금을 역색인한다. 같은 해금을 두 칸이 가리키면 1:1 위반이다.
    private void IndexWorkSlots()
    {
        foreach (var slot in _workSlots)
        {
            if (slot.UnlockTID == 0)
            {
                continue;
            }

            if (!_unlocks.ContainsKey(slot.UnlockTID))
            {
                throw new InvalidOperationException(
                    $"WorkSlotTable 칸 {slot.WorkSlotTID}의 UnlockTID {slot.UnlockTID}이(가) UnlockTable에 없습니다");
            }

            if (!_workSlotByUnlock.TryAdd(slot.UnlockTID, slot.WorkSlotTID))
            {
                throw new InvalidOperationException(
                    $"UnlockTID {slot.UnlockTID}을(를) 작업슬롯 칸 {_workSlotByUnlock[slot.UnlockTID]}과 {slot.WorkSlotTID}이 함께 참조합니다 (해금 1:1 위반)");
            }
        }
    }

    // 자원 비용은 TID·수량 두 배열의 짝이다. 짝이 어긋나면 무엇을 몇 개 받는지 알 수 없다.
    private void ValidateItemCosts()
    {
        foreach (var row in _unlocks.Values)
        {
            if (row.ItemTIDs.Length != row.ItemCounts.Length)
            {
                throw new InvalidOperationException(
                    $"UnlockTable {row.UnlockTID}의 ItemTIDs({row.ItemTIDs.Length}개)와 ItemCounts({row.ItemCounts.Length}개)의 개수가 다릅니다");
            }

            if (row.ItemCounts.Any(c => c <= 0))
            {
                throw new InvalidOperationException($"UnlockTable {row.UnlockTID}의 ItemCounts에 0 이하 수량이 있습니다");
            }
        }
    }

    // 건물 노드를 해금 TID로 역색인하고, 노드의 산업을 건물 표에서 정한다.
    private void IndexBuildingNodes(IEnumerable<BuildingTableRow> buildingRows, IEnumerable<BuildingNodeTableRow> nodeRows)
    {
        var industryByBuilding = buildingRows.ToDictionary(b => b.BuildingTID, b => b.Industry);

        foreach (var node in nodeRows)
        {
            if (!_unlocks.ContainsKey(node.UnlockTID))
            {
                throw new InvalidOperationException($"BuildingNodeTable의 UnlockTID {node.UnlockTID}이(가) UnlockTable에 없습니다");
            }

            if (!industryByBuilding.TryGetValue(node.BuildingTID, out var industry))
            {
                throw new InvalidOperationException($"BuildingNodeTable {node.UnlockTID}의 건물 {node.BuildingTID}이(가) BuildingTable에 없습니다");
            }

            if (!_buildingNodes.TryAdd(node.UnlockTID, new BuildingNodeEffect(industry, node.EffectType, node.EffectValue)))
            {
                throw new InvalidOperationException($"UnlockTID {node.UnlockTID}을(를) 건물 노드 두 행이 함께 참조합니다 (해금 1:1 위반)");
            }

            if (_workSlotByUnlock.ContainsKey(node.UnlockTID))
            {
                throw new InvalidOperationException($"UnlockTID {node.UnlockTID}을(를) 작업슬롯과 건물 노드가 함께 참조합니다 (해금 1:1 위반)");
            }
        }
    }

    private void WarnUnreferenced(HashSet<int> otherReferences)
    {
        foreach (var tid in _unlocks.Keys)
        {
            if (!_workSlotByUnlock.ContainsKey(tid) && !_buildingNodes.ContainsKey(tid) && !otherReferences.Contains(tid))
            {
                ServerLog.Warn("해금", $"어느 콘텐츠도 참조하지 않는 UnlockTID {tid} — 오타이거나 죽은 행일 수 있습니다");
            }
        }
    }
}
