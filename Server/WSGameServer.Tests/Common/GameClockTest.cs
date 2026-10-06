using Microsoft.Extensions.Time.Testing;

namespace WSGameServer;

/// <summary>게임 시계 — 실제 시각 + 오프셋. 여기가 새면 시간 치트가 만료·삭제 판정과 어긋난다.</summary>
public class GameClockTest
{
    private static readonly DateTime T0 = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _real  = new(new DateTimeOffset(T0));
    private readonly GameClock        _clock;

    public GameClockTest() => _clock = new GameClock(_real);

    [Fact]
    public void 오프셋이_없으면_실제_시각과_같다()
    {
        _clock.UtcNow.ShouldBe(T0);
    }

    [Fact]
    public void 넘긴_시간은_누적되고_실제_시간도_함께_흐른다()
    {
        _clock.Advance(TimeSpan.FromHours(1));
        _clock.Advance(TimeSpan.FromDays(1));
        _real.Advance(TimeSpan.FromSeconds(5));

        _clock.Offset.ShouldBe(TimeSpan.FromHours(25));
        _clock.UtcNow.ShouldBe(T0.AddHours(25).AddSeconds(5));
    }

    [Fact]
    public void 초기화하면_되돌린_양을_음수로_돌려주고_오프셋은_0이다()
    {
        _clock.Advance(TimeSpan.FromDays(7));

        _clock.Reset().ShouldBe(TimeSpan.FromDays(-7));
        _clock.UtcNow.ShouldBe(T0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 뒤로_넘기기는_없다(long seconds)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _clock.Advance(TimeSpan.FromSeconds(seconds)));
    }
}
