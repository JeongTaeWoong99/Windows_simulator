namespace WSGameServer;

/// <summary>
/// 게임 로직의 "지금". 실제 시각에 오프셋을 더한 값이다 — 시간 치트(<c>AdvanceTime</c>·<c>ResetTime</c>)가 오프셋만 움직인다.
/// 세션 감시·gRPC 기한·로그·ID 발급처럼 <b>실제 시간이 흘러야 하는 것은 이 시계를 쓰지 않는다</b> → Server/docs/치트.md 5장.
/// 오프셋은 서버 전체에 하나이고 재시작하면 0이다(저장하지 않는다). 로직·릴레이 스레드가 함께 읽어 Interlocked로 든다.
/// </summary>
public sealed class GameClock
{
    /// <summary>운영 인스턴스. 테스트는 자기 시계를 만들어 넘긴다 — 전역 오프셋을 병렬 테스트끼리 공유하지 않게.</summary>
    public static GameClock Instance { get; } = new(TimeProvider.System);

    private readonly TimeProvider _real;
    private long _offsetTicks;

    public GameClock(TimeProvider? real = null)
    {
        _real = real ?? TimeProvider.System;
    }

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public DateTime UtcNow => _real.GetUtcNow().UtcDateTime + Offset;

    /// <summary>오프셋을 더한다(누적). 뒤로 돌리기는 <see cref="Reset"/>뿐이다.</summary>
    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delta, TimeSpan.Zero);
        Interlocked.Add(ref _offsetTicks, delta.Ticks);
    }

    /// <summary>오프셋을 0으로. 되돌린 양(음수 또는 0)을 돌려준다 — 부르는 쪽이 슬롯 시각을 같은 만큼 민다.</summary>
    public TimeSpan Reset() => -TimeSpan.FromTicks(Interlocked.Exchange(ref _offsetTicks, 0));
}
