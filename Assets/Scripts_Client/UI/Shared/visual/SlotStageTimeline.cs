using UnityEngine;

// 판정 주기 안의 한 순간 → "지금 무슨 동작의 몇 번째 프레임인가". 상태가 없는 계산표다.
// 무대('SlotStageView')가 매 프레임 부른다. 근거는 게임UI 2.5.
//
// ■ 한 주기는 [여운] → [숨] → [달리기] → [숨] → [공격]
//   여운   : 앞 주기를 끝낸 공격(처치 타)의 남은 프레임. 쓰러진 대상이 그 자리에서 사라진다.
//   숨     : 대기 자세 한순간 — 달리기와 공격이 바로 맞붙으면 뚝뚝 끊겨 보여서 사이에 끼운다.
//   달리기 : 다음 대상이 왼쪽에서 땅과 같이 흘러온다.
//   공격   : 대상에 닿은 순간부터 **1 → 2 → … → n → 1 → 2 …** 앞에서부터 순서대로 친다.
//            주기 끝(판정)에 타격이 닿는 공격이 곧 처치 타다 — 빨리 죽으면 1타 하나, 오래 걸리면 1234 1234 ….
//
// ■ 공격 몇 번을 칠지 · 한 번의 길이
//   공격 구간에 공격 m번을 넣고, m번째 공격의 타격이 정확히 주기 끝에 오도록 공격 한 번의 길이를 살짝 늘이거나 줄인다.
//   m은 그 길이가 '공격 한 번(초)'에 가장 가까워지는 수로 고른다 — 구간이 길수록 늘이고 줄이는 폭이 작다.
//   프레임 하나 = 공격 길이 ÷ 프레임 수. 타격(번쩍임) = 공격 시작 + 타격 프레임 ÷ 프레임 수 × 공격 길이.
//   (2026-10-05 이전에는 판정에서 거꾸로 세어 마지막 타를 처치 타로 고정했다 — 칸 수에 따라 순서가 1·2·3·1·…·4,
//    짧은 주기엔 4타만 나가는 등 뒤섞여 보여서 앞에서부터 세는 방식으로 바꿨다.)
//
// ■ 짧은 주기는 여운 → 숨 → 달리기 순으로 줄인다
//   1타의 타격까지는 남긴다. 판정 순간(처치 타의 타격 = 주기 끝)은 건드리지 않는다.
public static class SlotStageTimeline
{
    public enum EPhase { Recover, Idle, Run, Attack }

    public readonly struct Result
    {
        public readonly EPhase Phase;
        public readonly int    Frame;       // 그 동작 안의 프레임 번호 (숨은 늘 0)
        public readonly int    Combo;       // 공격 순서 (여운·공격일 때)
        public readonly float  SinceHit;    // 가장 최근 타격 뒤로 흐른 초. 음수면 아직 이번 대상에 타격 없음 (공격 전 구간은 앞 대상의 처치 타 기준)
        public readonly int    HitCombo;    // 그 타격을 낸 공격 순서 — 타격 이펙트를 고른다
        public readonly float  RunStart;    // 달리기가 시작되는 순간 — 땅이 흐르기 시작한다
        public readonly float  RunEnd;      // 달리기가 끝나는 순간 — 대상이 멈춤 자리에 닿는다
        public readonly float  AttackStart; // 공격이 시작되는 순간

        public bool HitsComing => Phase == EPhase.Attack && SinceHit >= 0f; // 최근 타격이 다가온 대상의 것인가 (아니면 쓰러진 대상)

        public Result(EPhase phase, int frame, int combo, float sinceHit, int hitCombo, float runStart, float runEnd, float attackStart)
        {
            Phase       = phase;
            Frame       = frame;
            Combo       = combo;
            SinceHit    = sinceHit;
            HitCombo    = hitCombo;
            RunStart    = runStart;
            RunEnd      = runEnd;
            AttackStart = attackStart;
        }
    }

