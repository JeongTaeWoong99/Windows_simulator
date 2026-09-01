using System;
using System.Net;
using System.Threading.Tasks;
using MikaProtocol;

namespace MikaNetwork
{
    public sealed class MikaClient
    {
        private readonly MikaConnector _connector = new();
        public MikaClientSession? Session { get; private set; }
        public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? PacketReceived;

        public async Task ConnectAsync(string ip, int port) => await ConnectAsync(IPAddress.Parse(ip), port).ConfigureAwait(false);
        public async Task ConnectAsync(IPAddress ipAddress, int port)
        {
            Session = await _connector.ConnectAsync(ipAddress, port).ConfigureAwait(false);

            Session.Received += OnSessionPacketReceived;
            Session.Connected += OnConnected;
            Session.Disconnected += OnDisconnected;

            // 스레드풀에서 시작한다. 호출자의 SynchronizationContext 위에서 그냥 부르면
            // 두 루프의 await가 그 컨텍스트를 캡처해, 호출자의 메시지 루프가 멈추면 소켓도 함께 멈춘다.
            _ = Task.Run(() => Session.StartAsync());
        }

        public void Disconnect()
        {
            Session?.Disconnect();
        }

        public void Send<T>(T packet) where T : IPacket
        {
            Session?.SendPacket(packet);
        }

        private async ValueTask OnSessionPacketReceived(ISession session, ReadOnlyMemory<byte> data)
        {
            var handler = PacketReceived;
            if (handler != null)
            {
                await handler(session, data);
            }
        }
        
        private void OnConnected(ISession session)
        {
            Console.WriteLine($"Connected to {session.RemoteEndPoint}");
        }

        private void OnDisconnected(ISession session)
        {
            Console.WriteLine($"$Disconnected from {session.RemoteEndPoint}");
        }

    }
}
