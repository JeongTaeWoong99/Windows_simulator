using System;
using System.Collections.Generic;
using GameData;

// 큐브 자동의 예상 확률 — 지금 등급에서 큐브를 한 개씩 쓸 때 목표에 닿는 누적 확률 (이슈 #53).
//
// ■ 테이블만 읽는다 — 엑셀을 바꾸면 화면도 따라간다
//   등급 상승 = 'GameDataLoader.GetEnchantUpPermyriad'(서버 판정식의 사본) · 칸 = 'EnchantOptionTable.Weight' ÷ 그 등급 가중치 합.
//   칸 조건 확률은 칸 종류의 모든 묶음을 세어 줄 조건에 맞는 것을 더한다(칸마다 따로 뽑으므로 곱).
//
// ■ 상태 = 인챈트 등급 하나 (없음 + 6단계)
//   등급은 내려가지 않고, 상급 큐브 자동도 오른 등급은 늘 남긴다 — 그래서 두 큐브 모두 같은 연쇄로 센다.
//   한 번 굴릴 때마다 "오른다 / 그대로"로 나뉘고, 굴린 등급이 목표 이상이면 그 등급의 칸 조건 확률로 끝난다.
//   기획 1.4 표(인챈트 큐브 없음 → 신화 평균 2,794.75개)와 같은 값이 나온다.
public static class EnchantForecast
{
    // 계산을 멈추는 횟수 — 이 안에 99.999%가 안 되면 평균을 모른다고 답한다.
    private const int MaxSteps = 100_000;

    private const int GradeCount = 7; // 없음 + 일반~신화

    public readonly struct Result
    {
        public readonly double SlotChance;   // 목표 등급에서 한 번 굴려 칸 조건이 맞을 확률 (칸 조건이 없으면 1)
        public readonly double WithinCap;    // 상한 안에 닿을 확률
        public readonly int    Half;         // 절반이 닿는 개수 (0 = 계산 범위 밖)
        public readonly int    Ninety;       // 열에 아홉이 닿는 개수 (0 = 계산 범위 밖)
        public readonly double Mean;         // 평균 개수 (0 = 계산 범위 밖)

        public Result(double slotChance, double withinCap, int half, int ninety, double mean)
        {
            SlotChance = slotChance;
            WithinCap  = withinCap;
            Half       = half;
            Ninety     = ninety;
            Mean       = mean;
        }
    }

    // 지금 등급에서 시작해 'cap'개 안에 목표에 닿을 확률 등을 센다.
    //   current   : 장비의 지금 인챈트 등급 ('None' = 아직 없음)
    //   slotCount : 장비 등급이 정한 칸 수
    public static Result Compute(GlobalRarity current, int slotCount, int cubeTid, EnchantAutoTarget target, int cap)
    {
        // 등급마다 한 번 굴려 목표에 닿을 확률 — 목표 미만이면 0
        var hit = new double[GradeCount];
        var up  = new double[GradeCount];

        // 오른 등급에서 목표 없이 멈출 확률 — 편의 토글('EnchantAutoTarget.StopsEarly'). 멈추면 그 자동은 끝난다(실패로 센다)
        var halt = new double[GradeCount];

        for (int g = 0; g < GradeCount; g++)
        {
            var grade = (GlobalRarity)g;

            up[g]   = grade == GlobalRarity.None ? 1d : GameDataLoader.GetEnchantUpPermyriad(grade, cubeTid) / 10000d;
            hit[g]  = grade != GlobalRarity.None && grade >= target.Grade ? SlotChanceAt(grade, slotCount, target) : 0d;
            halt[g] = g > 0 && target.StopsEarly((GlobalRarity)(g - 1), grade) ? 1d - hit[g] : 0d;
        }

        var state = new double[GradeCount];
        var next  = new double[GradeCount];

        state[(int)current] = 1d;

        double done = 0d, halted = 0d, missSum = 0d, withinCap = 0d;
        int    half = 0, ninety = 0;

        for (int n = 1; n <= MaxSteps; n++)
        {
            Array.Clear(next, 0, GradeCount);

            for (int g = 0; g < GradeCount; g++)
            {
                double q = state[g];

                if (q <= 0d)
                {
                    continue;
                }

                // 없음 → 일반은 판정 없이 확정. 최고 등급은 오를 곳이 없다(up = 0).
                int    raised = Math.Min(g + 1, GradeCount - 1);
                double u      = up[g];

                double stop   = raised != g ? halt[raised] : 0d;

                done         += q * u * hit[raised];
                halted       += q * u * stop;
                next[raised] += q * u * (1d - hit[raised] - stop);

                if (g != (int)GlobalRarity.None)
                {
                    done    += q * (1d - u) * hit[g];
                    next[g] += q * (1d - u) * (1d - hit[g]);
                }
            }

            (state, next) = (next, state);

            missSum += 1d - done - halted;

            if (half == 0 && done >= 0.5d)
            {
                half = n;
            }

            if (ninety == 0 && done >= 0.9d)
            {
                ninety = n;
            }

            if (n == cap)
            {
                withinCap = done;
            }

            if (done + halted > 0.99999d)
            {
                if (n < cap)
                {
                    withinCap = done;
                }

                break;
            }
        }

        // 평균 = Σ P(아직 안 끝남) — 0번째(1)를 더한다. 편의 토글로 멈추는 것도 끝으로 센다(그 자동이 큐브를 몇 개 쓰나)
        double mean = done + halted > 0.999d ? missSum + 1d : 0d;

        return new Result(SlotChanceAt(target.Grade, slotCount, target), withinCap, half, ninety, mean);
    }

