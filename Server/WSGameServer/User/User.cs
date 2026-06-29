using MikaNetwork;
using WSGameServer.Common;

namespace WSGameServer.User;

/// <summary>
/// Session 접속 후 로그인하면 생성되는 게임 로직 단위 객체.
/// 참조 방향은 User -> Session 단방향이며, Session은 User를 알지 못한다.
/// 생성은 반드시 <see cref="UserManager.CreateUser"/>를 통해서만 이루어진다.
/// </summary>
public sealed class User : Entity
{
    public long SessionId { get; }
    public ISession Session { get; }
    public string Name { get; }           
    public DateTime LoggedInAt { get; }

    internal User(ISession session, string name)
    {
        SessionId = session.SessionId;
        Session = session;
        Name = name;
        LoggedInAt = DateTime.UtcNow;
    }
    
    // public ValueTask PostDBTask<T>()
    // {
    //     
    // }
}
