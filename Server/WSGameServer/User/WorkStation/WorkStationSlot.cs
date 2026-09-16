using GameData;
using MikaProtocol;

namespace WSGameServer;

// 작업슬롯 한 칸. 산업 1개 + 캐릭터 1명이 배치되면 독립적으로 채취를 돌린다.
// 진행도는 세션 지역 상태(저장 안 함)이며 단위는 시간이 아니라 작업량이다 → Server/docs/채취-정산.md
public sealed class WorkStationSlot
{
    /// <summary>기준 채취 주기(초). 속도 1.0배일 때 판정 1회에 걸리는 시간.</summary>
    public const int BaseCycleSeconds = 30;

    /// <summary>작업속도의 정수 단위. 1000 = 1.0배(천분율). "작업속도"라고 적힌 정수는 전부 이 단위다.</summary>
    public const int WorkSpeedScale = 1000;

    /// <summary>기준 작업속도(1.0배). 보정이 하나도 없을 때의 값.</summary>
    public const int DefaultWorkSpeed = WorkSpeedScale;

    // 0이면 TimeUntilNextJudge에서 0으로 나눈다. "채취 정지"는 배치를 비우는 것으로 표현한다.
    public const int MinWorkSpeed = 1;

    // Lv1 판정 비용 = 기준주기(초) × 1000ms × 1000천분율. 경과ms × 속도천분율과 차원이 같아 나눗셈 없이 누적한다.
    // 실제 판정은 인스턴스의 JudgeCostUnits(레벨별 RequiredScore)를 쓴다 → Server/docs/채취-정산.md 2장
    public const long JudgeCost = (long)BaseCycleSeconds * 1000 * WorkSpeedScale;

    /// <summary>판정 1회당 산출 개수. 현재 1개 고정(회당 산출 수치 미확정).</summary>
    public const int YieldPerJudge = 1;

    /// <summary>기본 산업 레벨. 게임 시작 시 열려 있는 레벨(산업레벨.md 3.3).</summary>
    public const int DefaultIndustryLevel = 1;

    public WorkStationSlot(
        int slotIndex,
        IndustryType industry,
        long characterId,
        DateTime startedAt,
        int currentWorkSpeed = DefaultWorkSpeed,
        int industryLevel = DefaultIndustryLevel,
        long judgeCostUnits = JudgeCost)
    {
        SlotIndex        = slotIndex;
        Industry         = industry;
        IndustryLevel    = industryLevel;
        JudgeCostUnits   = judgeCostUnits;
        CharacterId      = characterId;
        LastTickAt       = startedAt;
        CurrentWorkSpeed = Math.Max(MinWorkSpeed, currentWorkSpeed);
    }

    public int SlotIndex { get; }

    /// <summary>지정된 산업. None이면 미지정. DB에 저장된다.</summary>
    public IndustryType Industry { get; private set; }

    /// <summary>지정된 산업 레벨. ⚠️ 저장·패킷·해금 검증 미구현(T-017)이라 지금은 항상 DefaultIndustryLevel.</summary>
    public int IndustryLevel { get; private set; }

    /// <summary>판정 1회에 필요한 작업량(밀리초×천분율). (산업, 레벨)의 값이며 Assign이 레벨과 함께 바꾼다.</summary>
    public long JudgeCostUnits { get; private set; }

    /// <summary>배치된 캐릭터. 0이면 비어 있다. DB에 저장된다.</summary>
    public long CharacterId { get; private set; }

    /// <summary>마지막 정산 시각(UTC). 세션 지역 상태. 정산 단위가 밀리초라 현재보다 1ms 미만 뒤처질 수 있다.</summary>
    public DateTime LastTickAt { get; private set; }

    /// <summary>판정에 못 미친 남은 작업량(0 이상 JudgeCostUnits 미만). 세션 지역 상태. 초가 아니라 작업량으로 든다.</summary>
    public long ProgressUnits { get; private set; }

    /// <summary>현재 작업속도(천분율). 보정을 모두 적용한 확정값이며 접속마다 재계산한다. 테이블 기본값은 입력, 이 값은 결과.</summary>
    public int CurrentWorkSpeed { get; private set; }

    /// <summary>이 슬롯의 실효 채취 주기. 표시·로그용이며 계산에는 쓰지 않는다.</summary>
    public TimeSpan EffectiveCycle
        => TimeSpan.FromMilliseconds((double)JudgeCostUnits / CurrentWorkSpeed);

