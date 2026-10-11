using Microsoft.Extensions.Configuration;

namespace WSGameServer;

/// <summary>
/// 게임 서버 설정(<see cref="GameSettings"/>)이 개발 기본값을 갖고, <c>Game</c> 절·명령줄이 그 값을 덮는지 지킨다.
/// 틀리면 원격(Docker)이 개발 포트·개발 DB 경로로 뜨거나, 개발 서버가 원격 경매장 주소로 붙는다.
/// </summary>
public class GameSettingsTest : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ws-settings-{Guid.NewGuid():N}");

    public GameSettingsTest()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void 설정이_없으면_게임_포트는_개발용_10050이다()
    {
        // 원격(Docker)은 30329 — 같은 PC에서 둘이 함께 떠도 겹치지 않는다.
        Empty().ListenPort.ShouldBe(10050);
    }

    [Fact]
    public void 설정이_없으면_DB는_저장소의_game_sqlite3다()
    {
        Empty().DbPath.ShouldBe("game.sqlite3");
    }

    [Fact]
    public void 설정이_없으면_경매장은_이_PC의_개발_경매장_10060이다()
    {
        Empty().Auction.Address.ShouldBe("http://127.0.0.1:10060");
    }

    [Fact]
    public void 설정이_없으면_치트는_모든_계정이_쓴다()
    {
        Empty().CheatAdminLevel.ShouldBe(0);
    }

    [Fact]
    public void Game_절의_값이_기본값을_덮는다()
    {
        var settings = From(new() { ["Game:ListenPort"] = "30329" });

        settings.ListenPort.ShouldBe(30329);
    }

    [Fact]
    public void 경매장_주소는_Game_Auction_절에서_읽는다()
    {
        // compose 안에서는 컨테이너 이름으로 붙는다 — 127.0.0.1은 게임 컨테이너 자신이다.
        var settings = From(new() { ["Game:Auction:Address"] = "http://auction:30330" });

        settings.Auction.Address.ShouldBe("http://auction:30330");
    }

    [Fact]
    public void 설정_파일의_값을_읽는다()
    {
        File.WriteAllText(Path.Combine(_dir, "appsettings.json"), """{ "Game": { "CheatAdminLevel": 1 } }""");

        GameSettings.Load(_dir, []).CheatAdminLevel.ShouldBe(1);
    }

    [Fact]
    public void 명령줄_인자가_설정_파일을_덮는다()
    {
        // 같은 PC에서 개발 서버를 하나 더 띄우는 경우
        File.WriteAllText(Path.Combine(_dir, "appsettings.json"), """{ "Game": { "ListenPort": 10050 } }""");

        GameSettings.Load(_dir, ["--Game:ListenPort=10051"]).ListenPort.ShouldBe(10051);
    }

    private static GameSettings Empty()
    {
        return From(new());
    }

    private static GameSettings From(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return GameSettings.From(configuration);
    }
}
