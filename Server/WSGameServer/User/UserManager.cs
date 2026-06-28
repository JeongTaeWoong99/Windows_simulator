using System.Collections.Concurrent;
using MikaNetwork.Core.Interfaces;
using MikaUtils;

namespace WSGameServer.User;

/// <summary>
/// 로그인한 User들을 SessionId 단위로 보관/조회/정리하는 게임 로직 레이어 매니저.
/// 프레임워크의 <c>SessionManager</c>(transport)와 분리된 게임 로직 전용 저장소다.
/// </summary>
public sealed class UserManager : Singleton<UserManager>
{
    private readonly ConcurrentDictionary<long, User> _users = new();

    public int Count => _users.Count;

    /// <summary>
    /// 로그인 성공 시 User를 생성·등록한다. 이미 같은 SessionId로 등록돼 있으면 null.
    /// </summary>
    public User? CreateUser(ISession session, string name)
    {
        var user = new User(session, name);
        return _users.TryAdd(user.SessionId, user) ? user : null;
    }

    public bool TryRemove(long sessionId, out User? user) => _users.TryRemove(sessionId, out user);

    public bool TryGet(long sessionId, out User? user) => _users.TryGetValue(sessionId, out user);
}
