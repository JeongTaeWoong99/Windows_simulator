using MikaProtocol;

namespace WSGameServer;

public class AddItemRepository : IRepository
{
    public long Key => User.DbKey;

    public User User { get; init; }
    public ItemChangeInfo ItemChangeInfo { get; init; }

    public AddItemRepository(User user, ItemChangeInfo itemChangeInfo)
    {
        User = user;
        ItemChangeInfo = itemChangeInfo;
    }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_inventory (user_id, item_id, count)
              VALUES (@userId, @itemId, @count)
              ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count;",
            new { userId = User.Uid, itemId = ItemChangeInfo.ItemId, count = ItemChangeInfo.Count });
    }

    public void Apply()
    {

    }
}

/// <summary>
/// 인벤토리 변경분(누적 총량)을 확정값으로 쓴다. 0개가 된 아이템은 행을 지운다 — 남기면 다음 로그인에 0개짜리로 적재된다.
/// 상자 차감처럼 재화와 짝이 아닌 차감에 쓴다(재화와 짝이면 <see cref="SellItemsRepository"/>).
/// </summary>
public sealed class SaveItemChangesRepository : IRepository
{
    private readonly List<ItemChangeInfo> _changes;

    public SaveItemChangesRepository(User user, List<ItemChangeInfo> changes)
    {
        User     = user;
        _changes = changes;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in _changes)
            {
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
        });
    }

    public void Apply()
    {
    }
}
