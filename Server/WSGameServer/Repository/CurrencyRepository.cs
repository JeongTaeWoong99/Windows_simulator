namespace WSGameServer;

// 유저의 재화 보유량을 DB에 반영한다. 델타가 아니라 확정 잔액을 쓴다 — 델타를 DB에서 더하면 재시도·중복 전송이 곧 재화 복제다.
// 한쪽만 바뀌어도 두 재화를 함께 쓴다(둘 다 확정 잔액이라 덮어써도 안전하고, 컬럼별 SQL이 늘지 않는다).
public sealed class SaveCurrencyRepository : IRepository
{
    private readonly long _gold;
    private readonly long _dia;

    public SaveCurrencyRepository(User user, long gold, long dia)
    {
        User  = user;
        _gold = gold;
        _dia  = dia;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        // 가입 시 0짜리 행을 만들지 않으므로 첫 변동이 곧 첫 INSERT다 — UPSERT로 두 경우를 한 문장에 담는다.
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_currency (user_id, gold, dia)
              VALUES (@userId, @gold, @dia)
              ON CONFLICT (user_id) DO UPDATE SET gold = excluded.gold, dia = excluded.dia;",
            new { userId = User.Uid, gold = _gold, dia = _dia });
    }

    public void Apply()
    {
    }
}
