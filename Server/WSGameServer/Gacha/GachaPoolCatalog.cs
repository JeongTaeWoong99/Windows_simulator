using GameData;
using MikaUtils;

namespace WSGameServer;

// 가챠 풀 항목 하나. RewardType이 RewardTID의 의미를 정한다(종류는 값이 아니라 출처가 정한다).
// 등급은 여기 두지 않는다 — 원본은 각 정의 테이블 하나 → Server/docs/데이터-카탈로그.md 3장
public readonly record struct GachaEntry(
    int              GachaId,
    GachaRewardType  RewardType,
    int              RewardTID,
    int              Count,
    int              Weight);

// 가챠 풀 보관소. GachaId별 추첨기. 원본은 Gacha.xlsx의 아이템·캐릭터·장비 세 시트이며 같은 GachaId면 한 풀에 섞인다.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md
public sealed class GachaPoolCatalog : Singleton<GachaPoolCatalog>
{
    private Dictionary<int, WeightedPicker<GachaEntry>> _byPool = new();

    /// <summary>등록된 풀 수.</summary>
    public int Count => _byPool.Count;

    /// <summary>엑셀의 아이템·캐릭터·장비 시트 전 행을 풀별 추첨기로 만든다. GameTable.LoadAll 이후에 부른다.</summary>
    public void LoadAll()
    {
        var items = ToEntries(GameTable.GachaItemTable.All, GachaRewardType.Item,
                              r => r.GachaId, r => r.ItemTID, r => r.Count, r => r.Weight);

        var characters = ToEntries(GameTable.GachaCharacterTable.All, GachaRewardType.Character,
                                   r => r.GachaId, r => r.CharacterTID, r => r.Count, r => r.Weight);

        var equips = ToEntries(GameTable.GachaEquipTable.All, GachaRewardType.Equip,
                               r => r.GachaId, r => r.EquipTID, r => r.Count, r => r.Weight);

        Load(items.Concat(characters).Concat(equips));

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
