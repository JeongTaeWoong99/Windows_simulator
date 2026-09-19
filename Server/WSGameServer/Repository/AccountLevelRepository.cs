namespace WSGameServer;

/// <summary>
/// 계정 레벨·경험치·남은 특성 포인트를 <c>t_user_account</c>에 확정값으로 쓴다(UPSERT).
/// 증감이 아니라 확정값을 쓰므로 같은 작업이 두 번 돌아도 결과가 같다.
/// </summary>
public sealed class SaveAccountRepository : IRepository
{
    public int  Level      { get; }
    public long Exp        { get; }
    public int  TraitPoint { get; }

    public SaveAccountRepository(User user, int level, long exp, int traitPoint)
    {
        User       = user;
        Level      = level;
        Exp        = exp;
        TraitPoint = traitPoint;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_account (user_id, level, exp, trait_point)
              VALUES (@userId, @level, @exp, @traitPoint)
              ON CONFLICT (user_id) DO UPDATE SET
                  level       = excluded.level,
                  exp         = excluded.exp,
                  trait_point = excluded.trait_point;",
            new { userId = User.Uid, level = Level, exp = Exp, traitPoint = TraitPoint });
    }

    public void Apply()
    {
    }
}
