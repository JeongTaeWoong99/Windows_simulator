using GameData;
using MikaProtocol;

namespace WSGameServer.User.WorkStation;

/// <summary>
/// 작업슬롯 한 칸. <b>산업 1개 + 캐릭터 1명</b>이 배치되면 독립적으로 채취를 돌린다.
///
/// <para>
/// 진행도는 <see cref="LastTickAt"/> <b>하나로만</b> 표현한다.
/// 방치형의 진행은 시각의 함수(<c>누적 = f(지금 - 마지막 정산 시각)</c>)이므로,
/// 이 값 하나면 <b>온라인·오프라인을 같은 계산으로 처리</b>할 수 있다.
/// 별도 카운터를 두면 온라인용/오프라인용 두 경로가 생기고, 둘이 어긋나는 순간 재화가 새거나 사라진다.
/// </para>
/// </summary>
public sealed class WorkStationSlot
{
    /// <summary>채취 판정 주기. 전 산업 공통 30초(게임기획코어 확정).</summary>
    public const int CycleSeconds = 30;

    /// <summary>판정 1회당 산출 개수. 현재 1개 고정이다(회당 산출 수치 미확정).</summary>
    public const int YieldPerJudge = 1;

    public WorkStationSlot(int slotIndex, ItemType industry, long characterId, DateTime lastTickAt)
    {
        SlotIndex   = slotIndex;
        Industry    = industry;
        CharacterId = characterId;
        LastTickAt  = lastTickAt;
    }

    public int SlotIndex { get; }

    /// <summary>지정된 산업. <see cref="ItemType.None"/>이면 미지정.</summary>
    public ItemType Industry { get; private set; }

    /// <summary>배치된 캐릭터. 0이면 비어 있다.</summary>
    public long CharacterId { get; private set; }

    /// <summary>마지막으로 정산이 끝난 시각(UTC). 진행도의 단일 원본.</summary>
    public DateTime LastTickAt { get; private set; }

    /// <summary>산업이 지정되고 캐릭터가 배치돼야 돌아간다. 둘 중 하나라도 비면 채취하지 않는다.</summary>
    public bool IsActive => Industry != ItemType.None && CharacterId != 0;

    /// <summary>
    /// 배치를 바꾼다. <b>호출 전에 반드시 정산을 끝내야 한다</b> —
    /// 바꾸기 전 구간은 이전 설정으로 계산돼야 하기 때문이다.
    /// </summary>
    public void Assign(ItemType industry, long characterId, DateTime now)
    {
        Industry    = industry;
        CharacterId = characterId;

        // 배치를 바꾸면 진행 중이던 30초 조각은 버린다.
        // 이월하면 "산업을 계속 갈아타며 조각을 모으는" 악용이 가능해진다.
        LastTickAt = now;
    }

    /// <summary>
    /// 지금까지 쌓인 판정 횟수를 꺼내고 <see cref="LastTickAt"/>을 그만큼 전진시킨다.
    /// <b>남은 자투리 시간은 이월된다</b>(29초 경과 후 정산해도 손해가 없다).
    /// </summary>
    /// <returns>정산할 판정 횟수. 비활성 슬롯이거나 주기가 안 찼으면 0.</returns>
    public int ConsumeJudgeCount(DateTime now)
    {
        if (!IsActive)
        {
            // 비어 있는 동안 시간이 쌓이면 캐릭터를 꽂는 순간 한꺼번에 터진다. 시계만 따라가게 둔다.
            LastTickAt = now;
            return 0;
        }

        var elapsed = now - LastTickAt;
        if (elapsed <= TimeSpan.Zero)
            return 0;

        var judgeCount = (int)(elapsed.TotalSeconds / CycleSeconds);
        if (judgeCount <= 0)
            return 0;

        LastTickAt = LastTickAt.AddSeconds((double)judgeCount * CycleSeconds);
        return judgeCount;
    }

    /// <summary>다음 판정까지 남은 시간. 클라이언트 카운트다운을 맞출 때 쓴다.</summary>
    public TimeSpan TimeUntilNextJudge(DateTime now)
    {
        var remain = LastTickAt.AddSeconds(CycleSeconds) - now;
        return remain > TimeSpan.Zero ? remain : TimeSpan.Zero;
    }

    public WorkStationSlotInfo ToInfo() => new()
    {
        SlotIndex      = SlotIndex,
        Industry       = (byte)Industry,
        CharacterId    = CharacterId,
        LastTickAtUnix = new DateTimeOffset(LastTickAt, TimeSpan.Zero).ToUnixTimeSeconds(),
    };
}
