using System.Collections.Concurrent;
using MikaNetwork;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 로그인한 User들을 SessionId 단위로 보관/조회/정리하는 게임 로직 레이어 매니저.
/// 프레임워크의 <c>SessionManager</c>(transport)와 분리된 게임 로직 전용 저장소다.
/// </summary>
public sealed class UserManager : Singleton<UserManager>
{
    private readonly ConcurrentDictionary<ulong, User>   _userKeys    = new(); // uid -> User
    private readonly ConcurrentDictionary<string, ulong> _pids        = new(); // pid -> uid
    private readonly ConcurrentDictionary<long, ulong>   _sessionKeys = new(); // sessId -> uid  
    //private readonly ConcurrentDictionary<long, long>   _uids        = new(); // 

    public bool TryGetUserBySessionId(long sessionId, out User? user)
    {
        if (!_sessionKeys.TryGetValue(sessionId, out var uid))
        {
            user = null;
            return false;
        }

        return TryGetUser(uid, out user);
    }

    
    public bool TryGetUserByPid(string pid, out User? user)
    {
        if (!_pids.TryGetValue(pid, out var uid))
        {
            user = null;
            return false;
        }

        return TryGetUser(uid, out user);            
    }
    
    public bool TryGetUser(ulong uid, out User? user) => _userKeys.TryGetValue(uid, out user);

    public int Count => _userKeys.Count;

    /// <summary>접속 중인 모든 유저. 주기 정산처럼 전체를 훑어야 할 때 쓴다.</summary>
    public IEnumerable<User> All => _userKeys.Values;

    
    public void CreateUser(ISession session, string pid, string nickname) // pid is token or ...
    {
        // pid 중복체크
        if (TryGetUserByPid(pid, out _))
        {
            // Error Send
            // Log
            return;
        }
        
        // 이미 존재하는 유저인지 확인
        if (TryGetUserBySessionId(session.SessionId, out _))
        {
            // Log
            return;
        }

        // 여기가 조립 지점이다 — 전송 통로·DB 큐·시각을 운영 구현으로 묶어 User에 넘긴다.
        // User 자신은 ISession도 DBManager도 모른다.
        var user = new User(
            new SessionClientChannel(session),
            DBManager.Instance,
            pid,
            nickname,
            DateTime.UtcNow);

        if (!user.Create())
        {
            // Log
            user.Destroy();
        }
    }

    public bool JoinUser(User? user)
    {
        if (null == user)
        {
            // Log
            return false;
        }

        _userKeys.TryAdd(user.Key, user);
        _pids.TryAdd(user.Pid, user.Key);
        _sessionKeys.TryAdd(user.SessionId, user.Key);

        return true;
    }

    public bool LeaveUser(User? user)
    {
        if (null == user)
        {
            // LOg
            return false;
        }

        _sessionKeys.TryRemove(user.SessionId, out _);
        _pids.TryRemove(user.Pid, out _);
        _userKeys.TryRemove(user.Key, out _);

        return true;
    }
    
}
