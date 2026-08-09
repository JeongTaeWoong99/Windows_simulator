using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>IndustryLevelTable</c> 보관소. <b>(산업, 산업 레벨)로</b> 행을 찾아 준다.
///
/// <para>
/// 시트의 키는 단일 컬럼 제약 때문에 <c>IndustryLevelTID</c>(산업×100+레벨)지만,
/// 서버가 실제로 묻는 것은 언제나 <b>(산업, 레벨)</b>이다. 채번식 산수를 호출부마다
/// 반복하지 않도록 로드 시 한 번 인덱스를 만들어 둔다 (산업레벨.md 6.1).
/// </para>
///
/// <para>
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고,
/// 이후에는 조회만 한다. 등록이 끝나면 사실상 불변이라 여러 스레드가 동시에 읽어도 안전하다.
/// 판정 비용 외에 해금 요구치(<c>RequiredAptitude</c>·<c>RequiredAccountLevel</c>) 조회도
/// 구현되면 여기로 모은다.
/// </para>
/// </summary>
public sealed class IndustryLevelCatalog : Singleton<IndustryLevelCatalog>
{
    private readonly Dictionary<(IndustryType Industry, int Level), IndustryLevelTableRow> _byIndustryLevel = new();

    /// <summary>등록된 행 수. 산업 5 × 레벨 5 = 25가 정상이다.</summary>
    public int Count => _byIndustryLevel.Count;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다.</summary>
    /// <remarks>반드시 <c>GameTable.LoadAll</c> 이후에 부른다. 테이블 데이터가 없으면 여기서 터진다.</remarks>
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

    /// <summary>
    /// 판정 1회 비용(밀리초×천분율). 엑셀 <c>RequiredScore</c>는 초×천분율이라 <b>×1000은 단위 환산</b>이다.
    /// 엑셀에 밀리초 값을 직접 넣으면 Lv5(24억)가 int를 넘겨 깨진다 — 환산은 반드시 서버 몫이다 (산업레벨.md 2.4).
    /// </summary>
    public long GetJudgeCostUnits(IndustryType industry, int level)
        => Get(industry, level).RequiredScore * 1000L;
}
