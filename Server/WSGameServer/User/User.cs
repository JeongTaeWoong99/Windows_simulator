using MikaNetwork;
using MikaProtocol;
using WSGameServer.Common;
using WSGameServer.DB;
using WSGameServer.Network;
using WSGameServer.Repository;

namespace WSGameServer.User;

/// <summary>
/// Session 접속 후 로그인하면 생성되는 게임 로직 단위 객체.
/// 참조 방향은 User -> Session 단방향이며, Session은 User를 알지 못한다.
/// 생성은 반드시 <see cref="UserManager.CreateUser"/>를 통해서만 이루어진다.
/// </summary>
public sealed partial class User : Entity
{
    public long SessionId { get; }
    public string Pid { get; }    
    
    public ISession Session { get; }
    public string NickName { get; set; }
    public DateTime LoggedInAt { get; }
    
    public long Uid { get; set; }
    public int AdminLevel { get; set; }
    public bool IsNewbie { get; set; }
    
    // Inventory
    private Inventory.Inventory Inventory { get; init; } = new();

    internal User(ISession session, string pid, string nickname)
    {
        SessionId = session.SessionId;
        Pid = pid;
        Session = session;
        NickName = nickname;
        LoggedInAt = DateTime.UtcNow;
    }

    // 주의: 소멸자(finalizer)는 GC가 객체를 수거할 때 비결정적으로 호출된다.
    // Destroy() 호출과 무관하며, 결정적 정리·로그는 OnDestroy에서 처리한다.
    // (여기는 GC 수거 여부 진단용으로만 남겨둠)
    ~User()
    {
        // GC 수거 여부 진단용이라 평소에는 보이지 않는 Trace로 둔다.
        ServerLog.Trace("유저", $"GC 수거 SessionId={SessionId} Pid={Pid}");
    }

    public void Login()
    {
        // 연결되지 않았으면 정리
        if (!Session.IsConnected)
        {
            Destroy();
            return;
        }
        
        UserManager.Instance.JoinUser(this);
        
        Send(new S_LoginResponse {Success = true, SessionId = SessionId});

        SendInventory();   // S_InventoryResponse
        SendCurrencies();  // S_CurrencyResponse

        // 오프라인 진행이 없으므로 로그인 시점에 정산할 구간이 없다.
        // 슬롯은 LoginRepository에서 이미 "지금부터" 시작하도록 만들어져 있다.
        SendWorkStationSlots(); // S_WorkStationSlotsResponse
    }

    protected override void OnCreate()
    {
        ServerLog.Info("유저", $"생성 SessionId={SessionId} Pid={Pid}");
        
        PostDBTask(new AccountRepository(this));
    }

    protected override void OnDestroy()
    {
        // 끊김 시 결정적으로 호출됨(로직 스레드). 소멸자가 아니라 여기가 정리 지점이다.
        ServerLog.Info("유저", $"소멸 SessionId={SessionId} Pid={Pid}");

        // 마지막 정산. 접속 중 완성된 판정은 정당하게 번 것이므로 끊겼다고 버리지 않는다.
        // 세션이 이미 닫혔으므로 푸시는 하지 않고(notify: false) 지급·저장만 한다.
        // 판정에 못 미친 조각은 여기서 함께 사라진다 — 진행도는 세션과 수명을 같이한다.
        try
        {
            SettleWorkStation(DateTime.UtcNow, notify: false);
        }
        catch (Exception e)
        {
            // 정산이 실패해도 세션 정리는 반드시 진행해야 한다(User가 남으면 누수다).
            ServerLog.Error("채취", $"종료 정산 실패 SessionId={SessionId}", e);
        }

        UserManager.Instance.LeaveUser(this);
    }

    public void Initialize(long userId, string nickName, int adminLevel, bool isNewbie)
    {
        Uid = userId;
        NickName = nickName;
        AdminLevel = adminLevel;
        IsNewbie = isNewbie;

        PostDBTask<LoginRepository>(new (this));
    }

    public void Send<T>(T packet) where T : IPacket => Session.SendPacket(packet);
    
    public void PostDBTask<TRepository>(TRepository repository) where TRepository : IRepository 
    {
        DBManager.Instance.Post(repository);
    }
}