    // t     : 이번 주기 안의 초 (0 ~ cycle)
    // cycle : 판정 1회의 초
    public static Result Evaluate(float t, float cycle, CharacterVisual character, SlotStageSettings settings)
    {
        CharacterVisual.AttackMotion[] attacks = character.Attacks;
        int runFrames = character.RunFrames.Length;

        // 공격 그림이 없으면 달리기만 한다
        if (attacks.Length == 0)
        {
            return new Result(EPhase.Run, RunFrame(t, runFrames, settings), 0, -1f, 0, 0f, cycle, cycle);
        }

        float atk      = settings.AttackSeconds;
        float approach = settings.ApproachSeconds;
        float idle     = settings.IdleSeconds;
        int   n        = attacks.Length;

        // 구간 나누기 — 1타의 타격까지는 남기고, 모자라면 여운 → 숨 → 달리기 순으로 줄인다
        float avail    = Mathf.Max(0f, cycle - HitFraction(attacks[0]) * atk);
        float recover  = Mathf.Min(settings.RecoverSeconds, Mathf.Max(0f, avail - approach - 2f * idle));
        float rest     = avail - recover;
        float breath   = Mathf.Max(0f, Mathf.Min(idle, (rest - approach) / 2f));
        float run      = Mathf.Max(0f, Mathf.Min(approach, rest - 2f * breath));

        float runStart = recover + breath;
        float runEnd   = runStart + run;
        float start    = runEnd + breath;

        // 공격 횟수와 한 번의 길이 — 마지막(처치) 공격의 타격이 주기 끝에 닿게
        (int count, float length) = Fit(cycle - start, attacks, atk);
        int last = (count - 1) % n;

        if (t < recover)
        {
            // 앞 주기의 처치 타를 타격 프레임부터 이어서 — 주기가 같으면 처치 타도 같다
            int frames = attacks[last].frames.Length;
            int frame  = Mathf.Min(frames - 1, Mathf.FloorToInt((HitFraction(attacks[last]) + t / length) * frames));

            return new Result(EPhase.Recover, Mathf.Max(0, frame), last, t, last, runStart, runEnd, start);
        }

        if (t < runStart || (t >= runEnd && t < start))
        {
            return new Result(EPhase.Idle, 0, 0, t, last, runStart, runEnd, start);
        }

        if (t < runEnd)
        {
            return new Result(EPhase.Run, RunFrame(t - runStart, runFrames, settings), 0, t, last, runStart, runEnd, start);
        }

        // 몇 번째 공격인가 — 앞에서부터 센다
        int   index       = Mathf.Clamp(Mathf.FloorToInt((t - start) / length), 0, count - 1);
        int   combo       = index % n;
        float comboStart  = start + index * length;
        int   frameCount  = attacks[combo].frames.Length;
        int   attackFrame = Mathf.Clamp(Mathf.FloorToInt((t - comboStart) / length * frameCount), 0, Mathf.Max(0, frameCount - 1));

        // 지난 타격 중 가장 최근 것 — 이 공격의 타격이 아직이면 앞 공격의 것(첫 공격이면 아직 없음)
        float thisHit  = comboStart + HitFraction(attacks[combo]) * length;
        int   hitIndex = thisHit <= t ? index : index - 1;
        float since    = hitIndex < 0 ? -1f : t - (start + hitIndex * length + HitFraction(attacks[hitIndex % n]) * length);

        return new Result(EPhase.Attack, attackFrame, combo, since, Mathf.Max(0, hitIndex) % n, runStart, runEnd, start);
    }

    // 공격 구간에 몇 번 넣을지 — m번째 타격이 구간 끝에 닿는 길이가 '공격 한 번'에 가장 가까운 m
    //   구간 = (m − 1) × 길이 + m번째 공격의 타격 비율 × 길이
    private static (int count, float length) Fit(float window, CharacterVisual.AttackMotion[] attacks, float atk)
    {
        int   n         = attacks.Length;
        int   bestCount = 1;
        float best      = window / Mathf.Max(0.01f, HitFraction(attacks[0]));
        int   maxCount  = Mathf.Max(1, Mathf.CeilToInt(window / atk) + 1);

        for (int m = 2; m <= maxCount; m++)
        {
            float length = window / (m - 1 + Mathf.Max(0.01f, HitFraction(attacks[(m - 1) % n])));

            if (Mathf.Abs(Mathf.Log(length / atk)) < Mathf.Abs(Mathf.Log(best / atk)))
            {
                best      = length;
                bestCount = m;
            }
        }

        return (bestCount, Mathf.Max(0.01f, best));
    }

    // 공격 시작 → 타격까지가 공격 한 번의 몇 할인가
    private static float HitFraction(CharacterVisual.AttackMotion motion)
        => motion.frames.Length > 0 ? (float)motion.hitFrame / motion.frames.Length : 1f;

    private static int RunFrame(float t, int runFrames, SlotStageSettings settings)
    {
        if (runFrames == 0)
        {
            return 0;
        }

        return Mathf.Min(runFrames - 1, Mathf.FloorToInt(Mathf.Repeat(t / settings.RunLoopSeconds, 1f) * runFrames));
    }
}
