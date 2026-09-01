using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 가챠 풀 항목 하나. 엑셀 <c>GachaItemTable</c>·<c>GachaCharacterTable</c>의 Row를
/// <b>추첨에 필요한 값만으로 정규화</b>한 것이다.
///
/// <para>
/// <see cref="RewardType"/>이 <see cref="RewardTID"/>의 의미를 정한다 —
/// 아이템이면 <c>ItemTable.ItemTID</c>, 캐릭터면 <c>CharacterTable.CharacterTID</c>다.
/// 두 시트가 갈라져 있으므로 <b>종류는 값이 아니라 출처가 정한다.</b>
/// </para>
///
/// <para>
/// 등급은 여기 두지 않는다 — <b>등급의 원본은 각 정의 테이블 하나다.</b>
/// 풀에 등급을 따로 적으면 정의 쪽과 독립적으로 존재하다가 조용히 어긋난다(깃허브 이슈 #9 2-3).
/// </para>
/// </summary>
public readonly record struct GachaEntry(
    int              GachaId,
    GachaRewardType  RewardType,
    int              RewardTID,
    int              Count,
    int              Weight);

/// <summary>
/// 가챠 풀 보관소. <b>GachaId별로</b> 추첨기를 찾아 준다.
///
/// <para>
/// 풀 데이터의 원본은 엑셀 <c>Gacha.xlsx</c>의 두 시트다 — <c>GachaItemTable</c>(아이템)과
/// <c>GachaCharacterTable</c>(캐릭터). 각 시트가 자기 TID 컬럼에 <c>Ref</c>를 걸고 있어,
/// 실재하지 않는 아이템·캐릭터를 주는 풀은 <c>generate-tables.ps1</c> 시점에 막힌다.
/// <b>같은 GachaId를 두 시트가 함께 쓰면 한 풀 안에서 아이템과 캐릭터가 섞여 나온다.</b>
/// </para>
///
/// <para>
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고,
/// 이후에는 조회만 한다. 로드가 끝나면 불변이라 여러 스레드가 동시에 읽어도 안전하다.
/// </para>
/// </summary>
public sealed class GachaPoolCatalog : Singleton<GachaPoolCatalog>
{
    private Dictionary<int, WeightedPicker<GachaEntry>> _byPool = new();

    /// <summary>등록된 풀 수.</summary>
    public int Count => _byPool.Count;

    /// <summary>엑셀의 아이템·캐릭터 시트 전 행을 풀별 추첨기로 만든다.</summary>
    /// <remarks>반드시 <c>GameTable.LoadAll</c> 이후에 부른다. 테이블 데이터가 없으면 여기서 터진다.</remarks>
    public void LoadAll()
    {
        var items = ToEntries(GameTable.GachaItemTable.All, GachaRewardType.Item,
                              r => r.GachaId, r => r.ItemTID, r => r.Count, r => r.Weight);

        var characters = ToEntries(GameTable.GachaCharacterTable.All, GachaRewardType.Character,
                                   r => r.GachaId, r => r.CharacterTID, r => r.Count, r => r.Weight);

        Load(items.Concat(characters));

        ServerLog.Info("데이터", $"가챠 풀 {Count}개 등록 완료");
    }

    /// <summary>Row 목록을 한 종류의 보상 항목으로 정규화한다.</summary>
    public static IEnumerable<GachaEntry> ToEntries<TRow>(
        IEnumerable<TRow> rows,
        GachaRewardType   rewardType,
        Func<TRow, int>   gachaIdSelector,
        Func<TRow, int>   rewardTidSelector,
        Func<TRow, int>   countSelector,
        Func<TRow, int>   weightSelector)
    {
        return rows.Select(r => new GachaEntry(
            gachaIdSelector(r), rewardType, rewardTidSelector(r), countSelector(r), weightSelector(r)));
    }

    /// <summary>정규화된 항목으로 풀별 추첨기를 만든다. 기존 등록은 전부 교체된다.</summary>
    public void Load(IEnumerable<GachaEntry> entries)
    {
        _byPool = WeightedPicker.GroupBy(entries, e => e.GachaId, e => e.Weight);
    }

    public bool TryGet(int gachaId, out WeightedPicker<GachaEntry> pool)
        => _byPool.TryGetValue(gachaId, out pool!);
}
