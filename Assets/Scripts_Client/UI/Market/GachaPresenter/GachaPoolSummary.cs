using System.Collections.Generic;
using GameData;
using UnityEngine;

// 뽑기 풀 하나의 요약 — 카드 제목 괄호 · 나오는 종수 · 대표 아이콘 · 등급 확률 툴팁 ('GachaPresenter'가 카드에 넘긴다).
//
// ■ 풀은 네 시트를 GachaId로 섞은 것이다
// 서버 'GachaPoolCatalog'가 Gacha.xlsx의 아이템·캐릭터·장비·골드 시트에서 같은 GachaId 행을 한 추첨기에 넣는다.
// 확률도 같은 방식으로 센다 — 네 시트의 가중치 합이 분모다. 한 시트만 보면 섞인 풀의 확률이 틀린다.
//
// ■ 확률은 테이블에서 계산한다
// 수치를 문구에 적어 두면 엑셀이 바뀔 때 거짓이 된다('EquipLabel.BuildCubeRuleTooltip'과 같은 이유).
// 등급은 각 정의 테이블(아이템·캐릭터·장비)에서 읽는다 — 가챠 시트에는 등급이 없다. 'Description'은 읽지 않는다.
public sealed class GachaPoolSummary
{
    // 등급별 가중치 합 — 'GlobalRarity' 값이 번호다(0 = 등급 없음).
    private readonly long[] _rarityWeights = new long[(int)GlobalRarity.Mythic + 1];

    private long _goldWeight;
    private long _totalWeight;

    private int _characterKinds;
    private int _equipKinds;
    private int _itemKinds;

    // 장비 종류가 하나뿐이면 그 종류 — 종수 문구를 '무기 31종'처럼 쓴다. 섞였으면 null.
    private EquipKind? _equipKind;

    // 대표 아이콘 — 가장 높은 등급의 첫 항목.
    private ItemIconContent _icon = ItemIconContent.ForGold();
    private GlobalRarity     _iconRarity = GlobalRarity.None;

    public string Name { get; }

    private GachaPoolSummary(string name)
    {
        Name = name;
    }

    // 풀 하나를 테이블에서 읽어 요약한다 (GachaPresenter.Start에서 한 번).
    public static GachaPoolSummary Build(int gachaId, string name)
    {
        var summary = new GachaPoolSummary(name);

        foreach (GachaItemTableRow row in GameTable.GachaItemTable.All)
        {
            if (row.GachaId == gachaId)
            {
                summary._itemKinds++;
                summary.Add(GameDataLoader.GetItemRarity(row.ItemTID), row.Weight, () => ItemIconContent.ForItem(row.ItemTID, 1));
            }
        }

        foreach (GachaCharacterTableRow row in GameTable.GachaCharacterTable.All)
        {
            if (row.GachaId == gachaId)
            {
                summary._characterKinds++;
                summary.Add(GameDataLoader.GetCharacterRarity(row.CharacterTID), row.Weight, () => ItemIconContent.ForCharacter(row.CharacterTID));
            }
        }

        foreach (GachaEquipTableRow row in GameTable.GachaEquipTable.All)
        {
            if (row.GachaId != gachaId)
            {
                continue;
            }

            summary._equipKinds++;
            summary.TrackEquipKind(row.EquipTID);
            summary.Add(GameDataLoader.GetEquipRarity(row.EquipTID), row.Weight, () => WithoutSockets(ItemIconContent.ForEquip(row.EquipTID, null)));
        }

        foreach (GachaGoldTableRow row in GameTable.GachaGoldTable.All)
        {
            if (row.GachaId == gachaId)
            {
                summary._goldWeight  += row.Weight;
                summary._totalWeight += row.Weight;
            }
        }

        return summary;
    }

    #region 카드 문구

    // 제목 괄호 — 그 풀이 올려 주는 것. 장비는 전부 작업속도를 올리고, 캐릭터는 일꾼이다. 섞였거나 아이템뿐이면 빈 문자열.
    public string Subtitle
    {
        get
        {
            if (IsOnly(_characterKinds))
            {
                return "일꾼";
            }

            return IsOnly(_equipKinds) ? "작업속도 증가" : "";
        }
    }

