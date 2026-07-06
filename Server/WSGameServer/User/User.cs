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
        Console.WriteLine($"[GC] User finalized SessionId: {SessionId}, Pid: {Pid}");
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
        
        SendInventory(); // S_InventoryResponse
    }

    protected override void OnCreate()
    {
        Console.WriteLine($"User created SessionId: {SessionId}, Pid: {Pid}");
        
        PostDBTask(new AccountRepository(this));
    }

    protected override void OnDestroy()
    {
        // 끊김 시 결정적으로 호출됨(로직 스레드). 소멸자가 아니라 여기가 정리 지점이다.
        Console.WriteLine($"User destroyed SessionId: {SessionId}, Pid: {Pid}");

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
