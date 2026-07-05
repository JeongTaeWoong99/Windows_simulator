using Dapper;
using MikaNetwork.Server;
using MikaProtocol;
using WSGameServer.DB;
using WSGameServer.Network;

namespace WSGameServer;

class Program
{
    private static void Main(string[] args)
    {
        // Dapper 컬럼 매핑: snake_case(user_id) → PascalCase(UserId)
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        
        DBExecutor.Instance.Start(8);
        LogicExecutor.Instance.Start();
        
        DBManager.Instance.Initialize("game.sqlite3");
        NetworkManager.Instance.Initialize();

        Console.WriteLine("[Server] 10050 포트에서 대기 중...");
        Console.WriteLine("[Server] 종료하려면 엔터를 누르세요.");
        Console.ReadLine();
    }
}

