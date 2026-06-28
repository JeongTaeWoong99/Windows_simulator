using MikaNetwork.Core.Network;

namespace MikaDummyServer.Network;

public class ClientPacketManager : MikaPacketManager
{
    public ClientPacketManager()
    {
        MikaGenerated.GeneratedHandlers.RegisterAll(this);
    }
}
