using System;
using MikaNetwork;
using MikaProtocol;

namespace MikaDummyClient
{
    public static class ServerPacketHandler
    {
        [PacketHandler]
        public static void Handle_S_EchoResponse(ISession session, S_EchoResponse res)
        {
            Console.WriteLine($"[Client] Recv Echo: {res.Message}");
        }

        [PacketHandler]
        public static void Handle_S_PongResponse(ISession session, S_PongResponse res)
        {
            Console.WriteLine("[Client] Recv Pong");
        }

        [PacketHandler]
        public static void Handle_S_LoginResponse(ISession session, S_LoginResponse res)
        {
            Console.WriteLine($"[Client] Recv Login: Success={res.Success}, SessionId={res.SessionId}");
        }

        [PacketHandler]
        public static void Handle_S_UpdateItemResponse(ISession session, S_UpdateItemResponse res)
        {
            Console.WriteLine($"[Client] Recv UpdateItem: Count={res.Items.Count}");
            foreach (var item in res.Items)
                Console.WriteLine($"  - ItemId={item.ItemId}, Count={item.Count}");
        }
    }
}

