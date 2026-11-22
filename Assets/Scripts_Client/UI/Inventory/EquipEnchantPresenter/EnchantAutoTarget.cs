using System.Collections.Generic;
using System.Text;
using GameData;
using MikaProtocol;

// 큐브 자동에서 칸마다 고르는 옵션 — 'None'은 '전체'(그 칸은 아무 옵션이나 된다).
public enum EnchantAutoFocus
{
    None,
    Farming,
    Excavation,
    Mining,
    Logging,
    Hunting,
    CharacterExp,
}

// 큐브 자동의 목표 — 목표 등급 · 칸마다 고른 옵션 · 상한, 그리고 상급 큐브가 무엇을 남길지 (이슈 #53).
//
// ■ 칸 조건 = 칸 줄마다 "이 칸에 나와도 되는 옵션" (2026-11-22 사용자 — 시안 A '칸마다 고르기')
//   장비 칸 수만큼 줄이 있고, 줄마다 '전체' 또는 옵션 여러 개를 켠다. 한 줄에 여러 개면 그중 아무거나다.
//   '전체'를 켜면 그 줄의 다른 옵션이 꺼지고, 다른 옵션을 켜면 '전체'가 꺼진다. 다 끄면 다시 '전체'다.
//   예) 농 · 경 · 농+경 → [농 경 농] 또는 [농 경 경].
//   결과의 칸 순서는 보지 않는다 — 결과 칸을 줄에 하나씩 짝지어 맞는 짝이 하나라도 있으면 맞다.
//   '전 산업 속도'·전투 스탯은 고를 칩이 없다 — '전체' 줄에만 맞는다.
//
// 화면을 모른다 — 큐브 창이 칩을 누를 때 값을 바꾸고, 자동 루프가 'Matches'·'PreferNew'를 묻는다.
public sealed class EnchantAutoTarget
{
    // 칸 수의 상한 — 줄이 셋이다 ('EnchantGradeTable.SlotCount'의 최댓값).
    public const int MaxSlots = 3;

    // 줄 하나에 고를 수 있는 칩 — '전체' 다음은 화면 공용 표시 순서('IndustryLabel.DisplayOrder' — 발굴이 맨 끝) · 경험치.
    // 'EnchantAutoFocus' 값 순서와 다르다 — 칩 자리를 enum 값으로 바로 바꾸면 발굴이 둘째 칸에 온다.
    public static readonly EnchantAutoFocus[] PickSteps =
    {
        EnchantAutoFocus.None,
        EnchantAutoFocus.Farming,
        EnchantAutoFocus.Mining,
        EnchantAutoFocus.Logging,
        EnchantAutoFocus.Hunting,
        EnchantAutoFocus.Excavation,
        EnchantAutoFocus.CharacterExp,
    };

    // 칸 3개를 줄 3개에 짝짓는 모든 경우 — 칸 수가 적으면 앞쪽만 쓴다(PermutationsOf).
    private static readonly int[][] Perm1 = { new[] { 0 } };
    private static readonly int[][] Perm2 = { new[] { 0, 1 }, new[] { 1, 0 } };
    private static readonly int[][] Perm3 =
    {
        new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
        new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
    };

    // 목표 등급 — 이 등급 **이상**이면 등급 조건을 채운다.
    public GlobalRarity Grade { get; set; } = GlobalRarity.Mythic;

    // 이번 자동에 쓸 큐브 상한.
    public int Cap { get; set; } = 100;

    // 편의 토글 (2026-11-22 사용자) — 칸 조건이 안 맞아도 멈춘다. 둘 다 **등급이 오른 순간**만 본다.
    //   StopOnMythic  : 신화로 오르면 멈춘다 — 귀한 결과를 다음 굴림으로 날리지 않게
    //   StopAboveGoal : 목표보다 높은 등급으로 오르면 멈춘다 — '전설 농·농·농'을 노리다 신화가 뜬 경우
    public bool StopOnMythic  { get; set; } = true;
    public bool StopAboveGoal { get; set; } = true;

    // 줄마다 켠 옵션 — 비트 = 1 << (int)focus. 0이면 '전체'(아무거나).
    private readonly int[] _picks = new int[MaxSlots];

    // 그 줄에서 이 칩이 켜져 있나 — '전체'는 아무것도 안 켰을 때 켜져 있다.
    public bool IsPicked(int row, EnchantAutoFocus focus)
    {
        if (row < 0 || row >= MaxSlots)
        {
            return false;
        }

        return focus == EnchantAutoFocus.None ? _picks[row] == 0 : (_picks[row] & Bit(focus)) != 0;
    }

    // 칩을 누른다 — '전체'면 그 줄을 비우고(아무거나), 다른 칩이면 켜고 끈다. 다 끄면 '전체'로 돌아간다.
    public void TogglePick(int row, EnchantAutoFocus focus)
    {
        if (row < 0 || row >= MaxSlots)
        {
            return;
        }

        _picks[row] = focus == EnchantAutoFocus.None ? 0 : _picks[row] ^ Bit(focus);
    }

