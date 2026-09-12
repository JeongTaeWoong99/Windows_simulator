using System;
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
// ■ 표시는 값의 진실이 아니다
// 카운트다운은 연출이고 판정은 서버가 한다. 어긋나도 다음 슬롯 동기화가 기준점을 교정한다.
public static class WorkStationProgress
{
    // 작업량 단위는 "밀리초 × 천분율 속도"다. 1초 × 1.0배 = 1000ms × 1000 = 1,000,000 단위.
    // 남은 시간을 초로 되돌릴 때 이 값으로 나눈다.
    private const float UnitsPerSecondAtBaseSpeed = 1000f;

    // 칸이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다.
    // 'IsRunning'과 다르다. 그쪽은 속도까지 봐서 "카운트다운을 돌릴 수 있는가"를 뜻한다.
    public static bool IsAssigned(WorkStationSlotInfo slot)
        => slot.Industry != EIndustryType.None && slot.CharacterId != 0;

    // 배치돼 있고 속도가 0이 아니어서 카운트다운을 돌릴 수 있는가.
    public static bool IsRunning(WorkStationSlotInfo slot)
        => slot.CharacterId != 0 && slot.CurrentWorkSpeed > 0;

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

        return remainUnits / (float)slot.CurrentWorkSpeed / UnitsPerSecondAtBaseSpeed;
    }

    // 마지막 정산 이후 쌓인 작업량 중 이번 판정에 해당하는 몫을 구한다.
    // 판정 1회 비용으로 나눈 나머지라, 여러 판정이 밀려 있어도 현재 사이클만 남는다.
    private static long GetPendingUnits(WorkStationSlotInfo slot)
    {
        double elapsedMs   = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - slot.LastTickAtUnixMs);
        double accumulated = slot.ProgressUnits + elapsedMs * slot.CurrentWorkSpeed;

        return (long)(accumulated % slot.JudgeCostUnits);
    }
}
