using MikaNetwork;
using MikaProtocol;
using WSGameServer.Common;
using WSGameServer.DB;
using WSGameServer.Network;
using WSGameServer.User.Repository;

namespace WSGameServer.User;

/// <summary>
/// Session 접속 후 로그인하면 생성되는 게임 로직 단위 객체.
/// 참조 방향은 User -> Session 단방향이며, Session은 User를 알지 못한다.
/// 생성은 반드시 <see cref="UserManager.CreateUser"/>를 통해서만 이루어진다.
/// </summary>
public sealed class User : Entity
{
    public long SessionId { get; }
    public string Pid { get; }    
    
    public ISession Session { get; }
    public string NickName { get; set; }
    public DateTime LoggedInAt { get; }
    
    
    public long Uid { get; set; }
    public int AdminLevel { get; set; }
    public bool IsNewbie { get; set; }

    internal User(ISession session, string pid, string nickname)
    {
        SessionId = session.SessionId;
        Pid = pid;
        Session = session;
        NickName = nickname;
        LoggedInAt = DateTime.UtcNow;
    }

    public void Login()
    {
        UserManager.Instance.JoinUser(this);
        
        Send(new S_LoginResponse {Success = true, SessionId = SessionId});
    }

    protected override void OnCreate()
    {
        PostDBTask(new AccountRepository(this));
    }

    protected override void OnDestroy()
    {
        UserManager.Instance.LeaveUser(this);
    }

    public void Initialize(long userId, string nickName, int adminLevel, bool isNewbie)
    {
        Uid = userId;
        NickName = nickName;
        AdminLevel = adminLevel;
        IsNewbie = isNewbie;

        PostDBTask(new LoginRepository(this));
    }
    
    public void Send(IPacket packet)
    {
        Session.SendPacket(packet);
    }
    
    public void PostDBTask(IRepository repository)
    {
        DBManager.Instance.Post(repository);
    }
}
