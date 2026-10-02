using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 채취 공통 보상(상자 · T-030) 보관소. 자원 롤과 따로, 판정 1회마다 행마다 따로 굴린다.
/// 기본은 <c>CommonRewardTable</c>의 <b>레벨 행</b>(전 산업 공통)이고, <c>CommonRewardOverrideTable</c>에
/// 그 (산업, 레벨) 행이 하나라도 있으면 공통 행을 <b>통째로</b> 대신한다.
/// 확률은 백만분율이라 아주 낮은 값(0.01% = 100)도 정수로 적는다.
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class CommonRewardCatalog : Singleton<CommonRewardCatalog>
{
    public const int ChanceScale = 1_000_000;

    // 두 시트의 행을 굴리는 데 필요한 값만 남긴 공통 형태
    private sealed record Reward(int ItemTID, int Count, int ChancePerMillion);

    private readonly Dictionary<int, List<Reward>> _commonByLevel = new();
    private readonly Dictionary<(IndustryType, int), List<Reward>> _overrideByIndustryLevel = new();

    public int Count => _commonByLevel.Values.Sum(rows => rows.Count) + _overrideByIndustryLevel.Values.Sum(rows => rows.Count);

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.CommonRewardTable.All, GameTable.CommonRewardOverrideTable.All);
    }

    public void Load(IEnumerable<CommonRewardTableRow> commons, IEnumerable<CommonRewardOverrideTableRow> overrides)
    {
        ArgumentNullException.ThrowIfNull(commons);
        ArgumentNullException.ThrowIfNull(overrides);

        _commonByLevel.Clear();
        _overrideByIndustryLevel.Clear();

        foreach (var row in commons)
        {
            Add(_commonByLevel, row.IndustryLevel, new Reward(row.ItemTID, row.Count, row.ChancePerMillion));
        }

        foreach (var row in overrides)
        {
            Add(_overrideByIndustryLevel, (row.IndustryType, row.IndustryLevel), new Reward(row.ItemTID, row.Count, row.ChancePerMillion));
        }
    }

    /// <summary>(<paramref name="industry"/>, <paramref name="industryLevel"/>)로 판정 <paramref name="judgeCount"/>회분을 굴려 아이템별로 모은다. 아무것도 안 나오면 빈 목록.</summary>
    public Dictionary<int, int> Roll(IndustryType industry, int industryLevel, int judgeCount, Random random)
    {
        var gained = new Dictionary<int, int>();
        if (!_overrideByIndustryLevel.TryGetValue((industry, industryLevel), out var rows) &&
            !_commonByLevel.TryGetValue(industryLevel, out rows))
        {
            return gained;
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < judgeCount; i++)
            {
                if (random.Next(ChanceScale) < row.ChancePerMillion)
                {
                    gained[row.ItemTID] = gained.GetValueOrDefault(row.ItemTID) + row.Count;
                }
            }
        }

        return gained;
    }

    private static void Add<TKey>(Dictionary<TKey, List<Reward>> map, TKey key, Reward reward) where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<Reward>();
            map.Add(key, list);
        }

        list.Add(reward);
    }
}
