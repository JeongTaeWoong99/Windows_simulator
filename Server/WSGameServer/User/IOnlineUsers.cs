namespace WSGameServer;

/// <summary>
/// 접속 중인 유저 조회. <see cref="User"/>가 다른 유저를 봐야 하는 두 경우(uid로 찾기 · 전체 순회)만 연다 —
/// 등록·해제는 <see cref="UserManager"/>가 <see cref="User.LoggedIn"/>·<see cref="User.Left"/>를 듣고 직접 한다.
/// </summary>
public interface IOnlineUsers
{
    bool TryGetUserByUid(long uid, out User? user);

    IEnumerable<User> All { get; }
}
