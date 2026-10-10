using System.Collections.Generic;
using System.Text;
using GameData;
using MikaProtocol;

// 큐브 자동에서 노릴 옵션 — 없음이면 등급만 본다. 나머지는 '전체'(전 산업 속도)와 짝을 이룬다.
public enum EnchantAutoFocus
{
    None,
    Farming,
    Fishing,
    Mining,
    Logging,
    Hunting,
    CharacterExp,
}

// 큐브 자동의 목표 — 목표 등급 · 칸 조건 · 상한, 그리고 상급 큐브가 무엇을 남길지 (이슈 #53).
//
// ■ 칸 조건 = '전체' + 노릴 옵션 하나로 만든 조합을 OR로 켠다
//   3칸에 낚시면 [전체 전체 전체] [전체 전체 낚시] [전체 낚시 낚시] [낚시 낚시 낚시] — 켠 것 중 하나면 맞다.
//   칸 순서는 보지 않는다 — '전체'가 몇 칸인가로 센다('Combos[전체 칸 수]').
//
// 화면을 모른다 — 큐브 창이 칩을 누를 때 값을 바꾸고, 자동 루프가 'Matches'·'PreferNew'를 묻는다.
public sealed class EnchantAutoTarget
{
    // 칸 수의 상한 — 조합 인덱스가 0~3이다 ('EnchantGradeTable.SlotCount'의 최댓값).
    public const int MaxSlots = 3;

    // 목표 등급 — 이 등급 **이상**이면 등급 조건을 채운다.
    public GlobalRarity Grade { get; set; } = GlobalRarity.Mythic;

    // 노릴 옵션. 'None'이면 칸 조건이 없다.
    public EnchantAutoFocus Focus { get; set; } = EnchantAutoFocus.None;

    // 이번 자동에 쓸 큐브 상한.
    public int Cap { get; set; } = 100;

    // 켠 조합 — 인덱스 = '전체' 칸 수. 칸 수가 바뀌면 'ResetCombos'로 전부 켠다.
    private readonly bool[] _combos = { true, true, true, true };

    public bool UsesSlots => Focus != EnchantAutoFocus.None;

    public bool IsComboOn(int allCount) => allCount >= 0 && allCount < _combos.Length && _combos[allCount];

    public void ToggleCombo(int allCount)
    {
        if (allCount >= 0 && allCount < _combos.Length)
        {
            _combos[allCount] = !_combos[allCount];
        }
    }

    // 조합을 전부 켠다 (장비가 바뀌어 칸 수가 달라질 때).
    public void ResetCombos()
    {
        for (int i = 0; i < _combos.Length; i++)
        {
            _combos[i] = true;
        }
    }

    // 칸 조건을 쓰는데 켠 조합이 하나도 없는가 — 그러면 시작할 수 없다.
    public bool HasNoCombo(int slotCount)
    {
        if (!UsesSlots)
        {
            return false;
        }

        for (int k = 0; k <= slotCount && k < _combos.Length; k++)
        {
            if (_combos[k])
            {
                return false;
            }
        }

        return true;
    }

    // 이 결과가 목표에 닿았나 — 등급 이상 + (칸 조건이면) 칸 전부가 노린 것이고 '전체' 칸 수가 켠 조합이다.
    public bool Matches(GlobalRarity grade, IReadOnlyList<int> optionTids)
    {
        if (grade < Grade)
        {
            return false;
        }

        if (!UsesSlots)
        {
            return true;
        }

        int allCount = 0;

        foreach (int tid in optionTids)
        {
            if (!GameDataLoader.TryGetEnchantOption(tid, out EnchantOptionTableRow row))
            {
                return false;
            }

            if (IsAllIndustry(row))
            {
                allCount++;
            }
            else if (!IsFocus(row, Focus))
            {
                return false;
            }
        }

        return IsComboOn(allCount);
    }

    // 상급 큐브 자동 — 목표에 못 닿은 새 값을 남길까.
    // 등급이 오르면 새 값(오른 등급을 버릴 이유가 없다), 같으면 노린 칸이 **더 많을 때만** 새 값 — 같으면 이전을 둔다.
    public bool PreferNew(GlobalRarity before, IReadOnlyList<int> beforeTids, GlobalRarity after, IReadOnlyList<int> afterTids)
    {
        if (after != before)
        {
            return after > before;
        }

        return CountWanted(afterTids) > CountWanted(beforeTids);
    }

