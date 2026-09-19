using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>UserTraitTable</c> 보관소 — 계정 단위 특성 트리. <b>노드 하나 = 해금 하나</b>이고 노드 TID가 곧 <c>UnlockTID</c>다.
/// 조건(계정 레벨·선행)은 <c>UnlockTable</c>이, 비용(특성 포인트)과 효과는 여기가 갖는다.
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class UserTraitCatalog : Singleton<UserTraitCatalog>
{
    private readonly Dictionary<int, UserTraitTableRow> _byTid = new();
    private readonly List<UserTraitTableRow> _speedAdds = new();

    public int Count => _byTid.Count;

    /// <summary>속도 가산 노드 전부. 슬롯 속도를 낼 때 열린 것만 더한다.</summary>
    public IReadOnlyList<UserTraitTableRow> SpeedAdds => _speedAdds;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.UserTraitTable.All);
    }

    /// <summary>행 목록으로 인덱스를 만든다. 같은 TID가 두 번 나오면 예외.</summary>
    public void Load(IEnumerable<UserTraitTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byTid.Clear();
        _speedAdds.Clear();

        foreach (var row in rows)
        {
            if (!_byTid.TryAdd(row.UserTraitTID, row))
            {
                throw new InvalidOperationException($"UserTraitTable에 UserTraitTID가 중복됐습니다: {row.UserTraitTID}");
            }

            if (row.EffectType == UserTraitEffect.SpeedAdd)
            {
                _speedAdds.Add(row);
            }
        }
    }

    public bool TryGet(int userTraitTid, out UserTraitTableRow row)
        => _byTid.TryGetValue(userTraitTid, out row!);

    /// <summary>이 해금이 특성 노드인가. 그렇다면 <c>C_UnlockRequest</c>로는 열 수 없다 — 포인트를 건너뛰는 뒷문이 된다.</summary>
    public bool IsTraitUnlock(int unlockTid) => _byTid.ContainsKey(unlockTid);
}
