using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 특성 보관소 — 계정 단위 <b>레벨형</b> 특성(T-108). 특성 하나(<c>UserTraitTable</c>)가 기본 레벨에서 최대 레벨까지 오르고,
/// 레벨마다 계정 레벨 조건(<c>UserTraitLevelTable</c>)이 붙는다. 비용과 효과는 레벨마다 같다(특성 행의 <c>TraitPoint</c>·<c>EffectValue</c>).
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class UserTraitCatalog : Singleton<UserTraitCatalog>
{
    private readonly Dictionary<int, UserTraitTableRow> _byTid = new();
    private readonly Dictionary<(int Tid, int Level), UserTraitLevelTableRow> _levels = new();
    private readonly Dictionary<IndustryType, UserTraitTableRow> _industryUnlocks = new();
    private readonly List<UserTraitTableRow> _speedAdds = new();
    private readonly List<UserTraitTableRow> _yieldAdds = new();

    public int Count => _byTid.Count;

    /// <summary>속도 가산 특성 전부. 슬롯 속도를 낼 때 레벨만큼 더한다.</summary>
    public IReadOnlyList<UserTraitTableRow> SpeedAdds => _speedAdds;

    /// <summary>산출량 가산 특성 전부. 판정의 자원 개수를 낼 때 레벨만큼 더한다.</summary>
    public IReadOnlyList<UserTraitTableRow> YieldAdds => _yieldAdds;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.UserTraitTable.All, GameTable.UserTraitLevelTable.All);
    }

    /// <summary>인덱스를 만들고 검증한다. 데이터 오류면 예외 — 조용히 돌면 오를 수 없는 레벨이 생긴다.</summary>
    public void Load(IEnumerable<UserTraitTableRow> traits, IEnumerable<UserTraitLevelTableRow> levels)
    {
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(levels);

        _byTid.Clear();
        _levels.Clear();
        _industryUnlocks.Clear();
        _speedAdds.Clear();
        _yieldAdds.Clear();

        foreach (var row in traits)
        {
            if (!_byTid.TryAdd(row.UserTraitTID, row))
            {
                throw new InvalidOperationException($"UserTraitTable에 UserTraitTID가 중복됐습니다: {row.UserTraitTID}");
            }

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

        foreach (var row in levels)
        {
            if (!_byTid.ContainsKey(row.UserTraitTID))
            {
                throw new InvalidOperationException($"UserTraitLevelTable {row.UserTraitLevelTID}이(가) 없는 특성 {row.UserTraitTID}을(를) 가리킵니다");
            }

            if (!_levels.TryAdd((row.UserTraitTID, row.Level), row))
            {
                throw new InvalidOperationException($"특성 {row.UserTraitTID}의 Lv{row.Level} 행이 둘입니다");
            }
        }

        // 기본 레벨 다음부터 최대 레벨까지 행이 빠짐없이 있어야 한다.
        foreach (var trait in _byTid.Values)
        {
            for (var level = trait.BaseLevel + 1; level <= trait.MaxLevel; level++)
            {
                if (!_levels.ContainsKey((trait.UserTraitTID, level)))
                {
                    throw new InvalidOperationException($"특성 {trait.UserTraitTID}의 Lv{level} 행이 없습니다");
                }
            }
        }
    }

    public bool TryGet(int userTraitTid, out UserTraitTableRow row)
        => _byTid.TryGetValue(userTraitTid, out row!);

    public bool TryGetLevel(int userTraitTid, int level, out UserTraitLevelTableRow row)
        => _levels.TryGetValue((userTraitTid, level), out row!);

    /// <summary>이 산업의 개척 특성. 특성 레벨이 곧 열린 산업 레벨이다.</summary>
    public bool TryGetIndustryUnlock(IndustryType industry, out UserTraitTableRow row)
        => _industryUnlocks.TryGetValue(industry, out row!);
}
