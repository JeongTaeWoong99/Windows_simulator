using System;
using System.Collections.Generic;
using MemoryPack;
using MikaNetwork.Core.Interfaces;

namespace MikaNetwork.Core.Network
{
    /// <summary>
    /// 수신 디스패처. PacketId → "그 타입으로 역직렬화 후 핸들러 호출"하는 어댑터를 등록해둔다.
    /// 등록 시점에만 구체 타입 T를 알면 되고, 수신부(OnRecvPacket)는 타입을 몰라도 된다.
    /// </summary>
    public class MikaPacketManager
    {
        private readonly Dictionary<ushort, Action<ISession, ReadOnlyMemory<byte>>> _handlers = new();

        public void Register<T>(ushort id, Action<ISession, T> handler)
        {
            _handlers[id] = (session, body) =>
            {
                var packet = MemoryPackSerializer.Deserialize<T>(body.Span)!;
                handler(session, packet);
            };
        }

        public void OnRecvPacket(ISession session, ReadOnlyMemory<byte> packet)
        {
            if (packet.Length < MikaPacketBuilder.HeaderSize) return;                       // 최소 헤더 크기

            ushort id   = MikaPacketBuilder.ReadId(packet.Span);    // [0..2) = id
            var body = MikaPacketBuilder.ReadBody(packet);                 // [4..]  = body (헤더 제외)

            if (_handlers.TryGetValue(id, out var handler))
            {
                handler(session, body);
            }
        }
    }
}
