#if UNITY_5_3_OR_NEWER

using System;

namespace MikaNetwork
{
    public static class ServerPacketHandler
    {
        [PacketHandler]
        public static void Handle_S_EchoResponse(ISession session, S_EchoResponse res)
        {
            Console.WriteLine($"[Client] Recv Echo: {res.Message}");
        }
    }
}

#endif
