namespace WSGameServer;

/// <summary>
/// 검색 빈도 제한. 너무 빡빡하면 사람이 막히고, 회복 계산이 새면 매크로가 제한을 우회한다.
/// </summary>
public class TokenBucketTest
{
    private static readonly DateTime T0 = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    private static int TakeAll(TokenBucket bucket, DateTime now)
    {
        var taken = 0;
        while (bucket.TryTake(now))
        {
            taken++;
        }

        return taken;
    }

    [Fact]
    public void 처음에는_용량만큼_연달아_허용한다()
    {
        TakeAll(new TokenBucket(5, TimeSpan.FromSeconds(2), T0), T0).ShouldBe(5);
    }

    [Fact]
    public void 다_쓰면_회복_전까지_거절한다()
    {
        var bucket = new TokenBucket(5, TimeSpan.FromSeconds(2), T0);
        TakeAll(bucket, T0);

        bucket.TryTake(T0.AddSeconds(1.9)).ShouldBeFalse();
    }

    [Fact]
    public void 회복_간격마다_하나씩_채운다()
    {
        var bucket = new TokenBucket(5, TimeSpan.FromSeconds(2), T0);
        TakeAll(bucket, T0);

        // 5초 = 2초 두 번 + 1초 조각
        TakeAll(bucket, T0.AddSeconds(5)).ShouldBe(2);
    }

    [Fact]
    public void 남은_조각_시간은_다음_회복에_이어진다()
    {
        var bucket = new TokenBucket(5, TimeSpan.FromSeconds(2), T0);
        TakeAll(bucket, T0);
        TakeAll(bucket, T0.AddSeconds(5));

        // 5초까지 두 번 회복했고 1초 조각이 남았다 — 6초에 하나 더 찬다.
        bucket.TryTake(T0.AddSeconds(6)).ShouldBeTrue();
    }

    [Fact]
    public void 오래_쉬어도_용량을_넘게_쌓이지_않는다()
    {
        var bucket = new TokenBucket(5, TimeSpan.FromSeconds(2), T0);
        TakeAll(bucket, T0);

        TakeAll(bucket, T0.AddHours(1)).ShouldBe(5);
    }
}
