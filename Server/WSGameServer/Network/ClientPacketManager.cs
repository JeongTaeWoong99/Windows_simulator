using MikaNetwork;

namespace WSGameServer.Network;

public class ClientPacketManager : MikaPacketManager
{
    public ClientPacketManager()
    {
        MikaGenerated.GeneratedHandlers.RegisterAll(this);
    }
}
