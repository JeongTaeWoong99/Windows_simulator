using MemoryPack;
using MikaNetwork.Core.Interfaces;
using MikaNetwork.Core.Network;
using MikaProtocol.Interfaces;

/// <summary>
/// Core(MikaSession/MikaClient)는 byte[]만 다루는 순수 전송 계층이라 IPacket을 모른다.
/// 객체 → [id][size][body] 직렬화/프레이밍은 약속(Protocol) 계층의 책임이므로
/// 여기서 확장 메서드로 얹는다. (Protocol → Core 단방향 의존)
/// </summary>

namespace MikaProtocol
{
    public static class MikaSessionPacketExtensions
    {
        public static void SendPacket<T>(this ISession session, T packet) where T : IPacket
        {
            byte[] body = MemoryPackSerializer.Serialize(packet);                                          // body 직렬화
            byte[] framed = MikaPacketBuilder.MakePacket(MikaGenerated.GeneratedPacketIds.Get<T>(), body); // [id][size][body]
            session.Send(framed);                                                                       // Core의 byte[] Send 재사용
        }
    }
}
