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
        // _server.Disconnected += session => UserManager.Instance.TryRemove(session.SessionId, out _);

        _server.Listen();
    }
}
