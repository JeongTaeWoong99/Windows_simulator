using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>CommonRewardTable</c> 보관소 — 채취 공통 보상(상자 · T-030). 자원 롤과 따로, <b>슬롯 (산업, 레벨)의 행만</b> 판정 1회마다 행마다 따로 굴린다.
/// 산업마다 확률을 다르게 둘 수 있다.
/// 확률은 백만분율이라 아주 낮은 값(0.01% = 100)도 정수로 적는다.
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class CommonRewardCatalog : Singleton<CommonRewardCatalog>
{
    public const int ChanceScale = 1_000_000;

    private readonly Dictionary<(IndustryType, int), List<CommonRewardTableRow>> _byIndustryLevel = new();

    public int Count => _byIndustryLevel.Values.Sum(rows => rows.Count);

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.CommonRewardTable.All);
    }

    public void Load(IEnumerable<CommonRewardTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byIndustryLevel.Clear();
        foreach (var row in rows)
        {
            var key = (row.IndustryType, row.IndustryLevel);
            if (!_byIndustryLevel.TryGetValue(key, out var list))
            {
                list = new List<CommonRewardTableRow>();
                _byIndustryLevel.Add(key, list);
            }

            list.Add(row);
        }
    }

    /// <summary>(<paramref name="industry"/>, <paramref name="industryLevel"/>)의 행으로 판정 <paramref name="judgeCount"/>회분을 굴려 아이템별로 모은다. 아무것도 안 나오면 빈 목록.</summary>
    public Dictionary<int, int> Roll(IndustryType industry, int industryLevel, int judgeCount, Random random)
    {
        var gained = new Dictionary<int, int>();
        if (!_byIndustryLevel.TryGetValue((industry, industryLevel), out var rows))
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
}
