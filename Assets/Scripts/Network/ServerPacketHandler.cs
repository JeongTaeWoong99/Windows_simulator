using System;
using MikaNetwork.Core.Interfaces;
using MikaProtocol;

namespace Network
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