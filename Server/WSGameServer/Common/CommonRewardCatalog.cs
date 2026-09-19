using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>CommonRewardTable</c> 보관소 — 채취 공통 보상(T-030). 산업·레벨과 무관하게 <b>판정 1회마다 행마다 따로</b> 굴린다.
/// 확률은 백만분율이라 아주 낮은 값(0.01% = 100)도 정수로 적는다.
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class CommonRewardCatalog : Singleton<CommonRewardCatalog>
{
    public const int ChanceScale = 1_000_000;

    private readonly List<CommonRewardTableRow> _rows = new();

    public int Count => _rows.Count;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다. 반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.CommonRewardTable.All);
    }

    public void Load(IEnumerable<CommonRewardTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _rows.Clear();
        _rows.AddRange(rows);
    }

    /// <summary>판정 <paramref name="judgeCount"/>회분을 굴려 나온 것을 아이템별로 모은다. 아무것도 안 나오면 빈 목록.</summary>
    public Dictionary<int, int> Roll(int judgeCount, Random random)
    {
        var gained = new Dictionary<int, int>();

        foreach (var row in _rows)
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
