using GameData;
using MikaUtils;

namespace WSGameServer.Common;

/// <summary>
/// 드롭 테이블 보관소. <b>산업별로</b> 테이블을 찾아 준다.
///
/// <para>
/// 드롭 테이블은 산업이 늘면 함께 늘어난다(농사·벌목·낚시·채굴·사냥).
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
/// <b>지금은 산업당 테이블 1개다.</b> 기획(자원채취 2.4)은 기본 90% : 특별 10% + 공통 보상의
/// 3층 구조를 그리고 있지만, 확률값과 희귀도 분포가 미확정이라 시트가 아직 하나뿐이다.
/// 층이 생기면 키에 축을 하나 더하고 <c>Get(industry)</c>은 기본 층을 가리키게 두면 된다.
/// <para>
/// 산업 축에 <see cref="ItemType"/>을 그대로 쓴다. 기획이 "산업 = ItemType"으로 잡고 있어서인데,
/// <b>이 enum은 아이템 분류(<c>Misc</c>·<c>Special</c>)도 겸하고 있어 의미가 둘로 갈려 있다.</b>
/// 갈라지기 시작하면 <c>IndustryType</c>을 분리한다(→ GameDesign 기획평가.md).
/// 그때 고칠 곳이 여기로 모이도록 키를 이 클래스 안에 가둬 뒀다.
/// </para>
/// </remarks>
public sealed class DropTableCatalog : Singleton<DropTableCatalog>
{
    private readonly Dictionary<ItemType, DropTable> _byIndustry = new();

    /// <summary>등록된 테이블 수.</summary>
    public int Count => _byIndustry.Count;

    /// <summary>
    /// 모든 드롭 테이블을 <c>GameTable</c>에서 읽어 등록한다.
    /// <b>드롭 시트를 추가하면 여기에 한 줄을 더한다.</b>
    /// </summary>
    /// <remarks>반드시 <c>GameTable.LoadAll</c> 이후에 부른다. 테이블 데이터가 없으면 여기서 터진다.</remarks>
    public void LoadAll()
    {
        _byIndustry.Clear();

        Register(ItemType.Fishing, nameof(GameTable.FishingBasicTable),
                 GameTable.FishingBasicTable.All, r => r.ItemTID, r => r.Weight);

        // 농사·벌목·채굴·사냥은 아직 시트가 없다. 만들어지는 대로 위와 같은 형태로 한 줄씩 추가한다.

        ServerLog.Info("데이터", $"드롭 테이블 {Count}개 등록 완료");
    }

    /// <summary>테이블 Row 목록으로 만들어 등록한다.</summary>
    public void Register<TRow>(
        ItemType          industry,
        string            name,
        IEnumerable<TRow> rows,
        Func<TRow, int>   itemTidSelector,
        Func<TRow, int>   weightSelector)
        => Register(industry, DropTable.From(name, rows, itemTidSelector, weightSelector));

    /// <summary>이미 만들어진 테이블을 등록한다. 같은 산업을 두 번 넣으면 예외.</summary>
    public void Register(ItemType industry, DropTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        // 덮어쓰기를 허용하면 등록 순서에 따라 확률이 조용히 바뀐다. 중복은 설정 실수로 본다.
        if (!_byIndustry.TryAdd(industry, table))
            throw new InvalidOperationException($"드롭 테이블이 중복 등록됐습니다: {industry}");
    }

    /// <summary>조회한다. 없으면 예외 — 드롭이 조용히 비는 것보다 즉시 드러나는 편이 낫다.</summary>
    public DropTable Get(ItemType industry)
        => _byIndustry.TryGetValue(industry, out var table)
            ? table
            : throw new KeyNotFoundException(
                $"[{industry}] 드롭 테이블이 없습니다. DropTableCatalog.LoadAll에 등록됐는지 확인하세요.");

    public bool TryGet(ItemType industry, out DropTable table)
        => _byIndustry.TryGetValue(industry, out table!);

    /// <summary>등록된 테이블 목록. 진단·검증용.</summary>
    public IEnumerable<(ItemType Industry, DropTable Table)> All
        => _byIndustry.Select(kv => (kv.Key, kv.Value));
}
