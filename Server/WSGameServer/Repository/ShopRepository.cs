using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 즉시 판매를 DB에 반영한다. <b>인벤토리 차감과 재화 잔액을 한 트랜잭션으로 쓴다</b> —
/// 둘로 나누면 차감만 남고 지급이 실패했을 때 아이템이 증발한다.
/// </summary>
public sealed class SellItemsRepository : IRepository
{
    private readonly List<ItemChangeInfo> _itemChanges;
    private readonly long _gold;
    private readonly long _dia;

    public SellItemsRepository(User user, List<ItemChangeInfo> itemChanges, long gold, long dia)
    {
        User         = user;
        _itemChanges = itemChanges;
        _gold        = gold;
        _dia         = dia;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in _itemChanges)
            {
                // 0개가 된 아이템은 행을 지운다 — 남겨 두면 다음 로그인에 0개짜리로 적재된다.
                if (change.Count == 0)
                {
                    await tx.ExecuteAsync(
                        "DELETE FROM t_user_inventory WHERE user_id = @userId AND item_id = @itemId",
                        new { userId = User.Uid, itemId = change.ItemId });
                    continue;
                }

                await tx.ExecuteAsync(
                    @"INSERT INTO t_user_inventory (user_id, item_id, count)
                      VALUES (@userId, @itemId, @count)
                      ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count;",
                    new { userId = User.Uid, itemId = change.ItemId, count = change.Count });
            }

            // 델타가 아니라 확정 잔액을 쓴다 — 재시도·중복 전송이 곧 재화 복제가 된다.
            // 바뀌는 건 골드뿐이지만 두 재화를 함께 쓴다 — SaveCurrencyRepository와 같은 이유다.
            await tx.ExecuteAsync(
                @"INSERT INTO t_user_currency (user_id, gold, dia)
                  VALUES (@userId, @gold, @dia)
                  ON CONFLICT (user_id) DO UPDATE SET gold = excluded.gold, dia = excluded.dia;",
                new { userId = User.Uid, gold = _gold, dia = _dia });
        });
    }

    public void Apply()
    {
    }
}
