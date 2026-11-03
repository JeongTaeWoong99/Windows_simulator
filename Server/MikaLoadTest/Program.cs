using System.Diagnostics;
using MikaLoadTest;

// 서버 비용 측정용 부하 봇 (T-192). 서버는 따로 띄운다 — 측정할 DB를 git의 game.sqlite3와 섞지 않게 경로를 덮어서.
//   dotnet run -c Release --project Server/WSGameServer -- --Game:ListenPort=10070 --Game:DbPath=<임시>/load.sqlite3
//   dotnet run -c Release --project Server/MikaLoadTest -- --bots 100 --minutes 10 --heavy --port 10070
var options = LoadOptions.Parse(args);
Console.WriteLine($"봇 {options.Bots} · {(options.Heavy ? "heavy(슬롯 7)" : "light(슬롯 1)")} · {options.Minutes}분 · {options.Host}:{options.Port} · 서버 프로세스 {options.Process}");

var server = Process.GetProcessesByName(options.Process).FirstOrDefault();
if (server is null)
{
    Console.WriteLine($"서버 프로세스 '{options.Process}'를 찾지 못했다 — CPU·메모리는 빼고 잰다.");
}

// 핑은 접속보다 먼저 돌린다 — 다 붙은 뒤에 시작하면 먼저 붙은 봇이 무응답 판정(15초)에 끊긴다.
var bots = new List<Bot>();
using var pingTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
var pings = Task.Run(async () =>
{
    while (await pingTimer.WaitForNextTickAsync())
    {
        lock (bots)
        {
            foreach (var bot in bots)
            {
                bot.Ping();
            }
        }
    }
});

for (var i = 0; i < options.Bots; i++)
{
    var bot = new Bot($"{options.Prefix}-{i:D4}", options.Heavy);
    await bot.StartAsync(options.Host, options.Port);
    lock (bots)
    {
        bots.Add(bot);
    }

    if (i % 20 == 19)
    {
        await Task.Delay(1000);   // 초당 20명씩 — 한꺼번에 몰리면 로그인 폭주만 재게 된다
    }
}

// 접속·배치가 끝나기를 기다린 뒤부터 잰다 — 정상 상태(동접이 붙어 채취만 도는 때)의 비용이 목적이다.
await Task.Delay(TimeSpan.FromSeconds(options.WarmupSeconds));
var meter = new ServerMeter(server);
var bytesBefore   = bots.Sum(b => b.BytesReceived);
var packetsBefore = bots.Sum(b => b.PacketsReceived);
var gatherBefore  = bots.Sum(b => b.GatherResults);
var started       = Stopwatch.StartNew();

var samples = new List<ServerSample>();
while (started.Elapsed < TimeSpan.FromMinutes(options.Minutes))
{
    await Task.Delay(TimeSpan.FromSeconds(10));
    var s = meter.Sample();
    samples.Add(s);
    Console.WriteLine($"[{started.Elapsed:mm\\:ss}] CPU {s.CpuPercentOfOneCore,6:0.0}% (1코어 기준) · 메모리 {s.WorkingSetMb,6:0.0}MB · 로그인 {bots.Count(b => b.LoggedIn)} · 끊김 {bots.Count(b => b.Disconnected)}");
}

var seconds = started.Elapsed.TotalSeconds;
var bytes   = bots.Sum(b => b.BytesReceived) - bytesBefore;
var packets = bots.Sum(b => b.PacketsReceived) - packetsBefore;
var gathers = bots.Sum(b => b.GatherResults) - gatherBefore;

Console.WriteLine();
Console.WriteLine("=== 결과 ===");
Console.WriteLine($"봇 {options.Bots} · 로그인 {bots.Count(b => b.LoggedIn)} · 끊김 {bots.Count(b => b.Disconnected)} · 측정 {seconds:0}초");
if (samples.Count > 0)
{
    Console.WriteLine($"CPU 평균 {samples.Average(s => s.CpuPercentOfOneCore):0.0}% · 최대 {samples.Max(s => s.CpuPercentOfOneCore):0.0}% (1코어 = 100%)");
    Console.WriteLine($"메모리 평균 {samples.Average(s => s.WorkingSetMb):0.0}MB · 최대 {samples.Max(s => s.WorkingSetMb):0.0}MB");
}

Console.WriteLine($"서버→클라 {bytes / seconds / 1024:0.00} KB/s · 패킷 {packets / seconds:0.0}개/s · 채취 결과 {gathers / seconds:0.0}개/s");
Console.WriteLine($"한 달(30일·24시간 접속 유지) 송신 환산 {bytes / seconds * 86400 * 30 / 1024 / 1024 / 1024:0.00} GB");

foreach (var bot in bots)
{
    bot.Stop();
}

internal sealed record LoadOptions(int Bots, double Minutes, bool Heavy, string Host, int Port, string Process, string Prefix, int WarmupSeconds)
{
    public static LoadOptions Parse(string[] args)
    {
        string Get(string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        return new LoadOptions(
            int.Parse(Get("--bots", "100")),
            double.Parse(Get("--minutes", "5")),
            args.Contains("--heavy"),
            Get("--host", "127.0.0.1"),
            int.Parse(Get("--port", "10070")),
            Get("--process", "WSGameServer"),
            Get("--prefix", "load"),
            int.Parse(Get("--warmup", "30")));
    }
}

internal sealed record ServerSample(double CpuPercentOfOneCore, double WorkingSetMb);

/// <summary>서버 프로세스의 CPU(직전 표본 이후 · 1코어 = 100%)와 작업 집합 메모리.</summary>
internal sealed class ServerMeter(Process? process)
{
    private TimeSpan _lastCpu = process?.TotalProcessorTime ?? TimeSpan.Zero;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastWall = TimeSpan.Zero;

    public ServerSample Sample()
    {
        if (process is null)
        {
            return new ServerSample(0, 0);
        }

        process.Refresh();
        var cpu  = process.TotalProcessorTime;
        var wall = _clock.Elapsed;
        var pct  = (cpu - _lastCpu).TotalMilliseconds / (wall - _lastWall).TotalMilliseconds * 100;
        _lastCpu  = cpu;
        _lastWall = wall;
        return new ServerSample(pct, process.WorkingSet64 / 1024.0 / 1024.0);
    }
}
