using System.Threading.Tasks;
using MikaProtocol.Interfaces;
using MikaServerCore.Network;

namespace Network
{
    public class NetworkManager
    {
        private readonly MikaClient _client = new();
        private readonly ServerPacketManager _packetManager = new();

        public async Task Initialize()
        {
            _client.PacketReceived += (session, data) =>
            {
                _packetManager.OnRecvPacket(session, data);
                return default(ValueTask);
            };

            await _client.ConnectAsync("127.0.0.1", 10010);
        }

        public void Send<T>(T packet) where T : IPacket
        {
            _client.Send(packet);
        }
    }
}