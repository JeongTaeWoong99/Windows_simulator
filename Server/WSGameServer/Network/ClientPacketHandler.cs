using MikaNetwork;
using MikaProtocol;
using WSGameServer.DB;
using WSGameServer.User;
using WSGameServer.User.Repository;

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
        Console.WriteLine("[Server] Recv Ping");

        session.SendPacket(new S_PongResponse());
    }

    
    [PacketHandler]
    public static void Handle_C_LoginRequest(ISession session, C_LoginRequest req)
    {
        // DB 스레드에서 조회/자동가입 → 로직 스레드에서 User 등록·응답 (LoginRepository.Apply)
        Console.WriteLine($"[Server] Login 요청: Id={req.Id}, Session={session.SessionId}");

        UserManager.Instance.CreateUser(session, req.Id, req.Id);
        //DBManager.Instance.Post(new LoginRepository(session, req.Id));
    }
}