    // 노린 칸 수 — '전체' 또는 노릴 옵션 (PreferNew에서 호출).
    private int CountWanted(IReadOnlyList<int> optionTids)
    {
        int count = 0;

        foreach (int tid in optionTids)
        {
            if (GameDataLoader.TryGetEnchantOption(tid, out EnchantOptionTableRow row) && (IsAllIndustry(row) || IsFocus(row, Focus)))
            {
                count++;
            }
        }

        return count;
    }

    // '전체' — 전 산업 속도 옵션인가.
    public static bool IsAllIndustry(EnchantOptionTableRow row)
        => row.OptionType == EnchantOptionType.Speed && row.Industry == IndustryType.None;

    // 노릴 옵션인가.
    public static bool IsFocus(EnchantOptionTableRow row, EnchantAutoFocus focus) => focus switch
    {
        EnchantAutoFocus.None         => false,
        EnchantAutoFocus.CharacterExp => row.OptionType == EnchantOptionType.CharacterExp,
        _                             => row.OptionType == EnchantOptionType.Speed && row.Industry == ToIndustry(focus),
    };

    private static IndustryType ToIndustry(EnchantAutoFocus focus) => focus switch
    {
        EnchantAutoFocus.Farming => IndustryType.Farming,
        EnchantAutoFocus.Fishing => IndustryType.Fishing,
        EnchantAutoFocus.Mining  => IndustryType.Mining,
        EnchantAutoFocus.Logging => IndustryType.Logging,
        EnchantAutoFocus.Hunting => IndustryType.Hunting,
        _                        => IndustryType.None,
    };

    // 칩 글씨 — '농사' · '경험치'. 산업 이름은 'IndustryLabel'을 따른다.
    public static string GetFocusName(EnchantAutoFocus focus) => focus switch
    {
        EnchantAutoFocus.None         => "없음",
        EnchantAutoFocus.CharacterExp => "경험치",
        _                             => IndustryLabel.Get((EIndustryType)(byte)ToIndustry(focus)),
    };

    // 조합 칩 글씨 — '전체/전체/낚시'. 칸마다 '/'로 갈라 무엇을 노리는지 칸 단위로 읽히게 한다(사용자 결정 2026-10-10).
    // 노릴 옵션이 '없음'이면 칸 조건이 없다 — '—'.
    public static string GetComboLabel(EnchantAutoFocus focus, int slotCount, int allCount)
    {
        if (focus == EnchantAutoFocus.None)
        {
            return "—";
        }

        var    text  = new StringBuilder();
        string other = GetFocusName(focus);

        for (int i = 0; i < slotCount; i++)
        {
            if (i > 0)
            {
                text.Append('/');
            }

            text.Append(i < allCount ? "전체" : other);
        }

        return text.ToString();
    }

    // 켠 조합 전부 — '칸 [전체/낚시/낚시] [전체/전체/전체]' — 켠 것끼리는 OR (큐브 창 규칙 줄 둘째 줄 — 설정 중 · 도는 중).
    // 전부 켰으면 줄여 쓴다 — '칸마다 전체 또는 낚시'. 네 묶음을 늘어놓으면 한 줄을 넘는다(사용자 지적 2026-10-11).
    public string DescribeCombos(int slotCount)
    {
        if (IsEveryComboOn(slotCount))
        {
            return $"칸마다 전체 또는 {GetFocusName(Focus)}";
        }

        var text = new StringBuilder();

        for (int allCount = slotCount; allCount >= 0; allCount--)
        {
            if (!IsComboOn(allCount))
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append('[').Append(GetComboLabel(Focus, slotCount, allCount)).Append(']');
        }

        return text.Length == 0 ? "칸 조합 없음" : $"칸 {text}";
    }

    // 그 칸 수에서 고를 수 있는 조합(전체 0~칸 수)을 전부 켰나 (DescribeCombos에서 호출).
    private bool IsEveryComboOn(int slotCount)
    {
        for (int k = 0; k <= slotCount && k < _combos.Length; k++)
        {
            if (!_combos[k])
            {
                return false;
            }
        }

        return true;
    }
}
