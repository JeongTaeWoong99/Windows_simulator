using System.Text.Json;
using MikaProtocol;

namespace WSGameServer;

/// <summary>스팀 티켓 확인 결과. <see cref="Code"/>가 Ok일 때만 <see cref="SteamId"/>가 의미 있다.</summary>
public readonly record struct SteamTicketResult(EResultCode Code, ulong SteamId, string Reason)
{
    public static SteamTicketResult Ok(ulong steamId) => new(EResultCode.Ok, steamId, "");

    public static SteamTicketResult Fail(EResultCode code, string reason) => new(code, 0, reason);
}

/// <summary><c>ISteamUserAuth/AuthenticateUserTicket</c> 응답 본문을 로그인 판정으로 바꾼다 (T-207).</summary>
public static class SteamTicketResponse
{
    public static SteamTicketResult Parse(string json)
    {
        try
        {
            return ParseOrThrow(json);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            // 유저 티켓 탓이 아니라 스팀 쪽 응답이 이상한 것이다 — 인증 실패로 돌리면 클라가 새 티켓만 계속 만든다.
            return SteamTicketResult.Fail(EResultCode.SteamUnavailable, $"알아볼 수 없는 응답: {e.Message}");
        }
    }

    private static SteamTicketResult ParseOrThrow(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var response = doc.RootElement.GetProperty("response");

        if (response.TryGetProperty("error", out var error))
        {
            return SteamTicketResult.Fail(EResultCode.SteamAuthFailed, $"스팀 오류 {error}");
        }

        var parameters = response.GetProperty("params");
        var result = parameters.GetProperty("result").GetString();
        if (result != "OK")
        {
            return SteamTicketResult.Fail(EResultCode.SteamAuthFailed, $"결과 {result}");
        }

        // 패밀리 공유면 ownersteamid가 빌려준 사람이다. 계정은 실제로 하는 사람(steamid)으로 만든다(2026-11-21 결정).
        var steamId = ulong.Parse(parameters.GetProperty("steamid").GetString()!);

        // VAC 밴은 막지 않는다 — 다른 게임의 치트 이력이다. 우리가 건 퍼블리셔 밴만 거절한다.
        if (parameters.TryGetProperty("publisherbanned", out var banned) && banned.GetBoolean())
        {
            return SteamTicketResult.Fail(EResultCode.Banned, $"퍼블리셔 밴 SteamId={steamId}");
        }

        return SteamTicketResult.Ok(steamId);
    }
}
