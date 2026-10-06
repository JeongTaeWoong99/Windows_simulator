namespace AuctionServer;

/// <summary>
/// 경매장의 "지금" — 실제 시각에 메인 서버의 게임 시계 오프셋을 더한다(시간 치트). 만료·예약 해제·전일 평균이 이 시각을 따른다.
/// 타이머는 안쪽 시계 그대로다 — 청소 주기는 실제 시간으로 돈다. 오프셋은 저장하지 않는다(재시작하면 0, 메인이 다시 맞춘다).
/// </summary>
public sealed class GameTimeProvider(TimeProvider inner) : TimeProvider
{
    private long _offsetTicks;

    public TimeSpan Offset
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));
        set => Interlocked.Exchange(ref _offsetTicks, value.Ticks);
    }

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + Offset;

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override long GetTimestamp() => inner.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => inner.CreateTimer(callback, state, dueTime, period);
}
