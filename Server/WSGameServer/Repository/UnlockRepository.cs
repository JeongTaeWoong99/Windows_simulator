namespace WSGameServer;

/// <summary>
/// 열린 해금 하나를 <c>t_user_unlock</c>에 기록한다. 해금은 영구라 UPDATE·DELETE 경로가 없다.
/// 이미 있으면 무시한다 — 재시도·중복 전송이 와도 행이 하나뿐이라 안전하다.
/// </summary>
public sealed class SaveUnlockRepository : IRepository
{
    public int UnlockTid { get; }

    public SaveUnlockRepository(User user, int unlockTid)
    {
        User      = user;
        UnlockTid = unlockTid;
    }

    public long Key => User.SessionId;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_unlock (user_id, unlock_tid)
              VALUES (@userId, @unlockTid)
              ON CONFLICT (user_id, unlock_tid) DO NOTHING;",
            new { userId = User.Uid, unlockTid = UnlockTid });
    }

    public void Apply()
    {
    }
}
