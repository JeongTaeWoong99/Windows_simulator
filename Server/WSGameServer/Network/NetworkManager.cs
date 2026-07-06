using MikaUtils;
using MikaNetwork.Server;
using WSGameServer.User;

namespace WSGameServer.Network;

public class NetworkManager : Singleton<NetworkManager>
{
    private readonly MikaServer _server = new(10050);
    private readonly ClientPacketManager _packetManager = new();

    public void Initialize()
    {
        _server.PacketReceived += (session, data) =>
        {
            _packetManager.OnRecvPacket(session, data, LogicExecutor.Instance.Post);
            
            return ValueTask.CompletedTask;
        };

        // 접속 해제 시 해당 세션의 User 정리
        // 네트워크 스레드에서 호출되지만, Destroy()가 OnDestroy를 LogicExecutor로 넘겨
        // 실제 정리(UserManager.LeaveUser)는 로직 스레드에서 실행된다.
        _server.Disconnected += session => session.GetUser()?.Destroy();

        _server.Listen();
    }
}
