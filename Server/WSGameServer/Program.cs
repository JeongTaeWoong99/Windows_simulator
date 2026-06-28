using MikaDummyServer.Network;
using MikaProtocol;

namespace MikaDummyServer
{
    class Program
    {
        private static void Main(string[] args)
        {
            NetworkManager.Instance.Initialize();

            Console.WriteLine("[Server] 10010 포트에서 대기 중...");
            Console.WriteLine("[Server] 종료하려면 엔터를 누르세요.");
            Console.ReadLine();
        }
    }
}
