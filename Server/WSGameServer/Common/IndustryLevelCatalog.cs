using GameData;
using MikaUtils;

namespace WSGameServer;

// IndustryLevelTable 보관소. 시트 키는 IndustryLevelTID(산업×100+레벨)지만 서버는 (산업, 레벨)로 묻는다.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md 2장
public sealed class IndustryLevelCatalog : Singleton<IndustryLevelCatalog>
{
    private readonly Dictionary<(IndustryType Industry, int Level), IndustryLevelTableRow> _byIndustryLevel = new();

    /// <summary>등록된 행 수. 산업 5 × 레벨 5 = 25가 정상이다.</summary>
    public int Count => _byIndustryLevel.Count;

    /// <summary>모든 행을 GameTable에서 읽어 등록한다. GameTable.LoadAll 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.IndustryLevelTable.All);
    }

    /// <summary>행 목록으로 인덱스를 만든다. 같은 (산업, 레벨)이 두 번 나오면 예외.</summary>
    public void Load(IEnumerable<IndustryLevelTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byIndustryLevel.Clear();

        foreach (var row in rows)
        {
            // 중복을 허용하면 어느 행의 필요 점수가 이기는지가 로드 순서에 달린다. 데이터 오류로 본다.
            if (!_byIndustryLevel.TryAdd((row.IndustryType, row.Level), row))
            {
                throw new InvalidOperationException(
                    $"IndustryLevelTable에 (산업, 레벨)이 중복됐습니다: {row.IndustryType} Lv{row.Level}");
            }
        }
    }

    /// <summary>조회한다. 없으면 예외 — 조용히 기본값을 주면 잘못된 레벨이 30초로 돌아 아무도 모른다.</summary>
    public IndustryLevelTableRow Get(IndustryType industry, int level)
    {
        if (!_byIndustryLevel.TryGetValue((industry, level), out var row))
        {
            throw new KeyNotFoundException(
                $"[{industry} Lv{level}] IndustryLevelTable 행이 없습니다. IndustryLevelCatalog.LoadAll을 확인하세요.");
        }

        return row;
    }

    public bool TryGet(IndustryType industry, int level, out IndustryLevelTableRow row)
        => _byIndustryLevel.TryGetValue((industry, level), out row!);

    /// <summary>판정 1회 비용(밀리초×천분율). ×1000은 단위 환산이며 반드시 서버 몫 — 엑셀에 넣으면 Lv5가 int를 넘긴다.</summary>
    public long GetJudgeCostUnits(IndustryType industry, int level)
        => Get(industry, level).RequiredScore * 1000L;
}
