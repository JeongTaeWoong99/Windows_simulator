using Dapper;
using MikaNetwork.Server;
using MikaProtocol;
using WSGameServer.Common;
using WSGameServer.DB;
using WSGameServer.Network;
using GameData;

namespace WSGameServer;

class Program
{
    private static void Main(string[] args)
    {
        // Dapper 컬럼 매핑: snake_case(user_id) → PascalCase(UserId)
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        
        GameTable.LoadAll(name => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name)));

        // 드롭 테이블 추첨기를 미리 만들어 둔다. 반드시 GameTable.LoadAll 뒤에 온다.
        DropTableCatalog.Instance.LoadAll();

        DBExecutor.Instance.Start(8);
        LogicExecutor.Instance.Start();
        
        DBManager.Instance.Initialize("game.sqlite3");
        NetworkManager.Instance.Initialize();

        // 접속 중인 플레이어의 채취를 주기적으로 정산해 밀어 준다(서버 권위).
        GatheringScheduler.Instance.Start();

        Console.WriteLine("[Server] 10050 포트에서 대기 중...");
        Console.WriteLine("[Server] 종료하려면 엔터를 누르세요.");
        Console.ReadLine();
    }
}

