using System.Text;
using GameData;
using MikaUtils;

namespace WSGameServer;

// 드롭 테이블 보관소. 시트는 산업마다 하나지만 로드 시 레벨로 갈라 (산업, 레벨)마다 독립 테이블로 둔다.
// 만드는 곳은 LoadAll 한 군데 — 시트를 추가하면 여기에 한 줄 → Server/docs/데이터-카탈로그.md 1장
public sealed class DropTableCatalog : Singleton<DropTableCatalog>
{
    private readonly Dictionary<(IndustryType Industry, int Level), DropTable> _byIndustryLevel = new();

    /// <summary>등록된 테이블 수. (산업, 레벨) 조합 하나가 테이블 하나다.</summary>
    public int Count => _byIndustryLevel.Count;

    /// <summary>모든 드롭 테이블을 GameTable에서 읽어 등록한다. GameTable.LoadAll 이후에 부른다. 시트를 추가하면 여기에 한 줄.</summary>
    public void LoadAll()
    {
        _byIndustryLevel.Clear();

        Register(IndustryType.Fishing, nameof(GameTable.FishingBasicTable),
                 GameTable.FishingBasicTable.All, r => r.IndustryLevel, r => r.ItemTID, r => r.Weight);

        Register(IndustryType.Farming, nameof(GameTable.FarmingBasicTable),
                 GameTable.FarmingBasicTable.All, r => r.IndustryLevel, r => r.ItemTID, r => r.Weight);

        Register(IndustryType.Logging, nameof(GameTable.LoggingBasicTable),
                 GameTable.LoggingBasicTable.All, r => r.IndustryLevel, r => r.ItemTID, r => r.Weight);

        Register(IndustryType.Mining, nameof(GameTable.MiningBasicTable),
                 GameTable.MiningBasicTable.All, r => r.IndustryLevel, r => r.ItemTID, r => r.Weight);

        Register(IndustryType.Hunting, nameof(GameTable.HuntingBasicTable),
                 GameTable.HuntingBasicTable.All, r => r.IndustryLevel, r => r.ItemTID, r => r.Weight);

        ServerLog.Info("데이터", $"드롭 테이블 {Count}개 등록 완료 (산업 5종 × 레벨별)");
        LogDistributions();
    }

    /// <summary>산업×레벨별 가중치 분포를 로그로 찍는다(R18 진단). 5개 시트의 분포가 어긋나면 시작 로그에서 잡힌다.</summary>
    private void LogDistributions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("드롭 분포 요약 (R18 진단)");

        foreach (var (industry, level, table) in All.OrderBy(x => x.Industry).ThenBy(x => x.Level))
        {
            sb.Append($"  {industry} Lv{level}: ");
            foreach (var entry in table.Entries)
            {
                var pct = table.TotalWeight > 0
                    ? entry.Weight * 100.0 / table.TotalWeight
                    : 0;
                sb.Append($"TID {entry.ItemTID}={entry.Weight}({pct:F1}%)  ");
            }
            sb.AppendLine();
        }

        ServerLog.Debug("데이터", sb.ToString());
    }

    /// <summary>시트 행을 레벨로 갈라 레벨마다 독립 테이블로 등록한다. 이름은 엑셀 시트명 꼴(이름.Lv레벨).</summary>
    public void Register<TRow>(
        IndustryType          industry,
        string            name,
        IEnumerable<TRow> rows,
        Func<TRow, int>   levelSelector,
        Func<TRow, int>   itemTidSelector,
        Func<TRow, int>   weightSelector)
    {
        foreach (var level in rows.GroupBy(levelSelector))
        {
            Register(industry, level.Key,
                     DropTable.From($"{name}.Lv{level.Key}", level, itemTidSelector, weightSelector));
        }
    }

    /// <summary>이미 만들어진 테이블을 등록한다. 같은 (산업, 레벨)을 두 번 넣으면 예외.</summary>
    public void Register(IndustryType industry, int level, DropTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        // 덮어쓰기를 허용하면 등록 순서에 따라 확률이 조용히 바뀐다. 중복은 설정 실수로 본다.
        if (!_byIndustryLevel.TryAdd((industry, level), table))
        {
            throw new InvalidOperationException($"드롭 테이블이 중복 등록됐습니다: {industry} Lv{level}");
        }
    }

    /// <summary>조회한다. 없으면 예외 — 드롭이 조용히 비는 것보다 즉시 드러나는 편이 낫다.</summary>
    public DropTable Get(IndustryType industry, int level)
    {
        if (!_byIndustryLevel.TryGetValue((industry, level), out var table))
        {
            throw new KeyNotFoundException(
                $"[{industry} Lv{level}] 드롭 테이블이 없습니다. DropTableCatalog.LoadAll에 등록됐는지 확인하세요.");
        }

        return table;
    }

    public bool TryGet(IndustryType industry, int level, out DropTable table)
        => _byIndustryLevel.TryGetValue((industry, level), out table!);

    /// <summary>등록된 테이블 목록. 진단·검증용.</summary>
    public IEnumerable<(IndustryType Industry, int Level, DropTable Table)> All
        => _byIndustryLevel.Select(kv => (kv.Key.Industry, kv.Key.Level, kv.Value));
}
