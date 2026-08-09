using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 드롭 테이블 보관소. <b>(산업, 산업 레벨)별로</b> 테이블을 찾아 준다.
///
/// <para>
/// 드롭 시트는 산업마다 하나지만 행이 레벨별로 갈라져 있다(<c>IndustryLevel</c> 컬럼 — 산업레벨.md 6.2).
/// 시트 전체를 한 테이블로 등록하면 모든 레벨의 아이템이 한 통에 섞여 나오므로,
/// 로드 시 레벨로 갈라 <b>레벨마다 독립 테이블</b>로 등록한다.
/// </para>
///
/// <para>
/// 호출부가 각자 <see cref="DropTable.From{TRow}"/>를 부르고 캐시를 따로 들고 있으면
/// 캐시 위치가 흩어지고 "판정마다 새로 만드는" 실수가 섞여 든다.
/// 그래서 <b>만드는 곳을 <see cref="LoadAll"/> 한 군데로 모은다</b> — 시트를 추가하면 여기에 한 줄을 더한다.
/// </para>
///
/// <para>
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고,
/// 이후에는 조회만 한다. 등록이 끝나면 사실상 불변이라 여러 스레드가 동시에 읽어도 안전하다.
/// </para>
/// </summary>
/// <remarks>
/// <b>희귀도 가중치는 각 산업 시트가 직접 갖는다.</b> 공통 <c>RarityWeightTable</c>을 두지 않으므로
/// 분포를 산업마다 다르게 잡을 수 있다 — 대신 확률을 손볼 때 <b>5개 시트를 함께</b> 봐야 한다.
/// <para>
/// 산업 축에 <see cref="IndustryType"/>을 그대로 쓴다. 기획이 "산업 = IndustryType"으로 잡고 있어서인데,
/// <b>이 enum은 아이템 분류(<c>Misc</c>·<c>Special</c>)도 겸하고 있어 의미가 둘로 갈려 있다.</b>
/// 갈라지기 시작하면 <c>IndustryType</c>을 분리한다(→ GameDesign 기획평가.md).
/// 그때 고칠 곳이 여기로 모이도록 키를 이 클래스 안에 가둬 뒀다.
/// </para>
/// </remarks>
public sealed class DropTableCatalog : Singleton<DropTableCatalog>
{
    private readonly Dictionary<(IndustryType Industry, int Level), DropTable> _byIndustryLevel = new();

    /// <summary>등록된 테이블 수. (산업, 레벨) 조합 하나가 테이블 하나다.</summary>
    public int Count => _byIndustryLevel.Count;

    /// <summary>
    /// 모든 드롭 테이블을 <c>GameTable</c>에서 읽어 등록한다.
    /// <b>드롭 시트를 추가하면 여기에 한 줄을 더한다.</b>
    /// </summary>
    /// <remarks>반드시 <c>GameTable.LoadAll</c> 이후에 부른다. 테이블 데이터가 없으면 여기서 터진다.</remarks>
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

        // 1차 산업 5종이 모두 등록됐다. 산업이 늘면 위와 같은 형태로 한 줄씩 추가한다.

        ServerLog.Info("데이터", $"드롭 테이블 {Count}개 등록 완료 (산업 5종 × 레벨별)");
    }

    /// <summary>
    /// 시트 행 목록을 <b>레벨로 갈라</b> 레벨마다 독립 테이블로 등록한다.
    /// 테이블 이름은 엑셀의 레벨별 시트명과 같은 꼴(<c>이름.Lv레벨</c>)로 남긴다.
    /// </summary>
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