    // 나오는 종수 — '캐릭터 30종' · '무기 31종'. 섞인 풀은 '12종'.
    public string KindText
    {
        get
        {
            int total = _characterKinds + _equipKinds + _itemKinds;

            if (IsOnly(_characterKinds))
            {
                return $"캐릭터 {total}종";
            }

            if (IsOnly(_equipKinds))
            {
                return $"{(_equipKind.HasValue ? EquipLabel.GetKindName(_equipKind.Value) : "장비")} {total}종";
            }

            return IsOnly(_itemKinds) ? $"아이템 {total}종" : $"{total}종";
        }
    }

    // 카드 왼쪽 아이콘 — 가장 높은 등급의 첫 항목.
    public ItemIconContent Icon => _icon;

    #endregion

    #region 확률 툴팁

    // 등급 확률 툴팁 — [P]에 올리면 뜬다 (카드의 'TooltipTrigger.SetProvider'로 넘긴다).
    // 행이 없는 등급은 적지 않는다. 골드가 섞인 풀이면 골드 줄을 따로 둔다.
    public TooltipContent BuildRateTooltip()
    {
        var content = new TooltipContent($"{Name} 확률");

        if (_totalWeight <= 0L)
        {
            return content.Row("뽑을 항목이 없다", "");
        }

        for (GlobalRarity rarity = GlobalRarity.Mythic; rarity >= GlobalRarity.Common; rarity--)
        {
            long weight = _rarityWeights[(int)rarity];

            if (weight > 0L)
            {
                content.Row(RarityLabel.Get(rarity), FormatRate(weight), "", RarityPalette.Get(rarity));
            }
        }

        if (_rarityWeights[(int)GlobalRarity.None] > 0L)
        {
            content.Row("등급 없음", FormatRate(_rarityWeights[(int)GlobalRarity.None]));
        }

        if (_goldWeight > 0L)
        {
            content.Row("골드", FormatRate(_goldWeight));
        }

        return content.Row("10회도 한 번마다 같은 확률", "");
    }

    // 가중치 → '2.7%'. 소수 둘째 자리까지 — 작은 등급(0.3% 남짓)도 0으로 뭉개지지 않는다.
    private string FormatRate(long weight) => $"{weight * 100.0 / _totalWeight:0.##}%";

    #endregion

    #region 집계

    // 항목 하나를 센다. 아이콘은 지금까지보다 높은 등급일 때만 만든다(만드는 비용이 조회 몇 번이다).
    private void Add(GlobalRarity rarity, int weight, System.Func<ItemIconContent> icon)
    {
        int index = Mathf.Clamp((int)rarity, 0, _rarityWeights.Length - 1);

        _rarityWeights[index] += weight;
        _totalWeight          += weight;

        if (rarity > _iconRarity)
        {
            _iconRarity = rarity;
            _icon       = icon();
        }
    }

    // 장비 종류가 하나로 모이는지 본다 — 둘 이상 섞이면 '장비'로 쓴다.
    private void TrackEquipKind(int equipTid)
    {
        if (!GameDataLoader.TryGetEquip(equipTid, out EquipTableRow row))
        {
            return;
        }

        if (_equipKinds == 1)
        {
            _equipKind = row.EquipKind;
        }
        else if (_equipKind.HasValue && _equipKind.Value != row.EquipKind)
        {
            _equipKind = null;
        }
    }

    // 이 종류만 있는가 — 다른 종류가 하나도 없고 골드도 없다.
    private bool IsOnly(int kinds) => kinds > 0
                                      && kinds == _characterKinds + _equipKinds + _itemKinds
                                      && _goldWeight == 0L;

    // 카드 아이콘에는 능력치 칸을 그리지 않는다 — 뽑기 전이라 옵션이 없어 빈 칸만 늘어선다.
    private static ItemIconContent WithoutSockets(ItemIconContent content)
        => new ItemIconContent(content.Rarity, content.Glyph, "", null, content.Icon);

    #endregion
}
