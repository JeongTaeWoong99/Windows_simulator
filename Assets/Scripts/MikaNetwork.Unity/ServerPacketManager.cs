#if UNITY_5_3_OR_NEWER

using MikaNetwork.Core.Network;

public class ServerPacketManager : MikaPacketManager
{
    public ServerPacketManager()
    {
        MikaGenerated.GeneratedHandlers.RegisterAll(this);
    }
}

#endif