    // 모든 줄을 '전체'로 (장비가 바뀌어 칸 수가 달라질 때 · 처음).
    public void ResetPicks()
    {
        for (int i = 0; i < _picks.Length; i++)
        {
            _picks[i] = 0;
        }
    }

    // 칸 조건이 있나 — 장비 칸 수 안의 줄 중 하나라도 '전체'가 아니면.
    public bool UsesSlots(int slotCount)
    {
        for (int i = 0; i < slotCount && i < MaxSlots; i++)
        {
            if (_picks[i] != 0)
            {
                return true;
            }
        }

        return false;
    }

    // 이 결과가 목표에 닿았나 — 등급 이상 + 결과 칸을 줄에 짝지어 모든 칸이 맞는 짝이 있다.
    public bool Matches(GlobalRarity grade, IReadOnlyList<int> optionTids)
    {
        if (grade < Grade)
        {
            return false;
        }

        var kinds = new EnchantAutoFocus[System.Math.Min(optionTids.Count, MaxSlots)];

        for (int i = 0; i < kinds.Length; i++)
        {
            if (!GameDataLoader.TryGetEnchantOption(optionTids[i], out EnchantOptionTableRow row))
            {
                return false;
            }

            kinds[i] = KindOf(row);
        }

        return Fits(kinds);
    }

    // 목표에 못 닿았어도 여기서 멈출까 — 편의 토글. 등급이 오른 굴림만 본다('Matches'가 먼저다).
    public bool StopsEarly(GlobalRarity before, GlobalRarity after)
    {
        if (after <= before)
        {
            return false;
        }

        return (StopAboveGoal && after > Grade) || (StopOnMythic && after >= GlobalRarity.Mythic);
    }

    // 상급 큐브 자동 — 목표에 못 닿은 새 값을 남길까.
    // 등급이 오르면 새 값(오른 등급을 버릴 이유가 없다), 같으면 맞춘 줄이 **더 많을 때만** 새 값 — 같으면 이전을 둔다.
    public bool PreferNew(GlobalRarity before, IReadOnlyList<int> beforeTids, GlobalRarity after, IReadOnlyList<int> afterTids)
    {
        if (after != before)
        {
            return after > before;
        }

        return CountWanted(afterTids) > CountWanted(beforeTids);
    }

    // 결과 칸의 종류가 줄 조건에 맞나 — 짝 중 하나라도 모든 칸이 맞으면 (Matches · 'EnchantForecast'에서 호출).
    public bool Fits(IReadOnlyList<EnchantAutoFocus> kinds)
    {
        foreach (int[] perm in PermutationsOf(kinds.Count))
        {
            bool all = true;

            for (int i = 0; i < kinds.Count && all; i++)
            {
                all = RowAccepts(perm[i], kinds[i]);
            }

            if (all)
            {
                return true;
            }
        }

        return false;
    }

    // 맞춘 줄 수 — '전체'가 아닌 줄 중 결과 칸으로 채운 수의 최댓값 (PreferNew에서 호출).
    private int CountWanted(IReadOnlyList<int> optionTids)
    {
        int n    = System.Math.Min(optionTids.Count, MaxSlots);
        var kind = new EnchantAutoFocus[n];

        for (int i = 0; i < n; i++)
        {
            kind[i] = GameDataLoader.TryGetEnchantOption(optionTids[i], out EnchantOptionTableRow row) ? KindOf(row) : EnchantAutoFocus.None;
        }

        int best = 0;

        foreach (int[] perm in PermutationsOf(n))
        {
            int count = 0;

            for (int i = 0; i < n; i++)
            {
                int r = perm[i];

                if (_picks[r] != 0 && RowAccepts(r, kind[i]))
                {
                    count++;
                }
            }

            best = System.Math.Max(best, count);
        }

        return best;
    }

    // 줄이 이 종류의 칸을 받나 — '전체' 줄은 무엇이든, 아니면 켠 칩 중 하나.
    private bool RowAccepts(int row, EnchantAutoFocus kind)
        => _picks[row] == 0 || (kind != EnchantAutoFocus.None && (_picks[row] & Bit(kind)) != 0);

    private static int Bit(EnchantAutoFocus focus) => 1 << (int)focus;

    private static int[][] PermutationsOf(int n) => n switch
    {
        <= 1 => Perm1,
        2    => Perm2,
        _    => Perm3,
    };

