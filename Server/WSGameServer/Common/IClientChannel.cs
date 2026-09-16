using MikaNetwork;
using MikaProtocol;

namespace WSGameServer;

// User가 클라이언트와 이어진 통로. ISession 전체가 아니라 User가 쓰는 셋만 노출한다.
// SendPacket이 확장 메서드라 Mock<ISession>으로는 패킷을 못 보기 때문 → Server/docs/테스트커버리지.md "테스트 접점"
public interface IClientChannel
{
    long SessionId { get; }

    /// <summary>끊긴 통로로는 보내지 않는다. 로그인 진입에서 확인한다.</summary>
    bool IsConnected { get; }

    void Send<T>(T packet) where T : IPacket;

    /// <summary>통로를 닫는다. 중복 로그인으로 밀려나는 세션 정리용 — User.Destroy()는 소켓을 닫지 않는다.</summary>
    void Disconnect();
}

/// <summary>운영 구현. ISession을 감싸 SendPacket으로 위임한다. 전송 계층을 아는 유일한 곳.</summary>
public sealed class SessionClientChannel : IClientChannel
{
    private readonly ISession _session;

    public SessionClientChannel(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public long SessionId   => _session.SessionId;
    public bool IsConnected => _session.IsConnected;

    public void Send<T>(T packet) where T : IPacket => _session.SendPacket(packet);

    public void Disconnect() => _session.Disconnect();
}
