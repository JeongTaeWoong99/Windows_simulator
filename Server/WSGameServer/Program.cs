namespace WSGameServer;

class Program
{
    private static async Task Main(string[] args)
    {
        var settings = GameSettings.Load(AppContext.BaseDirectory, args);

        var gameServer = new GameServer(settings);
        gameServer.Initialize();
        await gameServer.Run();

        ServerLog.Info("서버", $"{settings.ListenPort} 포트에서 대기 중...");
        ServerLog.Info("서버", "종료하려면 엔터를 누르세요.");
        await WaitForShutdown();
    }

    // 엔터 · Ctrl+C · 종료 신호(docker stop) 중 먼저 오는 것을 기다린다.
    // stdin이 닫혀 있으면(Docker·백그라운드 실행) ReadLine이 바로 null이라 엔터는 기다리지 않는다 — 예전엔 여기서 바로 꺼졌다.
    private static Task WaitForShutdown()
    {
        var stop = new TaskCompletionSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.TrySetResult();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.TrySetResult();

        _ = Task.Run(() =>
        {
            if (Console.ReadLine() != null)
            {
                stop.TrySetResult();
            }
        });

        return stop.Task;
    }
}
