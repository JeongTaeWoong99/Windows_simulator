using Dapper;
using MikaNetwork.Server;
using GameData;

namespace WSGameServer;

class Program
{
    private const string SQL_CONNECTION_STRING = "game.sqlite3";
    
    private static void Main(string[] args)
    {
        // Dapper 컬럼 매핑: snake_case(user_id) → PascalCase(UserId)
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        
        GameTable.LoadAll(name => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name)));

        // 드롭 테이블 추첨기를 미리 만들어 둔다. 반드시 GameTable.LoadAll 뒤에 온다.
        DropTableCatalog.Instance.LoadAll();

        DBExecutor.Instance.Start(8);
        LogicExecutor.Instance.Start();
        
        // 파일명 → Shared/<file> 경로 탐색 → 커넥션 팩토리 구성. 테스트는 팩토리 오버로드로 :memory:를 넣는다.
        DBManager.Instance.Initialize(SQL_CONNECTION_STRING);
        NetworkManager.Instance.Initialize();

        // 접속 중인 플레이어의 채취를 주기적으로 정산해 밀어 준다(서버 권위).
        GatheringScheduler.Instance.Start();

        WarnIfTuned();

        ServerLog.Info("서버", "10050 포트에서 대기 중...");
        ServerLog.Info("서버", "종료하려면 엔터를 누르세요.");
        Console.ReadLine();
    }

    /// <summary>
    /// 확인용 설정이 켜진 채로 돌고 있으면 시작할 때 경고한다.
    /// 전역 배수를 올려 둔 걸 잊고 배포하면 재화 산출량이 통째로 어긋난다.
    /// </summary>
    private static void WarnIfTuned()
    {
        const double baseCycle = WorkStationSlot.BaseCycleSeconds;

        if (Math.Abs(GatherSpeedMultiplier - 1.0) < 0.0001)
        {
            ServerLog.Info("설정", $"채취 전역 배수 1.0배 — 기준 주기 {baseCycle:F1}초");
            return;
        }

        ServerLog.Warn("설정",
            $"채취 전역 배수 {GatherSpeedMultiplier:F1}배 — " +
            $"기준 주기 {baseCycle:F1}초 → {baseCycle / GatherSpeedMultiplier:F1}초 (확인용 설정)");
    }
}

