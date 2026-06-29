using System.Net;
using MikaNetwork;

namespace MikaNetwork.Server;

public class MikaServer : IDisposable
{
    private readonly MikaAcceptor _acceptor;
    public SessionManager SessionManager { get; init; } = new();

    public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? PacketReceived;

    /// <summary>세션 접속 해제 시 게임 로직 레이어가 정리(User 제거 등)할 수 있도록 노출.</summary>
    public event Action<ISession>? Disconnected;

    public EndPoint EndPoint => _acceptor.EndPoint;
    
    public MikaServer(int port)
        : this(new MikaServerOptions
        {
            AcceptorOpt = new MikaAcceptorOptions { LocalAddr = IPAddress.Any, Port = port }
        })
    {
        
    }
    public MikaServer(IPAddress address, int port)
        : this(new MikaServerOptions
        {
            AcceptorOpt = new MikaAcceptorOptions { LocalAddr = address, Port = port }
        })
    {
        
    }
    public MikaServer(MikaServerOptions options)
    {
        _acceptor = new MikaAcceptor(options.AcceptorOpt!);
        _acceptor.Accepted += OnAccepted;
        
    }

    public void Listen()
    {
        _acceptor.Listen();
    }


    public void Send(int sessionId, byte[] data)
    {
        if (SessionManager.TryGet(sessionId, out var session))
        {
            session?.Send(data);
        }
    }
    public void Send(MikaServerSession serverSession, byte[] data)
    {
        serverSession.Send(data);    
    }

    public void Stop()
    {
        Dispose();
    }
    
    public void Dispose()
    {
        _acceptor.Dispose();
    }

    private void OnAccepted(MikaServerSession serverSession)
    {
        serverSession.Disconnected += OnDisconnected;
        serverSession.Received += OnSessionPacketReceived;
        
        SessionManager.TryAdd(serverSession.SessionId, serverSession);
        
        Console.WriteLine($"[{serverSession.SessionId}] Accepted");

        _ = serverSession.StartAsync();
    }
    private void OnDisconnected(ISession session)
    {
        session.Received -= OnSessionPacketReceived;
        session.Disconnected -= OnDisconnected;

        SessionManager.TryRemove(session.SessionId, out _);

        // 게임 로직 레이어가 User 등 세션 연관 상태를 정리하도록 알림
        Disconnected?.Invoke(session);

        Console.WriteLine($"[{session.SessionId}] Disconnected");
    }

    private async ValueTask OnSessionPacketReceived(ISession session, ReadOnlyMemory<byte> data)
    { 
        var handler = PacketReceived;
        if (handler != null)
        {
            await handler(session, data);
        }
    }
    
    
}