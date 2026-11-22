using System.Net;
using MikaProtocol;
using WSGameServer;

namespace WSGameServer.Tests.Steam;

/// <summary>
/// 스팀 Web API 호출이 키·AppID·티켓·identity를 제대로 싣고, 닿지 못할 때 스팀 불가로 돌리는지 지킨다.
/// 틀리면 정상 티켓이 거절되거나, 스팀 장애가 유저 탓(인증 실패)으로 보인다. HTTP만 가짜로 바꾼다.
/// </summary>
public class SteamWebApiTest
{
    private static readonly SteamSettings Settings = new()
    {
        AppId     = 480,
        WebApiKey = "TESTKEY",
        Identity  = "wsgame",
        Timeout   = TimeSpan.FromSeconds(5),
    };

    private const string OkBody = """
        {"response":{"params":{"result":"OK","steamid":"76561197960287930","ownersteamid":"76561197960287930","vacbanned":false,"publisherbanned":false}}}
        """;

    [Fact]
    public async Task 티켓_확인_요청에_키_AppID_티켓_identity를_싣는다()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, OkBody);
        var api = new SteamWebApi(new HttpClient(handler), Settings);

        await api.AuthenticateTicketAsync("ABCD01", CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!;
        uri.AbsolutePath.ShouldBe("/ISteamUserAuth/AuthenticateUserTicket/v1/");
        uri.Query.ShouldBe("?key=TESTKEY&appid=480&ticket=ABCD01&identity=wsgame");
    }

    [Fact]
    public async Task 스팀이_오류_상태를_주면_스팀_불가다()
    {
        // 403 = 키가 틀렸다(서버 설정 문제) — 유저 티켓 탓으로 돌리지 않는다
        var api = new SteamWebApi(new HttpClient(new FakeHandler(HttpStatusCode.Forbidden, "Forbidden")), Settings);

        var result = await api.AuthenticateTicketAsync("ABCD01", CancellationToken.None);

        result.Code.ShouldBe(EResultCode.SteamUnavailable);
    }

    [Fact]
    public async Task 기한_안에_답이_없으면_스팀_불가다()
    {
        var settings = new SteamSettings { WebApiKey = "TESTKEY", Timeout = TimeSpan.FromMilliseconds(50) };
        var api = new SteamWebApi(new HttpClient(new HangingHandler()), settings);

        var result = await api.AuthenticateTicketAsync("ABCD01", CancellationToken.None);

        result.Code.ShouldBe(EResultCode.SteamUnavailable);
    }

    [Fact]
    public async Task 키가_없으면_스팀에_묻지_않고_스팀_불가다()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, OkBody);
        var api = new SteamWebApi(new HttpClient(handler), new SteamSettings { WebApiKey = "" });

        var result = await api.AuthenticateTicketAsync("ABCD01", CancellationToken.None);

        result.Code.ShouldBe(EResultCode.SteamUnavailable);
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task 스팀_이름을_SteamID로_가져온다()
    {
        const string body = """
            {"response":{"players":[{"steamid":"76561197960287930","personaname":"진우"}]}}
            """;
        var handler = new FakeHandler(HttpStatusCode.OK, body);
        var api = new SteamWebApi(new HttpClient(handler), Settings);

        var name = await api.GetPersonaNameAsync(76561197960287930UL, CancellationToken.None);

        name.ShouldBe("진우");
        handler.LastRequest!.RequestUri!.Query.ShouldBe("?key=TESTKEY&steamids=76561197960287930");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"response":{"players":[]}}""")]
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(HttpStatusCode.OK, "<html>")]
    public async Task 스팀_이름을_못_가져오면_null이다(HttpStatusCode status, string body)
    {
        // 이름은 로그인을 막을 이유가 아니다 — 호출자가 대신할 이름을 쓴다
        var api = new SteamWebApi(new HttpClient(new FakeHandler(status, body)), Settings);

        var name = await api.GetPersonaNameAsync(76561197960287930UL, CancellationToken.None);

        name.ShouldBeNull();
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    // 스팀이 응답하지 않는 상황 — 취소될 때까지 기다린다
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("도달하지 않는다");
        }
    }
}
