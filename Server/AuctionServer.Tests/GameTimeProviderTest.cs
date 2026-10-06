using Microsoft.Extensions.Time.Testing;

namespace AuctionServer.Tests;

/// <summary>경매장 게임 시계 — "지금"만 오프셋을 따르고 타이머는 실제 시간으로 돈다.</summary>
public class GameTimeProviderTest
{
    private static readonly DateTime Start = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _real = new(new DateTimeOffset(Start));

    [Fact]
    public void 지금은_실제_시각에_오프셋을_더한_값이다()
    {
        var clock = new GameTimeProvider(_real) { Offset = TimeSpan.FromDays(1) };

        clock.GetUtcNow().ShouldBe(new DateTimeOffset(Start.AddDays(1)));
    }

    [Fact]
    public void 타이머는_오프셋과_무관하게_실제_시간으로_울린다()
    {
        // 오프셋을 타이머에 넣으면 청소가 하루치 밀려 한꺼번에 돌거나 아예 안 돈다.
        var clock = new GameTimeProvider(_real) { Offset = TimeSpan.FromDays(1) };
        var fired = 0;
        using var timer = clock.CreateTimer(_ => fired++, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

        _real.Advance(TimeSpan.FromSeconds(9));
        fired.ShouldBe(0);

        _real.Advance(TimeSpan.FromSeconds(1));
        fired.ShouldBe(1);
    }
}
