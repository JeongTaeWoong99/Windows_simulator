using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>AccountLevelTable</c> 보관소 — 계정 레벨 곡선과 레벨별 특성 포인트. <b>마지막 행이 곧 만렙이다</b>.
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class AccountLevelCatalog : Singleton<AccountLevelCatalog>
{
    private readonly Dictionary<int, AccountLevelTableRow> _byLevel = new();

    /// <summary>등록된 최고 레벨. 행이 없으면 1 — 그 레벨에서는 경험치가 쌓이지 않는다.</summary>
    public int MaxLevel { get; private set; } = 1;

    public int Count => _byLevel.Count;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.AccountLevelTable.All);
    }

    /// <summary>행 목록으로 곡선을 만든다. 같은 레벨이 두 번 나오면 예외.</summary>
    public void Load(IEnumerable<AccountLevelTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byLevel.Clear();
        MaxLevel = 1;

        foreach (var row in rows)
        {
            if (!_byLevel.TryAdd(row.AccountLevelTID, row))
            {
                throw new InvalidOperationException($"AccountLevelTable에 레벨이 중복됐습니다: Lv{row.AccountLevelTID}");
            }

            MaxLevel = Math.Max(MaxLevel, row.AccountLevelTID);
        }
    }

    /// <summary>이 레벨에 <b>도달하는 데</b> 직전 레벨에서 필요한 경험치. 행이 없으면 false.</summary>
    public bool TryGetRequiredExp(int level, out long requiredExp)
    {
        if (_byLevel.TryGetValue(level, out var row))
        {
            requiredExp = row.RequiredExp;
            return true;
        }

        requiredExp = 0;
        return false;
    }

    /// <summary>이 레벨에 도달하면 주는 특성 포인트. 행이 없으면 0.</summary>
    public int TraitPointAt(int level)
        => _byLevel.TryGetValue(level, out var row) ? row.TraitPoint : 0;
}
