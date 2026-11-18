namespace WSGameServer;

public readonly record struct RaidTicketState(int Count, DateOnly LastRechargeDate)
{
    public const int Capacity = 3;

    private static readonly TimeSpan KoreaStandardTimeOffset = TimeSpan.FromHours(9);

    public DateTimeOffset NextRechargeAt
        => new(LastRechargeDate.AddDays(1).ToDateTime(TimeOnly.MinValue), KoreaStandardTimeOffset);

    public static RaidTicketState FirstUnlock(DateTimeOffset now)
    {
        return new RaidTicketState(Capacity, KoreanDate(now));
    }

    public RaidTicketState Recharge(DateTimeOffset now)
    {
        DateOnly today = KoreanDate(now);
        int elapsedDays = today.DayNumber - LastRechargeDate.DayNumber;
        if (elapsedDays <= 0)
        {
            return this;
        }

        // 이미 최대치여도 오늘까지의 충전은 처리한 것으로 기록한다.
        return new RaidTicketState(Math.Min(Capacity, Count + elapsedDays), today);
    }

    public RaidTicketState ConsumeAfterVictory()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("소모할 레이드 티켓이 없습니다.");
        }

        return this with { Count = Count - 1 };
    }

    private static DateOnly KoreanDate(DateTimeOffset now)
        => DateOnly.FromDateTime(now.ToOffset(KoreaStandardTimeOffset).DateTime);
}
