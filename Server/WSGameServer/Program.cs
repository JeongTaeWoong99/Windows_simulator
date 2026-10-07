using MikaNetwork.Server;
using GameData;

namespace WSGameServer;

class Program
{
    private static async Task Main(string[] args)
    {
        var gameServer = new GameServer();
        gameServer.Initialize();
        await gameServer.Run();

        ServerLog.Info("서버", "10050 포트에서 대기 중...");
        ServerLog.Info("서버", "종료하려면 엔터를 누르세요.");
        Console.ReadLine();
    }
}

