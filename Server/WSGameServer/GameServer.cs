using GameData;
using MikaNetwork.Server;

namespace WSGameServer;

public class GameServer : IDisposable
{
    private const string SQL_CONNECTION_STRING = "game.sqlite3";
    
    private readonly LogicExecutor _logicExecutor;
    private readonly NetworkManager _networkManager;
    private readonly GatheringScheduler _gatheringScheduler;
    private readonly SessionWatchdog _sessionWatchdog;
    private readonly DBManager _dbManager;

    // 경매장 연결. 경매장이 꺼져 있어도 게임은 돈다 — 경매 요청만 AuctionUnavailable로 돌아가고 릴레이가 재시도한다.
    private GrpcAuctionClient? _auctionClient;
    private readonly CancellationTokenSource _auctionStop = new();

    // 매니저, Executor 등 전역 싱글톤은 한번 생성하고 이후는 생성자 주입
    public GameServer()
    {
        _logicExecutor = new LogicExecutor();
        _sessionWatchdog = new SessionWatchdog(_logicExecutor);
        _networkManager = new NetworkManager(_logicExecutor, _sessionWatchdog);
        _gatheringScheduler = new GatheringScheduler(_logicExecutor);
        _dbManager = new DBManager(_logicExecutor);
    }

    public void Initialize()
    {
        UserManager.Instance.Initialize(_dbManager, _logicExecutor);
    }

    public async Task Run()
    {
        await Task.Run(() =>
        {
            // Dapper 컬럼 매핑 옵션은 쓰지 않는다 — Row 프로퍼티가 DB 컬럼명(snake_case)과 1:1이다.
            GameTable.LoadAll(name => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name)));
            ConstantsCheck.EnsureAll();   // 공용 상수가 데이터에 다 있는지 — 없으면 여기서 기동이 멈춘다

            // 드롭 테이블·가챠 풀 추첨기와 산업 레벨 인덱스를 미리 만들어 둔다. 반드시 GameTable.LoadAll 뒤에 온다.
            DropTableCatalog.Instance.LoadAll();
            GachaPoolCatalog.Instance.LoadAll();
            IndustryLevelCatalog.Instance.LoadAll();
            CharacterLevelCatalog.Instance.LoadAll();
            UnlockCatalog.Instance.LoadAll();   // 데이터 오류(선행 순환·1:1 위반)면 여기서 기동이 멈춘다
            EquipCatalog.Instance.LoadAll();
            EnchantCatalog.Instance.LoadAll();
            AccountLevelCatalog.Instance.LoadAll();
            UserTraitCatalog.Instance.LoadAll();
            CommonRewardCatalog.Instance.LoadAll();
            MailCatalog.Instance.LoadAll();   // ItemTIDs·ItemCounts 개수가 다르면 여기서 기동이 멈춘다
            CharacterTableValidator.Validate(GameTable.CharacterTable.All);   // 기본 적성 > 상한이면 기동이 멈춘다

            // 실행기 예외 훅. Lib은 로그 정책이 없다 — 여기서 채우지 않으면 예외가 조용히 사라진다.
            DBExecutor.JobFailed    = e => ServerLog.Error("DB", "DB 작업 예외", e);
            LogicExecutor.JobFailed = e => ServerLog.Error("로직", "로직 작업 예외", e);

            DBExecutor.Instance.Start(8);
            _logicExecutor.Start();

            // 파일명 → Shared/<file> 경로 탐색 → 커넥션 팩토리 구성. 테스트는 팩토리 오버로드로 :memory:를 넣는다.
            _dbManager.Initialize(SQL_CONNECTION_STRING);
            _networkManager.Initialize();

            // 접속 중인 플레이어의 채취를 주기적으로 정산해 밀어 준다(서버 권위).
            _gatheringScheduler.Start();

            StartAuction();
        });

    }
    
    
    private void StartAuction()
    {
        var settings = AuctionClientSettings.FromEnvironment();
        _auctionClient = new GrpcAuctionClient(settings);

        var relay = new AuctionRelay(_auctionClient, _dbManager, _logicExecutor,
                                     uid => UserManager.Instance.TryGetUserByUid(uid, out var user) ? user : null);
        AuctionService.Configure(new AuctionService(_auctionClient, _logicExecutor) { KickRelay = relay.Kick });

        _ = Task.Run(() => relay.RunAsync(() => DateTime.UtcNow, _auctionStop.Token));
        ServerLog.Info("경매", $"경매장 {settings.Address} — 릴레이 시작");
    }

    public void Dispose()
    {
        _auctionStop.Cancel();
        _auctionClient?.Dispose();
    }
}