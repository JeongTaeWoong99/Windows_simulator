using Microsoft.Extensions.Configuration;

namespace WSGameServer;

// 게임 서버 설정 — appsettings.json의 Game 절. 기본값은 개발용이고, 원격(Docker)은 환경 변수(Game__ListenPort)로,
// 같은 PC에서 개발 서버를 하나 더 띄울 땐 명령줄(--Game:ListenPort=30331)로 덮는다. 원격 전용 설정 파일은 두지 않는다.
public sealed class GameSettings
{
    public int ListenPort { get; set; } = 30329;

    /// <summary>파일명이면 저장소의 <c>Server/Shared/</c>에서 찾고, 절대 경로면 그대로 쓴다(Docker 볼륨).</summary>
    public string DbPath { get; set; } = "game.sqlite3";

    /// <summary>치트를 쓸 수 있는 최소 admin_level. 개발 0(모든 계정) · 원격 1.</summary>
    public int CheatAdminLevel { get; set; }

    public AuctionClientSettings Auction { get; set; } = new();

    /// <summary><paramref name="directory"/>의 appsettings.json → 환경 변수 → 명령줄 순으로 덮어 읽는다.</summary>
    public static GameSettings Load(string directory, string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        return From(configuration);
    }

    public static GameSettings From(IConfiguration configuration)
    {
        return configuration.GetSection("Game").Get<GameSettings>() ?? new GameSettings();
    }
}
