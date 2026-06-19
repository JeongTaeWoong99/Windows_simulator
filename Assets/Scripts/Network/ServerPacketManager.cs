using MikaNetwork.Core.Network;

namespace Network
{
    public class ServerPacketManager : MikaPacketManager
    {
        public ServerPacketManager()
        {
            MikaGenerated.GeneratedHandlers.RegisterAll(this);
        }
    }
}