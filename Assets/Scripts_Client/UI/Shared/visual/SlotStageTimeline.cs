using UnityEngine;

// 판정 주기 안의 한 순간 → "지금 무슨 동작의 몇 번째 프레임인가". 상태가 없는 계산표다.
// 무대('SlotStageView')가 매 프레임 부른다. 근거는 게임UI 2.5.
//
// ■ 한 주기는 [처치 타 나머지] → [달리기] → [공격] — 대기 자세 구간은 끼우지 않는다 (2026-10-12 · 달리기 양 끝 그림만은 아래)
//   처치 타 나머지 : 앞 주기를 끝낸 공격(처치 타)의 타격 뒤 프레임. 쓰러진 대상이 그 자리에서 사라진다.
//   달리기 : 다음 대상이 왼쪽에서 땅과 같이 흘러온다.
//   공격   : 대상에 닿은 순간부터 **1 → 2 → … → n → 1 → 2 …** 앞에서부터 순서대로 친다.
//            주기 끝(판정)에 타격이 닿는 공격이 곧 처치 타다 — 빨리 죽으면 1타 하나, 오래 걸리면 1234 1234 ….
//   (공격 ↔ 달리기 사이에 대기 자세(숨)·처치 뒤 머무는 시간(여운)을 두었다가 뺐다 — 자세가 한 번 더 바뀌어 뚝뚝 끊겨 보였다.)
//
// ■ 달리기 양 끝은 대기 자세 한 장 (2026-10-13)
//   달리기와 공격은 자세 차이가 커서 바로 바꾸면 확 튄다 — 달리기의 처음·끝 '사이 자세'만 대기 그림으로 보인다.
//   새 구간이 아니라 달리기 안에서 바꾼다 — 주기·공격 시작·판정 순간은 그대로다.
//   서 있는 동안 땅이 흐르면 미끄러져 보인다 — 사이 자세 동안은 땅·대상도 멈추고('MoveStart'~'MoveEnd' 밖),
//   대신 움직이는 동안 그만큼 빨리 흐른다('SlotStageView'가 속도를 움직이는 구간에 맞춘다).
//   길이는 초가 아니라 '그림 몇 장'(기본 2장) — 캐릭터마다 달리기 한 장 길이(달리기 한 바퀴 ÷ 장수)를 곱한다.
//   그래서 장수가 적은(한 장이 긴) 캐릭터가 들어와도 자기 리듬으로 두 장이다. 화면 프레임 단위는 FPS마다 길이가 달라 쓰지 않는다.
//   달리기의 1/4을 넘지 않는다 — 장수가 아주 적어도 달리기가 사라지거나 선 채로 미끄러져 보이지 않게.
//
// ■ 공격은 늘 '공격 한 번(초)' 그대로 끝까지 재생한다 (2026-10-12)
//   처치 타는 타격에서 주기가 끝나고, 나머지는 다음 주기 맨 앞에서 마저 한다 — 그래서 한 바퀴에 공격 m번이 온전히 들어간다.
//   한 바퀴 = 달리기 + 공격 m번. m은 달리기를 1초(다가오기) 이상으로 두고 들어가는 만큼이다.
//   공격 한 번이 안 되는 나머지(0.6초 미만)는 **달리기를 늘려** 메운다 — 공격 속도는 건드리지 않는다.
//   예: 1.6초(최소 주기) = 달리기 1 + 공격 1번 · 2.2초부터 공격 2번.
//   (2026-10-12 이전에는 남는 시간을 공격 길이를 늘이고 줄여 메웠다 — 최소 주기에서도 공격이 2번 나갔다.)
//
// ■ 짧은 주기는 달리기를 줄인다
//   공격 1번은 남긴다. 판정 순간(처치 타의 타격 = 주기 끝)은 건드리지 않는다.
//   최소 주기(엑셀 'MinCycleMs')가 연출 한 바퀴('RequiredCycleSeconds') 이상이면 평소엔 생기지 않는다.
public static class SlotStageTimeline
{
    public enum EPhase { Recover, Run, Attack }

