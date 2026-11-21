using MikaProtocol;
using WSGameServer;

namespace WSGameServer.Tests.Steam;

/// <summary>
/// 스팀 <c>AuthenticateUserTicket</c> 응답을 로그인 판정으로 바꾸는 규칙을 지킨다.
/// 틀리면 위조·만료 티켓이 통과하거나, 정상 유저가 이유 없이 튕긴다.
/// </summary>
public class SteamTicketResponseTest
{
    [Fact]
    public void 결과가_OK면_SteamID로_통과한다()
    {
        const string json = """
            {"response":{"params":{"result":"OK","steamid":"76561197960287930","ownersteamid":"76561197960287930","vacbanned":false,"publisherbanned":false}}}
            """;

        var result = SteamTicketResponse.Parse(json);

        result.Code.ShouldBe(EResultCode.Ok);
        result.SteamId.ShouldBe(76561197960287930UL);
    }

    [Fact]
    public void 오류_응답이면_티켓_인증_실패다()
    {
        // 틀리거나 만료된 티켓 — 스팀은 params 대신 error를 준다
        const string json = """
            {"response":{"error":{"errorcode":101,"errordesc":"Invalid ticket"}}}
            """;

        SteamTicketResponse.Parse(json).Code.ShouldBe(EResultCode.SteamAuthFailed);
    }

    [Fact]
    public void 퍼블리셔_밴이면_밴으로_거절한다()
    {
        const string json = """
            {"response":{"params":{"result":"OK","steamid":"76561197960287930","ownersteamid":"76561197960287930","vacbanned":false,"publisherbanned":true}}}
            """;

        SteamTicketResponse.Parse(json).Code.ShouldBe(EResultCode.Banned);
    }

    [Fact]
    public void 패밀리_공유면_소유자가_아니라_플레이어_SteamID로_통과한다()
    {
        // 2026-11-21 결정 — 빌려 쓰는 사람도 받는다. 계정은 실제로 하는 사람 것이어야 소유자와 섞이지 않는다.
        const string json = """
            {"response":{"params":{"result":"OK","steamid":"76561198000000002","ownersteamid":"76561198000000001","vacbanned":false,"publisherbanned":false}}}
            """;

        var result = SteamTicketResponse.Parse(json);

        result.Code.ShouldBe(EResultCode.Ok);
        result.SteamId.ShouldBe(76561198000000002UL);
    }

    [Fact]
    public void 결과가_OK가_아니면_티켓_인증_실패다()
    {
        const string json = """
            {"response":{"params":{"result":"Invalid","steamid":"76561197960287930","ownersteamid":"76561197960287930","vacbanned":false,"publisherbanned":false}}}
            """;

        SteamTicketResponse.Parse(json).Code.ShouldBe(EResultCode.SteamAuthFailed);
    }

    [Theory]
    [InlineData("<html>503 Service Unavailable</html>")]
    [InlineData("""{"response":{}}""")]
    [InlineData("""{"response":{"params":{"result":"OK","steamid":"abc"}}}""")]
    public void 알아볼_수_없는_응답이면_스팀_불가로_돌린다(string body)
    {
        // 유저 티켓 탓이 아니라 스팀 쪽 문제다 — 인증 실패로 돌리면 클라가 새 티켓만 계속 만든다
        SteamTicketResponse.Parse(body).Code.ShouldBe(EResultCode.SteamUnavailable);
    }
}
