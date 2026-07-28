using System;
using System.Collections.Generic;
using MemoryPack;

namespace MikaNetwork
{
    /// <summary>
    /// 수신 디스패처. PacketId → "그 타입으로 역직렬화 후 핸들러 호출"하는 어댑터를 등록해둔다.
    /// 등록 시점에만 구체 타입 T를 알면 되고, 수신부(OnRecvPacket)는 타입을 몰라도 된다.
    /// </summary>
    public class MikaPacketManager
    {
        private readonly Dictionary<ushort, Func<ISession, ReadOnlyMemory<byte>, Action>> _handlers = new();

        public void Register<T>(ushort id, Action<ISession, T> handler)
        {
            _handlers[id] = (session, body) =>
            {
                var packet = MemoryPackSerializer.Deserialize<T>(body.Span)!;

                return () => handler(session, packet); // 직렬화만 Network Thread가 하도록 고정
            };
        }

        // OnRecvCallback : 보통 Unity에서 바로 처리하지않고 PacketQueue로 넘어갈일이 있으면 사용
        public void OnRecvPacket(ISession session, ReadOnlyMemory<byte> data, Action<Action>? onRecvCallback = null)
        {
            if (data.Length < MikaPacketBuilder.HeaderSize) return;                       // 최소 헤더 크기

            ushort id   = MikaPacketBuilder.ReadId(data.Span);    // [0..2) = id
            var body = MikaPacketBuilder.ReadBody(data);                 // [4..]  = body (헤더 제외)
            
            if (_handlers.TryGetValue(id, out var handler))
            {
                var job = handler(session, body);
                if (onRecvCallback != null)
                {
                    onRecvCallback.Invoke(job); // 실행을 다른 스레드로 이양
                }
                else
                {
                    job.Invoke(); // NetworkThread에서 직접 실행
                }
            }
            else
            {
                // 등록 안 된 id(핸들러 누락·버전 불일치·알 수 없는 패킷) -> silent drop 대신 로그
                Console.WriteLine($"[MikaPacketManager] 미처리 패킷 수신: id={id}");
            }


        }
    }
}