    // 옵션의 종류 — 칩으로 고를 수 있는 것(산업 하나의 속도 · 경험치)이 아니면 'None'('전 산업 속도' · 전투 스탯).
    public static EnchantAutoFocus KindOf(EnchantOptionTableRow row)
    {
        if (row.OptionType == EnchantOptionType.CharacterExp)
        {
            return EnchantAutoFocus.CharacterExp;
        }

        if (row.OptionType != EnchantOptionType.Speed)
        {
            return EnchantAutoFocus.None;
        }

        return row.Industry switch
        {
            IndustryType.Farming    => EnchantAutoFocus.Farming,
            IndustryType.Excavation => EnchantAutoFocus.Excavation,
            IndustryType.Mining     => EnchantAutoFocus.Mining,
            IndustryType.Logging    => EnchantAutoFocus.Logging,
            IndustryType.Hunting    => EnchantAutoFocus.Hunting,
            _                       => EnchantAutoFocus.None,
        };
    }

    private static IndustryType ToIndustry(EnchantAutoFocus focus) => focus switch
    {
        EnchantAutoFocus.Farming    => IndustryType.Farming,
        EnchantAutoFocus.Excavation => IndustryType.Excavation,
        EnchantAutoFocus.Mining     => IndustryType.Mining,
        EnchantAutoFocus.Logging    => IndustryType.Logging,
        EnchantAutoFocus.Hunting    => IndustryType.Hunting,
        _                           => IndustryType.None,
    };

    // 결과 칸 종류 글씨 — 칩과 같되 'None'은 '기타'(칩으로 못 고르는 옵션 — 전 산업 속도 · 전투 스탯).
    public static string GetKindName(EnchantAutoFocus kind)
        => kind == EnchantAutoFocus.None ? Lang.Get("UI.Enchant.Other") : GetFocusName(kind);

    // 칸 줄 칩 글씨 — 영어는 짧게('Farm' · 'Dig'). 칩 7개가 한 줄이라 'Excavation'이 절반 크기로 줄었다(2026-11-22 사용자).
    //   툴팁·안내처럼 자리가 넉넉한 곳은 'GetFocusName'(정식 이름)을 쓴다.
    public static string GetChipName(EnchantAutoFocus focus) => focus switch
    {
        EnchantAutoFocus.None         => Lang.Get("UI.Enchant.All"),
        EnchantAutoFocus.CharacterExp => Lang.Get("UI.Equip.Exp"),
        _                             => Lang.Get($"UI.Enchant.Chip.{(EIndustryType)(byte)ToIndustry(focus)}"),
    };

    // 칩 글씨 — '전체' · '농사' · '경험치'. 산업 이름은 'IndustryLabel'을 따른다.
    public static string GetFocusName(EnchantAutoFocus focus) => focus switch
    {
        EnchantAutoFocus.None         => Lang.Get("UI.Enchant.All"),
        EnchantAutoFocus.CharacterExp => Lang.Get("UI.Equip.Exp"),
        _                             => IndustryLabel.Get((EIndustryType)(byte)ToIndustry(focus)),
    };

    // 나올 수 있는 결과 — '칸 [농사/농사/경험치] [농사/경험치/경험치] +2' (큐브 창 규칙 줄 둘째 줄 — 설정 중 · 도는 중).
    // 줄마다 켠 칩 하나씩을 고른 묶음을 칸 순서 없이 모은다('전체' 줄은 '전체'). 많으면 앞의 'MaxShown'개 + 남은 수.
    // 사용자 요청(2026-11-22) — 고른 조합으로 무엇이 나오면 멈추는지 안내에서 바로 읽히게.
    public string DescribeOutcomes(int slotCount)
    {
        const int MaxShown = 3;

        int n = System.Math.Min(slotCount, MaxSlots);

        var found = new List<string>();
        var seen  = new HashSet<string>();
        var pick  = new int[n]; // 줄마다 'PickSteps'에서 고른 자리

        Collect(0);

        var text = new StringBuilder();

        for (int i = 0; i < found.Count && i < MaxShown; i++)
        {
            text.Append(i > 0 ? " " : "").Append('[').Append(found[i]).Append(']');
        }

        if (found.Count > MaxShown)
        {
            text.Append(" +").Append(found.Count - MaxShown);
        }

        return Lang.Format("UI.Enchant.Combos", text);

        // 줄 'row'부터 칩을 하나씩 골라 묶음을 모은다 — 표시 순서로 정렬해 같은 묶음은 한 번만.
        void Collect(int row)
        {
            if (row == n)
            {
                var order = (int[])pick.Clone();
                System.Array.Sort(order, (a, b) => SortKey(a).CompareTo(SortKey(b)));

                var names = new StringBuilder();

                for (int i = 0; i < order.Length; i++)
                {
                    names.Append(i > 0 ? "/" : "").Append(GetFocusName(PickSteps[order[i]]));
                }

                string key = names.ToString();

                if (seen.Add(key))
                {
                    found.Add(key);
                }

                return;
            }

            for (int s = 0; s < PickSteps.Length; s++)
            {
                if (IsPicked(row, PickSteps[s]))
                {
                    pick[row] = s;
                    Collect(row + 1);
                }
            }
        }
    }

    // 묶음 안 정렬 — 칩 순서대로, '전체'(자리 0)는 맨 뒤.
    private static int SortKey(int step) => step == 0 ? int.MaxValue : step;
}