    public readonly struct Result
    {
        public readonly EPhase Phase;
        public readonly int    Frame;       // 그 동작 안의 프레임 번호
        public readonly int    Combo;       // 공격 순서 (처치 타 나머지·공격일 때)
        public readonly float  SinceHit;    // 가장 최근 타격 뒤로 흐른 초. 음수면 아직 이번 대상에 타격 없음 (공격 전 구간은 앞 대상의 처치 타 기준)
        public readonly int    HitCombo;    // 그 타격을 낸 공격 순서 — 타격 이펙트를 고른다
        public readonly float  RunStart;    // 달리기가 시작되는 순간 — 땅이 흐르기 시작한다
        public readonly float  RunEnd;      // 달리기가 끝나는 순간 — 대상이 멈춤 자리에 닿는다
        public readonly float  AttackStart; // 공격이 시작되는 순간
        public readonly float  MoveStart;   // 땅·대상이 실제로 움직이기 시작하는 순간 — 달리기 앞 사이 자세 뒤
        public readonly float  MoveEnd;     // 움직임이 끝나는 순간 — 대상이 멈춤 자리에 닿는다. 뒤 사이 자세 동안은 멈춰 있다
        public readonly bool   IdlePose;    // 달리기 양 끝의 사이 자세 — 달리기 그림 대신 대기 그림 (머리 주석)

        public bool HitsComing => Phase == EPhase.Attack && SinceHit >= 0f; // 최근 타격이 다가온 대상의 것인가 (아니면 쓰러진 대상)

        public Result(EPhase phase, int frame, int combo, float sinceHit, int hitCombo, float runStart, float runEnd, float attackStart, float moveStart, float moveEnd, bool idlePose = false)
        {
            Phase       = phase;
            Frame       = frame;
            Combo       = combo;
            SinceHit    = sinceHit;
            HitCombo    = hitCombo;
            RunStart    = runStart;
            RunEnd      = runEnd;
            AttackStart = attackStart;
            MoveStart   = moveStart;
            MoveEnd     = moveEnd;
            IdlePose    = idlePose;
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
            return new Result(EPhase.Run, RunFrame(t, runFrames, settings), 0, -1f, 0, 0f, cycle, cycle, 0f, cycle);
        }

        int n = attacks.Length;

        // 공격 횟수 — 달리기를 다가오기 이상으로 두고 공격 한 번(초)이 온전히 들어가는 만큼. 늘 1번 이상
        int   count  = Mathf.Max(1, Mathf.FloorToInt((cycle - settings.ApproachSeconds) / settings.AttackSeconds + 0.0001f));
        int   last   = (count - 1) % n;
        float length = Mathf.Max(0.01f, Mathf.Min(settings.AttackSeconds, cycle)); // 주기가 공격 한 번보다도 짧을 때만 줄인다

        // 처치 타 나머지 — 앞 주기의 처치 타를 타격 뒤부터 끝까지. 주기가 같으면 처치 타도 같다
        float hit  = HitFraction(attacks[last]) * length;
        float tail = length - hit;

        // 공격은 그대로 두고 나머지는 전부 달리기 — 공격 한 번이 안 되는 나머지가 여기로 온다
        float run      = Mathf.Max(0f, cycle - count * length);
        float runStart = Mathf.Min(cycle, tail);
        float runEnd   = runStart + run;
        float start    = runEnd;

        // 사이 자세 — 달리기 양 끝, 달리기 그림 몇 장 길이. 이 동안은 서 있으므로 땅·대상도 멈춘다 (머리 주석)
        float beat      = runFrames > 0 ? settings.RunLoopSeconds / runFrames : 0f;
        float pose      = Mathf.Min(settings.PoseBeats * beat, run * 0.25f);
        float moveStart = runStart + pose;
        float moveEnd   = runEnd - pose;

        if (t < runStart)
        {
            // 처치 타를 타격 프레임부터 끝까지 — 끝나면 바로 달린다
            int frames = attacks[last].frames.Length;
            int frame  = Mathf.Min(frames - 1, Mathf.FloorToInt((hit + t) / length * frames));

            return new Result(EPhase.Recover, Mathf.Max(0, frame), last, t, last, runStart, runEnd, start, moveStart, moveEnd);
        }

        if (t < runEnd)
        {
            bool idle = t < moveStart || t >= moveEnd;

            return new Result(EPhase.Run, RunFrame(t - moveStart, runFrames, settings), 0, t, last, runStart, runEnd, start, moveStart, moveEnd, idle);
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

        return new Result(EPhase.Attack, attackFrame, combo, since, Mathf.Max(0, hitIndex) % n, runStart, runEnd, start, moveStart, moveEnd);
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
