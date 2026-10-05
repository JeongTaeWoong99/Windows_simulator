namespace WSGameServer;

/// <summary>
/// 특성 하나의 레벨을 <c>t_user_trait</c>에 기록한다. 마지막 값으로 덮어쓴다 — 치트로는 내려가기도 한다.
/// </summary>
public sealed class SaveUserTraitRepository : IRepository
{
    public int UserTraitTid { get; }
    public int Level        { get; }

    public SaveUserTraitRepository(User user, int userTraitTid, int level)
    {
        User         = user;
        UserTraitTid = userTraitTid;
        Level        = level;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_trait (user_id, user_trait_tid, level)
              VALUES (@userId, @userTraitTid, @level)
              ON CONFLICT (user_id, user_trait_tid) DO UPDATE SET level = excluded.level;",
            new { userId = User.Uid, userTraitTid = UserTraitTid, level = Level });
    }

    public void Apply()
    {
    }
}