    /// <summary>산업이 지정되고 캐릭터가 배치돼야 돌아간다.</summary>
    public bool IsActive => Industry != IndustryType.None && CharacterId != 0;

    /// <summary>배치를 바꾼다. ⚠️ 호출 전에 반드시 정산한다 — 바꾸기 전 구간은 이전 설정으로 계산돼야 한다.</summary>
    public void Assign(
        IndustryType industry,
        long characterId,
        DateTime now,
        int industryLevel = DefaultIndustryLevel,
        long judgeCostUnits = JudgeCost)
    {
        Industry       = industry;
        IndustryLevel  = industryLevel;
        JudgeCostUnits = judgeCostUnits;
        CharacterId    = characterId;

        // 진행 중이던 조각은 버린다. 이월하면 산업을 갈아타며 조각을 모으는 악용이 된다.
        LastTickAt    = now;
        ProgressUnits = 0;
    }

    /// <summary>채취 속도를 바꾼다. ⚠️ 호출 전에 반드시 정산한다 — 안 그러면 이전 구간까지 새 속도로 소급된다.</summary>
    /// <returns>값이 실제로 바뀌었으면 true.</returns>
    public bool ApplyWorkSpeed(int currentWorkSpeed)
    {
        var clamped = Math.Max(MinWorkSpeed, currentWorkSpeed);
        if (clamped == CurrentWorkSpeed)
        {
            return false;
        }

        CurrentWorkSpeed = clamped;
        return true;
    }

    /// <summary>지난 구간의 작업량을 누적하고 완성된 판정 횟수를 꺼낸다. 못 채운 자투리는 ProgressUnits에 이월된다.</summary>
    /// <returns>정산할 판정 횟수. 비활성 슬롯이거나 아직 1회를 못 채웠으면 0.</returns>
    public int ConsumeJudgeCount(DateTime now)
    {
        if (!IsActive)
        {
            // 비어 있는 동안 시간이 쌓이면 캐릭터를 꽂는 순간 한꺼번에 터진다. 시계만 따라가게 둔다.
            LastTickAt = now;
            return 0;
        }

        var elapsedMs = (long)(now - LastTickAt).TotalMilliseconds;
        if (elapsedMs <= 0)
        {
            return 0;
        }

        // 시계는 now가 아니라 정산한 만큼만 전진한다. now까지 밀면 버림된 1ms 미만이 매 틱 사라진다(이슈 #11).
        // 한 구간의 속도는 항상 하나다 — 속도가 바뀌는 지점에서 호출자가 먼저 정산하기 때문.
        LastTickAt     = LastTickAt.AddMilliseconds(elapsedMs);
        ProgressUnits += elapsedMs * CurrentWorkSpeed;

        var judgeCount = ProgressUnits / JudgeCostUnits;
        if (judgeCount <= 0)
        {
            return 0;
        }

        ProgressUnits -= judgeCount * JudgeCostUnits;
        return (int)judgeCount;
    }

    /// <summary>다음 판정까지 남은 시간(클라이언트 카운트다운용). 비활성 슬롯은 Zero.</summary>
    public TimeSpan TimeUntilNextJudge(DateTime now)
    {
        if (!IsActive)
        {
            return TimeSpan.Zero;
        }

        // ProgressUnits는 마지막 정산 시점의 값이라, 그 뒤로 흐른 시간을 얹어야 지금 값이 된다.
        var elapsedMs = (long)(now - LastTickAt).TotalMilliseconds;
        if (elapsedMs < 0)
        {
            elapsedMs = 0;
        }

        var remain = JudgeCostUnits - (ProgressUnits + elapsedMs * CurrentWorkSpeed);
        return remain > 0
            ? TimeSpan.FromMilliseconds((double)remain / CurrentWorkSpeed)
            : TimeSpan.Zero;
    }

    public WorkStationSlotInfo ToInfo() => new()
    {
        SlotIndex        = SlotIndex,
        Industry         = (EIndustryType)Industry,
        IndustryLevel    = (byte)IndustryLevel,
        CharacterId      = CharacterId,
        // 밀리초까지 보낸다. 초로 자르면 ProgressUnits와 어긋나 카운트다운이 먼저 0에 닿는다(이슈 #11).
        LastTickAtUnixMs = new DateTimeOffset(LastTickAt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        ProgressUnits    = ProgressUnits,
        CurrentWorkSpeed = CurrentWorkSpeed,
        JudgeCostUnits   = JudgeCostUnits,
    };
}
