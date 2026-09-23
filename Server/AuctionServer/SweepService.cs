namespace AuctionServer;

/// <summary>만료·예약 타임아웃 청소를 주기적으로 쓰기 채널에 넣는다. 청소도 쓰기라 같은 줄을 선다.</summary>
public sealed class SweepService(AuctionEngine engine, AuctionOptions options, TimeProvider time, ILogger<SweepService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SweepInterval, time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await engine.SweepAsync();
            }
            catch (Exception e)
            {
                // 청소 한 번이 실패해도 다음 주기에 다시 한다 — 서비스를 멈추지 않는다.
                log.LogError(e, "청소 실패");
            }
        }
    }
}
