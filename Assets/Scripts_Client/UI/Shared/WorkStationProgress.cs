using System;
using GameData;
using MikaProtocol;
using UnityEngine;

// 작업슬롯 스냅샷 하나를 읽어 "지금 돌고 있는가 · 얼마나 찼는가 · 몇 초 남았는가"를 내주는 계산표.
// 서버는 주기를 보내지 않으므로 클라가 서버 식을 그대로 재현한다 — 근거는 '패킷 레퍼런스.md'.
//
// ■ 왜 Presenter 밖에 있나
// 슬롯 목록('WorkStationListPresenter')과 상주 위젯('WidgetPresenter')이 같은 값을 그린다.
// 한쪽에 두고 복사하면 서버 판정식이 두 벌이 되어 한쪽만 고쳐진다.
// 'RarityPalette'·'ResultMessages'와 같은 부류 — 캔버스를 가로지르는 표시용 변환이라 여기 둔다.
//
// ■ 판정 1회에는 최소 시간이 있다 (2026-10-09 · T-102 · 이슈 #60)
//   실효 주기 = max('Constants.MinCycleMs', 판정 비용 ÷ 속도). 연출 한 바퀴(여운 → 숨 → 달리기 → 숨 → 공격 1회)를 보장하는 값이다.
//   속도로 바꾸면 **상한 = 판정 비용 ÷ 최소 시간(ms)** (정수 나눗셈 버림) — 서버도 같은 식으로 'CurrentWorkSpeed'를 자른다.
//   서버가 이미 자른 값을 보내면 여기서 한 번 더 잘라도 그대로다(같은 식이라 멱등).
//   ※ 최소 시간이 연출 한 바퀴보다 짧으면 'SlotStageSettings.RequiredCycleSeconds'가 경고한다.
//
// ■ 표시는 값의 진실이 아니다
// 카운트다운은 연출이고 판정은 서버가 한다. 어긋나도 다음 슬롯 동기화가 기준점을 교정한다.
public static class WorkStationProgress
{
    // 작업량 단위는 "밀리초 × 천분율 속도"다. 작업량 ÷ 속도 = 밀리초라, 초로 되돌릴 때 이 값으로 나눈다.
    // ※ 속도 스케일('Constants.WorkSpeedScale')이 아니다 — 속도로 나누면 스케일은 이미 지워진다(T-085).
    private const float MillisecondsPerSecond = 1000f;

    // 칸이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다.
    // 'IsRunning'과 다르다. 그쪽은 속도까지 봐서 "카운트다운을 돌릴 수 있는가"를 뜻한다.
    public static bool IsAssigned(WorkStationSlotInfo slot)
        => slot.Industry != EIndustryType.None && slot.CharacterId != 0;

    // 배치돼 있고 속도가 0이 아니어서 카운트다운을 돌릴 수 있는가.
    public static bool IsRunning(WorkStationSlotInfo slot)
        => slot.CharacterId != 0 && slot.CurrentWorkSpeed > 0;

    // 판정 1회 최소 시간(초) — 엑셀 'Constants.MinCycleMs' (효율 계산 표시에서 호출)
    public static float MinCycleSeconds => Constants.MinCycleMs / MillisecondsPerSecond;

    // 이 슬롯의 속도 상한(천분율) — 이보다 빠르면 주기가 최소 시간보다 짧아진다.
    public static int GetSpeedCap(WorkStationSlotInfo slot)
        => (int)Math.Min(int.MaxValue, Math.Max(1L, slot.JudgeCostUnits / Math.Max(1L, Constants.MinCycleMs)));

    // 실제로 쓰는 속도(천분율) — 받은 속도를 상한으로 자른 값. 진행도·남은 초·주기가 전부 이 값으로 돈다.
    public static int GetEffectiveSpeed(WorkStationSlotInfo slot)
        => Math.Min(slot.CurrentWorkSpeed, GetSpeedCap(slot));

    // 속도가 상한에 닿아 주기가 최소 시간에 붙어 있는가 (효율 계산·슬롯 줄 표시에서 호출)
    public static bool IsAtMinCycle(WorkStationSlotInfo slot)
        => slot.CurrentWorkSpeed > 0 && slot.CurrentWorkSpeed >= GetSpeedCap(slot);

    // 판정 진행도 0~1 (각 Presenter의 Update에서 호출)
    public static float CalculateProgress(WorkStationSlotInfo slot)
    {
        return Mathf.Clamp01((float)GetPendingUnits(slot) / slot.JudgeCostUnits);
    }

    // 다음 수확까지 남은 초 (각 Presenter의 Update에서 호출)
    public static float CalculateRemainSeconds(WorkStationSlotInfo slot)
    {
        long remainUnits = slot.JudgeCostUnits - GetPendingUnits(slot);

        // IsRunning이 CurrentWorkSpeed > 0을 보장하지만, 계산식만 떼어 봐도 안전하도록 가드를 남긴다.
        if (slot.CurrentWorkSpeed <= 0)
        {
            return 0f;
        }

        return remainUnits / (float)GetEffectiveSpeed(slot) / MillisecondsPerSecond;
    }

    // 판정 1회에 걸리는 초 = 실효 주기 (작업슬롯 선택 화면의 효율 계산에서 호출)
    // ※ 남은 초와 같은 식에서 진행도만 뺀 것이다 — 서버 판정식을 여기 한 곳에만 둔다.
    public static float CalculateCycleSeconds(WorkStationSlotInfo slot)
    {
        if (slot.CurrentWorkSpeed <= 0)
        {
            return 0f;
        }

        return slot.JudgeCostUnits / (float)GetEffectiveSpeed(slot) / MillisecondsPerSecond;
    }

    // 마지막 정산 이후 쌓인 작업량 중 이번 판정에 해당하는 몫을 구한다.
    // 판정 1회 비용으로 나눈 나머지라, 여러 판정이 밀려 있어도 현재 사이클만 남는다.
    private static long GetPendingUnits(WorkStationSlotInfo slot)
    {
        double elapsedMs   = (ServerClock.UtcNowUnixMs - slot.LastTickAtUnixMs);
        double accumulated = slot.ProgressUnits + elapsedMs * GetEffectiveSpeed(slot);

        return (long)(accumulated % slot.JudgeCostUnits);
    }
}
