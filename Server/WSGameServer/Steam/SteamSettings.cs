namespace WSGameServer;

// 스팀 로그인 설정 — appsettings.json의 Game:Steam 절 (T-207). 키는 저장소에 넣지 않는다 — 환경 변수 Game__Steam__WebApiKey로 준다.
public sealed class SteamSettings
{
    /// <summary>우리 게임의 AppID. 등록비를 내기 전까지는 테스트용 480(Spacewar).</summary>
    public uint AppId { get; set; } = 480;

    /// <summary>퍼블리셔 Web API 키. 비어 있으면 스팀 로그인은 전부 SteamUnavailable로 거절된다.</summary>
    public string WebApiKey { get; set; } = "";

    /// <summary>클라 <c>GetAuthTicketForWebApi(identity)</c>와 같은 문자열이어야 티켓이 통과한다.</summary>
    public string Identity { get; set; } = "wsgame";

    /// <summary>스팀 호출 한 번의 기한. 넘으면 SteamUnavailable — 로그인이 끝없이 매달리지 않게.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public string BaseAddress { get; set; } = "https://api.steampowered.com/";
}
