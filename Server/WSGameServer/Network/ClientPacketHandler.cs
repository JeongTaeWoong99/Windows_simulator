using MikaNetwork;
using MikaProtocol;
using WSGameServer.User;

namespace WSGameServer.Network;

public static class ClientPacketHandler
{
    [PacketHandler]
    public static void Handle_C_EchoRequest(ISession session, C_EchoRequest req)
    {
        Console.WriteLine($"[Server] Recv Echo: {req.Message}");

        // 응답도 객체로 송신 (직렬화/프레이밍은 SendPacket이 처리)
        session.SendPacket(new S_EchoResponse { Message = req.Message });
    }

    [PacketHandler]
    public static void Handle_C_PingRequest(ISession session, C_PingRequest req)
    {
        
    }
    
    [PacketHandler]
    public static void Handle_C_LoginRequest(ISession session, C_LoginRequest req)
    {
        var user = UserManager.Instance.CreateUser(session, req.Id);
        Console.WriteLine($"[Server] Login: Id={req.Id}, Session={session.SessionId}, Success={user != null}");

        session.SendPacket(new S_LoginResponse { Success = user != null, SessionId = session.SessionId });
    }
}
