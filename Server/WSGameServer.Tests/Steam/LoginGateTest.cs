using MikaNetwork;
using MikaProtocol;
using WSGameServer;

namespace WSGameServer.Tests.Steam;

/// <summary>
/// 로그인 요청을 스팀 티켓 / 개발용 Id로 갈라 어느 계정(pid)으로 들어갈지 정하는 규칙을 지킨다.
/// 틀리면 남의 계정으로 들어가지거나(아무 Id·남의 SteamID), 정상 스팀 유저가 들어오지 못한다.
/// 스팀(ISteamWebApi)과 소켓(ISession)만 가짜로 바꾼다.
/// </summary>
public class LoginGateTest
{
    private readonly FakeSteam _steam = new();
    private readonly List<(string Pid, string Nickname)> _created = new();
    private readonly List<EResultCode> _rejected = new();
    private readonly Mock<ISession> _session = new();

    public LoginGateTest()
    {
        _session.SetupGet(s => s.SessionId).Returns(7);
        _session.SetupGet(s => s.IsConnected).Returns(true);
    }

    private LoginGate CreateGate(bool allowDevLogin = false) => new(
        _steam,
        allowDevLogin,
        post: job => job(),
        createUser: (_, pid, nickname) => _created.Add((pid, nickname)),
        reject: (_, code) => _rejected.Add(code));

    [Fact]
    public async Task 스팀_티켓이_통과하면_steam_접두_SteamID_계정으로_들어간다()
    {
        _steam.Ticket = SteamTicketResult.Ok(76561197960287930UL);
        _steam.Name = "진우";

        await CreateGate().HandleAsync(_session.Object, new C_LoginRequest { SteamTicket = "ABCD01" });

        _created.ShouldBe(new[] { ("steam:76561197960287930", "진우") });
    }

    [Theory]
    [InlineData(EResultCode.SteamAuthFailed)]
    [InlineData(EResultCode.SteamUnavailable)]
    [InlineData(EResultCode.Banned)]
    public async Task 스팀이_거절하면_그_결과로_응답하고_들어가지_않는다(EResultCode code)
    {
        _steam.Ticket = SteamTicketResult.Fail(code, "테스트");

        await CreateGate().HandleAsync(_session.Object, new C_LoginRequest { SteamTicket = "ABCD01" });

        _rejected.ShouldBe(new[] { code });
        _created.ShouldBeEmpty();
    }

    [Fact]
    public async Task 스팀_이름을_못_가져와도_SteamID를_이름으로_들어간다()
    {
        _steam.Ticket = SteamTicketResult.Ok(76561197960287930UL);
        _steam.Name = null;

        await CreateGate().HandleAsync(_session.Object, new C_LoginRequest { SteamTicket = "ABCD01" });

        _created.ShouldBe(new[] { ("steam:76561197960287930", "76561197960287930") });
    }

    [Fact]
    public async Task 스팀을_기다리는_동안_연결이_끊기면_들어가지_않는다()
    {
        // 끊긴 세션에 User를 만들면 아무도 정리하지 않는 유령이 남는다
        _steam.Ticket = SteamTicketResult.Ok(76561197960287930UL);
        _session.SetupGet(s => s.IsConnected).Returns(false);

        await CreateGate().HandleAsync(_session.Object, new C_LoginRequest { SteamTicket = "ABCD01" });

        _created.ShouldBeEmpty();
    }

    [Fact]
    public async Task 개발용_로그인이_꺼진_서버는_티켓_없는_로그인을_거절한다()
    {
        await CreateGate(allowDevLogin: false).HandleAsync(_session.Object, new C_LoginRequest { Id = "tester" });

        _rejected.ShouldBe(new[] { EResultCode.SteamAuthFailed });
        _created.ShouldBeEmpty();
    }

    [Fact]
    public async Task 개발용_로그인이_켜진_서버는_Id로_들어간다()
    {
        await CreateGate(allowDevLogin: true).HandleAsync(_session.Object, new C_LoginRequest { Id = "tester" });

        _created.ShouldBe(new[] { ("tester", "tester") });
    }

    [Fact]
    public async Task 개발용_로그인으로_스팀_계정을_흉내낼_수_없다()
    {
        // 개발용이 켜진 원격 서버에서도 남의 스팀 계정 pid를 직접 적어 들어가면 안 된다
        await CreateGate(allowDevLogin: true).HandleAsync(_session.Object, new C_LoginRequest { Id = "steam:76561197960287930" });

        _rejected.ShouldBe(new[] { EResultCode.SteamAuthFailed });
        _created.ShouldBeEmpty();
    }

    [Fact(Timeout = 5000)]   // 막는 데 실패하면 두 번째 요청이 끝나지 않는 응답을 기다리며 멈춘다
    public async Task 스팀을_기다리는_중에_다시_보낸_로그인은_거절한다()
    {
        // 클라가 재시도를 겹쳐 보내도 같은 세션에 User가 둘 생기면 안 된다
        var pending = new TaskCompletionSource<SteamTicketResult>();
        _steam.Pending = pending.Task;
        var gate = CreateGate();

        var first = gate.HandleAsync(_session.Object, new C_LoginRequest { SteamTicket = "ABCD01" });
        await gate.HandleAsync(_session.Object, new C_LoginRequest { SteamTicket = "ABCD02" });
        pending.SetResult(SteamTicketResult.Ok(76561197960287930UL));
        await first;

        _rejected.ShouldBe(new[] { EResultCode.AlreadyLoggedIn });
        _created.Count.ShouldBe(1);
    }

    private sealed class FakeSteam : ISteamWebApi
    {
        public SteamTicketResult Ticket { get; set; }
        public string? Name { get; set; }

        /// <summary>정해 두면 스팀 응답이 이 작업이 끝날 때까지 오지 않는다.</summary>
        public Task<SteamTicketResult>? Pending { get; set; }

        public Task<SteamTicketResult> AuthenticateTicketAsync(string ticketHex, CancellationToken cancellationToken)
            => Pending ?? Task.FromResult(Ticket);

        public Task<string?> GetPersonaNameAsync(ulong steamId, CancellationToken cancellationToken)
            => Task.FromResult(Name);
    }
}
