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
            Console.WriteLine($"[Client] Recv UpdateItem: Count={res.ItemChangeInfos?.Count}");
            foreach (var item in res.ItemChangeInfos!)
                Console.WriteLine($"  - Kind={item.Kind.ToString()}, ItemId={item.ItemId}, Count={item.Count}");
        }

        [PacketHandler]
        public static void Handle_S_InventoryResponse(ISession session, S_InventoryResponse res)
        {
            Console.WriteLine($"[Client] Recv Inventory: Count={res.Items?.Count}");
            foreach (var item in res.Items!)
                Console.WriteLine($"  - ItemId={item.ItemId}, Count={item.Count}");
        }

        [PacketHandler]
        public static void Handle_S_GachaDrawResponse(ISession session, S_GachaDrawResponse res)
        {
            if (!res.Success)
            {
                Console.WriteLine("[Client] Recv Gacha: 실패(잘못된 GachaId 또는 DrawCount)");
                return;
            }

            Console.WriteLine($"[Client] Recv Gacha: Count={res.Rewards?.Count}");
            foreach (var reward in res.Rewards!)
                Console.WriteLine($"  - Rarity={reward.Rarity.ToString()}, ItemId={reward.ItemId}, Count={reward.Count}");
        }
    }
}

