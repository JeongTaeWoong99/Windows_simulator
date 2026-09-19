using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>UnlockTable</c>·<c>WorkSlotTable</c> 보관소. 해금 TID로 조건 행을, 해금 TID로 작업슬롯 칸을 찾아 준다.
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

    /// <summary>등록된 해금 수.</summary>
    public int Count => _unlocks.Count;

    /// <summary>작업슬롯 칸 전부, 칸 번호 순. 행 수가 곧 슬롯 상한이다.</summary>
    public IReadOnlyList<WorkSlotTableRow> WorkSlots => _workSlots;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        // 작업슬롯 말고도 해금을 참조하는 콘텐츠 — 특성 노드(TID = UnlockTID)와 그 노드가 여는 산업 레벨.
        var otherReferences = GameTable.UserTraitTable.All.Select(r => r.UserTraitTID)
            .Concat(GameTable.IndustryLevelTable.All.Select(r => r.UnlockTID));

        Load(GameTable.UnlockTable.All, GameTable.WorkSlotTable.All, otherReferences);
    }

    /// <summary>행 목록으로 인덱스를 만들고 검증한다. 데이터 오류면 예외 — 조용히 돌면 영영 못 여는 해금이 생긴다.</summary>
    /// <param name="otherReferences">작업슬롯 외 콘텐츠가 참조하는 UnlockTID — 미참조 경고에서 뺀다.</param>
    public void Load(
        IEnumerable<UnlockTableRow>   unlockRows,
        IEnumerable<WorkSlotTableRow> workSlotRows,
        IEnumerable<int>?             otherReferences = null)
    {
        ArgumentNullException.ThrowIfNull(unlockRows);
        ArgumentNullException.ThrowIfNull(workSlotRows);

        _unlocks.Clear();
        _workSlots.Clear();
        _workSlotByUnlock.Clear();

        foreach (var row in unlockRows)
        {
            if (!_unlocks.TryAdd(row.UnlockTID, row))
            {
                throw new InvalidOperationException($"UnlockTable에 UnlockTID가 중복됐습니다: {row.UnlockTID}");
            }
        }

        _workSlots.AddRange(workSlotRows.OrderBy(r => r.WorkSlotTID));

        ValidateRequirements();
        IndexWorkSlots();
        WarnUnreferenced(otherReferences?.ToHashSet() ?? new HashSet<int>());
    }

    public bool TryGetUnlock(int unlockTid, out UnlockTableRow row)
        => _unlocks.TryGetValue(unlockTid, out row!);

    /// <summary>이 해금이 여는 작업슬롯 칸 번호. 슬롯이 아닌 해금(산업 레벨 등)이나 0이면 false.</summary>
    public bool TryGetWorkSlotOf(int unlockTid, out int slotIndex)
        => _workSlotByUnlock.TryGetValue(unlockTid, out slotIndex);

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

    private void WarnUnreferenced(HashSet<int> otherReferences)
    {
        foreach (var tid in _unlocks.Keys)
        {
            if (!_workSlotByUnlock.ContainsKey(tid) && !otherReferences.Contains(tid))
            {
                ServerLog.Warn("해금", $"어느 콘텐츠도 참조하지 않는 UnlockTID {tid} — 오타이거나 죽은 행일 수 있습니다");
            }
        }
    }
}
