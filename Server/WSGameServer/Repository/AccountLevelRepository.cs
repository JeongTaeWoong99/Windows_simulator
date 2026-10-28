namespace WSGameServer;

/// <summary>
/// 계정 레벨·경험치를 <c>t_user_account</c>에 확정값으로 쓴다(UPSERT).
/// 증감이 아니라 확정값을 쓰므로 같은 작업이 두 번 돌아도 결과가 같다.
/// </summary>
public sealed class SaveAccountRepository : IRepository
{
    public int  Level { get; }
    public long Exp   { get; }

    public SaveAccountRepository(User user, int level, long exp)
    {
        User  = user;
        Level = level;
        Exp   = exp;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_account (user_id, level, exp)
              VALUES (@userId, @level, @exp)
              ON CONFLICT (user_id) DO UPDATE SET
                  level = excluded.level,
                  exp   = excluded.exp;",
            new { userId = User.Uid, level = Level, exp = Exp });
    }

    public void Apply()
    {
    }
}
