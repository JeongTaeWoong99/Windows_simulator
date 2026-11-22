using System.Collections.Concurrent;
using MikaNetwork;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 로그인 요청이 어느 계정(pid)으로 들어갈지 정한다 (T-207). 스팀 티켓은 스팀에 확인한 SteamID로,
/// 티켓이 없으면 개발용 Id로 — 개발용은 설정(AllowDevLogin)이 켜진 서버에서만 받는다.
/// </summary>
public sealed class LoginGate
{
    private readonly ISteamWebApi _steam;
    private readonly bool _allowDevLogin;
    private readonly Action<Action> _post;
    private readonly Action<ISession, string, string> _createUser;
    private readonly Action<ISession, EResultCode> _reject;

    // 스팀 확인을 기다리는 세션. 응답은 HTTP 스레드에서 오므로 동시 접근에 안전해야 한다.
    private readonly ConcurrentDictionary<long, byte> _pending = new();

    /// <param name="post">로직 스레드로 넘긴다. 스팀 응답은 HTTP 스레드에서 오고, 유저 생성은 로직 스레드의 일이다.</param>
    public LoginGate(
        ISteamWebApi steam,
        bool allowDevLogin,
        Action<Action> post,
        Action<ISession, string, string> createUser,
        Action<ISession, EResultCode> reject)
    {
        _steam         = steam;
        _allowDevLogin = allowDevLogin;
        _post          = post;
        _createUser    = createUser;
        _reject        = reject;
    }

    /// <summary>기동 때 <see cref="Configure"/>로 정한 운영 인스턴스. 패킷 핸들러가 정적이라 여기서 꺼내 쓴다(경매 서비스와 같은 모양).</summary>
    public static LoginGate? Current { get; private set; }

    public static void Configure(LoginGate gate) => Current = gate;

    /// <summary>스팀 계정의 pid 접두. 개발용 Id로는 이 접두를 쓸 수 없다 — 남의 스팀 계정으로 들어가는 길이 된다.</summary>
    public const string SteamPidPrefix = "steam:";

    public async Task HandleAsync(ISession session, C_LoginRequest req)
    {
        if (string.IsNullOrEmpty(req.SteamTicket))
        {
            HandleDevLogin(session, req.Id);
            return;
        }

        // 스팀을 기다리는 동안 같은 세션의 로그인이 겹치면 User가 둘 생긴다 — 먼저 온 것만 받는다.
        if (!_pending.TryAdd(session.SessionId, 0))
        {
            ServerLog.Warn("로그인", $"거절 — 스팀 확인 중 로그인 재요청 sid={session.SessionId}");
            _reject(session, EResultCode.AlreadyLoggedIn);
            return;
        }

        try
        {
            await HandleSteamAsync(session, req.SteamTicket);
        }
        finally
        {
            _pending.TryRemove(session.SessionId, out _);
        }
    }

    private async Task HandleSteamAsync(ISession session, string steamTicket)
    {
        var ticket = await _steam.AuthenticateTicketAsync(steamTicket, CancellationToken.None);
        if (ticket.Code != EResultCode.Ok)
        {
            ServerLog.Warn("로그인", $"스팀 거절 {ticket.Code} — {ticket.Reason} sid={session.SessionId}");
            _post(() => _reject(session, ticket.Code));
            return;
        }

        // 이름은 첫 가입 때만 저장된다. 못 가져오면 SteamID로 — 이름 때문에 로그인을 막지 않는다.
        var name = await _steam.GetPersonaNameAsync(ticket.SteamId, CancellationToken.None)
                   ?? ticket.SteamId.ToString();

        _post(() =>
        {
            // 스팀을 기다리는 사이 끊겼으면 만들지 않는다 — 끊긴 세션의 User는 아무도 정리하지 않는다.
            if (!session.IsConnected)
            {
                ServerLog.Info("로그인", $"스팀 확인 중 연결 끊김 — 로그인 취소 SteamId={ticket.SteamId} sid={session.SessionId}");
                return;
            }

            _createUser(session, SteamPidPrefix + ticket.SteamId, name);
        });
    }

    // 로직 스레드에서 부른다(패킷 핸들러) — 기다릴 것이 없어 바로 처리한다.
    private void HandleDevLogin(ISession session, string id)
    {
        if (!_allowDevLogin)
        {
            ServerLog.Warn("로그인", $"거절 — 티켓 없는 로그인(개발용 꺼짐) Id={id} sid={session.SessionId}");
            _reject(session, EResultCode.SteamAuthFailed);
            return;
        }

        if (id.StartsWith(SteamPidPrefix, StringComparison.OrdinalIgnoreCase))
        {
            ServerLog.Warn("로그인", $"거절 — 개발용 Id에 스팀 접두 Id={id} sid={session.SessionId}");
            _reject(session, EResultCode.SteamAuthFailed);
            return;
        }

        _createUser(session, id, id);
    }
}
