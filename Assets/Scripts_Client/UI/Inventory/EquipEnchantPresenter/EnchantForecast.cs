using System;
using GameData;

// 큐브 자동의 예상 확률 — 지금 등급에서 큐브를 한 개씩 쓸 때 목표에 닿는 누적 확률 (이슈 #53).
//
// ■ 테이블만 읽는다 — 엑셀을 바꾸면 화면도 따라간다
//   등급 상승 = 'GameDataLoader.GetEnchantUpPermyriad'(서버 판정식의 사본) · 칸 = 'EnchantOptionTable.Weight' ÷ 그 등급 가중치 합.
//   칸 조건 확률은 켠 조합마다 다항 확률(C(n,k)·전체^k·노린 것^(n−k))을 더한다.
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

        for (int g = 0; g < GradeCount; g++)
        {
            var grade = (GlobalRarity)g;

            up[g]  = grade == GlobalRarity.None ? 1d : GameDataLoader.GetEnchantUpPermyriad(grade, cubeTid) / 10000d;
            hit[g] = grade != GlobalRarity.None && grade >= target.Grade ? SlotChanceAt(grade, slotCount, target) : 0d;
        }

        var state = new double[GradeCount];
        var next  = new double[GradeCount];

        state[(int)current] = 1d;

        double done = 0d, missSum = 0d, withinCap = 0d;
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

                done         += q * u * hit[raised];
                next[raised] += q * u * (1d - hit[raised]);

                if (g != (int)GlobalRarity.None)
                {
                    done    += q * (1d - u) * hit[g];
                    next[g] += q * (1d - u) * (1d - hit[g]);
                }
            }

            (state, next) = (next, state);

            missSum += 1d - done;

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

            if (done > 0.99999d)
            {
                if (n < cap)
                {
                    withinCap = done;
                }

                break;
            }
        }

        // 평균 = Σ P(아직 못 닿음) — 0번째(1)를 더한다
        double mean = done > 0.999d ? missSum + 1d : 0d;

        return new Result(SlotChanceAt(target.Grade, slotCount, target), withinCap, half, ninety, mean);
    }

    // 그 등급에서 한 번 굴려 칸 조건이 맞을 확률 — 칸 조건이 없으면 1.
    public static double SlotChanceAt(GlobalRarity grade, int slotCount, EnchantAutoTarget target)
    {
        if (!target.UsesSlots)
        {
            return 1d;
        }

        ReadShares(grade, target.Focus, out double all, out double focus);

        double chance = 0d;

        for (int k = 0; k <= slotCount; k++)
        {
            if (target.IsComboOn(k))
            {
                chance += Choose(slotCount, k) * Math.Pow(all, k) * Math.Pow(focus, slotCount - k);
            }
        }

        return chance;
    }

    // 그 등급의 칸 하나에 '전체' · 노린 옵션이 뜰 확률 — 가중치 ÷ 그 등급 가중치 합.
    public static void ReadShares(GlobalRarity grade, EnchantAutoFocus focus, out double all, out double wanted)
    {
        long total = 0, allWeight = 0, focusWeight = 0;

        foreach (EnchantOptionTableRow row in GameDataLoader.EnchantOptions)
        {
            if (row.Grade != grade)
            {
                continue;
            }

            total += row.Weight;

            if (EnchantAutoTarget.IsAllIndustry(row))
            {
                allWeight += row.Weight;
            }
            else if (EnchantAutoTarget.IsFocus(row, focus))
            {
                focusWeight += row.Weight;
            }
        }

        all    = total > 0 ? (double)allWeight / total : 0d;
        wanted = total > 0 ? (double)focusWeight / total : 0d;
    }

    // 이항 계수 C(n, k).
    private static double Choose(int n, int k)
    {
        double result = 1d;

        for (int i = 1; i <= k; i++)
        {
            result = result * (n - k + i) / i;
        }

        return result;
    }
}
