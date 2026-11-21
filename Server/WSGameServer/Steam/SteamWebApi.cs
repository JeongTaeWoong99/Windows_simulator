using System.Text.Json;
using MikaProtocol;

namespace WSGameServer;

/// <summary>로그인에 쓰는 스팀 Web API. 테스트가 스팀 없이 로그인 흐름을 돌릴 수 있게 경계를 둔다.</summary>
public interface ISteamWebApi
{
    Task<SteamTicketResult> AuthenticateTicketAsync(string ticketHex, CancellationToken cancellationToken);

    /// <summary>스팀 표시 이름. 어떤 이유로든 못 가져오면 null — 이름 때문에 로그인을 막지 않는다.</summary>
    Task<string?> GetPersonaNameAsync(ulong steamId, CancellationToken cancellationToken);
}

/// <summary>스팀 Web API를 HTTP로 부른다 (T-207). 게임 서버 SDK(BeginAuthSession) 대신 쓴다 — 리눅스 Docker에 네이티브 라이브러리가 필요 없다.</summary>
public sealed class SteamWebApi : ISteamWebApi
{
    private readonly HttpClient _http;
    private readonly SteamSettings _settings;

    public SteamWebApi(HttpClient http, SteamSettings settings)
    {
        _http     = http;
        _settings = settings;
        _http.BaseAddress ??= new Uri(settings.BaseAddress);
    }

    public async Task<SteamTicketResult> AuthenticateTicketAsync(string ticketHex, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.WebApiKey))
        {
            return SteamTicketResult.Fail(EResultCode.SteamUnavailable, "WebApiKey 미설정");
        }

        var url = "ISteamUserAuth/AuthenticateUserTicket/v1/" +
                  $"?key={Uri.EscapeDataString(_settings.WebApiKey)}&appid={_settings.AppId}" +
                  $"&ticket={Uri.EscapeDataString(ticketHex)}&identity={Uri.EscapeDataString(_settings.Identity)}";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.Timeout);

        try
        {
            using var response = await _http.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // 403이면 키가 틀린 것이다 — 서버 설정 문제라 유저 티켓 탓(SteamAuthFailed)으로 돌리지 않는다.
                return SteamTicketResult.Fail(EResultCode.SteamUnavailable, $"HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return SteamTicketResponse.Parse(body);
        }
        catch (OperationCanceledException)
        {
            return SteamTicketResult.Fail(EResultCode.SteamUnavailable, $"기한 {_settings.Timeout.TotalSeconds}초 초과");
        }
        catch (HttpRequestException e)
        {
            return SteamTicketResult.Fail(EResultCode.SteamUnavailable, $"연결 실패: {e.Message}");
        }
    }

    public async Task<string?> GetPersonaNameAsync(ulong steamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.WebApiKey))
        {
            return null;
        }

        var url = $"ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(_settings.WebApiKey)}&steamids={steamId}";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.Timeout);

        try
        {
            using var response = await _http.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var players = doc.RootElement.GetProperty("response").GetProperty("players");
            if (players.GetArrayLength() == 0)
            {
                return null;
            }

            return players[0].GetProperty("personaname").GetString();
        }
        catch (Exception e) when (e is OperationCanceledException or HttpRequestException or JsonException
                                      or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
