namespace WSGameServer;

/// <summary>
/// 테스트마다 따로 쓰는 접속 유저 목록. 전역 <see cref="UserManager.Instance"/>를 공유하면
/// 병렬로 도는 테스트끼리 서로의 유저를 보게 된다(T-089).
/// </summary>
public sealed class FakeOnlineUsers : IOnlineUsers
{
    private readonly Dictionary<long, User> _byUid = new();

    public void Add(User user) => _byUid[user.Uid] = user;

    public bool TryGetUserByUid(long uid, out User? user)
    {
        var found = _byUid.TryGetValue(uid, out var hit);
        user = hit;
        return found;
    }

    public IEnumerable<User> All => _byUid.Values;
}