    // 그 등급에서 한 번 굴려 칸 조건이 맞을 확률 — 칸 조건이 없으면 1.
    // 칸마다 따로 뽑으므로 칸 종류(고를 칩 6개 + 그 밖 1개)의 모든 묶음(최대 7³ = 343)을 세어 맞는 것의 확률을 더한다.
    public static double SlotChanceAt(GlobalRarity grade, int slotCount, EnchantAutoTarget target)
    {
        if (!target.UsesSlots(slotCount))
        {
            return 1d;
        }

        double[] share = ReadKindShares(grade);
        int      n     = Math.Min(slotCount, EnchantAutoTarget.MaxSlots);
        var      kinds = new EnchantAutoFocus[n];

        return Sum(0, 1d);

        // 칸 'slot'부터 종류를 하나씩 정해 맞는 묶음의 확률을 더한다.
        double Sum(int slot, double p)
        {
            if (p <= 0d)
            {
                return 0d;
            }

            if (slot == n)
            {
                return target.Fits(kinds) ? p : 0d;
            }

            double total = 0d;

            for (int k = 0; k < share.Length; k++)
            {
                kinds[slot] = (EnchantAutoFocus)k;
                total      += Sum(slot + 1, p * share[k]);
            }

            return total;
        }
    }

    // 나올 수 있는 결과 — 칸 종류 묶음(순서 없이)마다 그 등급에서 한 번에 나올 확률. 높은 순 (큐브 창 조합 툴팁).
    // 'SlotChanceAt'과 같은 셈을 묶음별로 나눠 담는다 — 다 더하면 'SlotChanceAt'이 된다.
    public static List<(EnchantAutoFocus[] Kinds, double Chance)> ListOutcomes(GlobalRarity grade, int slotCount, EnchantAutoTarget target)
    {
        var found = new Dictionary<string, (EnchantAutoFocus[] Kinds, double Chance)>();

        if (!target.UsesSlots(slotCount))
        {
            return new List<(EnchantAutoFocus[], double)>();
        }

        double[] share = ReadKindShares(grade);
        int      n     = Math.Min(slotCount, EnchantAutoTarget.MaxSlots);
        var      kinds = new EnchantAutoFocus[n];

        Walk(0, 1d);

        var list = new List<(EnchantAutoFocus[] Kinds, double Chance)>(found.Values);
        list.Sort((a, b) => b.Chance.CompareTo(a.Chance));

        return list;

        void Walk(int slot, double p)
        {
            if (p <= 0d)
            {
                return;
            }

            if (slot == n)
            {
                if (!target.Fits(kinds))
                {
                    return;
                }

                var sorted = (EnchantAutoFocus[])kinds.Clone();
                Array.Sort(sorted, (a, b) => KindOrder(a).CompareTo(KindOrder(b)));

                string key = string.Join(",", sorted);
                found[key] = found.TryGetValue(key, out var had) ? (had.Kinds, had.Chance + p) : (sorted, p);

                return;
            }

            for (int k = 0; k < share.Length; k++)
            {
                kinds[slot] = (EnchantAutoFocus)k;
                Walk(slot + 1, p * share[k]);
            }
        }
    }

    // 묶음 안 순서 — 칩 순서, '기타'(None)는 맨 뒤
    private static int KindOrder(EnchantAutoFocus kind) => kind == EnchantAutoFocus.None ? int.MaxValue : Array.IndexOf(EnchantAutoTarget.PickSteps, kind);

    // 그 등급의 칸 하나에 종류별('EnchantAutoTarget.KindOf' — 인덱스 = enum 값)로 뜰 확률 — 가중치 ÷ 그 등급 가중치 합.
    private static double[] ReadKindShares(GlobalRarity grade)
    {
        var  weight = new long[Enum.GetValues(typeof(EnchantAutoFocus)).Length];
        long total  = 0;

        foreach (EnchantOptionTableRow row in GameDataLoader.EnchantOptions)
        {
            if (row.Grade != grade)
            {
                continue;
            }

            total                                     += row.Weight;
            weight[(int)EnchantAutoTarget.KindOf(row)] += row.Weight;
        }

        var share = new double[weight.Length];

        for (int k = 0; k < weight.Length; k++)
        {
            share[k] = total > 0 ? (double)weight[k] / total : 0d;
        }

        return share;
    }
}
