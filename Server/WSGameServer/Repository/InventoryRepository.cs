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
            @"INSERT INTO t_user_inventory (user_id, container, item_id, count, slot)
              VALUES (@userId, @container, @itemId, @count, @slot)
              ON CONFLICT (user_id, container, item_id) DO UPDATE SET count = excluded.count, slot = excluded.slot;",
            new
            {
                userId = User.Uid, container = (int)ItemChangeInfo.Container, itemId = ItemChangeInfo.ItemId,
                count  = ItemChangeInfo.Count, slot = ItemChangeInfo.Slot,
            });
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

    // 목록을 복사해 둔다 — 넘긴 쪽이 로직 스레드에서 목록을 고쳐도 DB 스레드의 순회가 깨지지 않게.
    public SaveItemChangesRepository(User user, List<ItemChangeInfo> changes)
    {
        User     = user;
        _changes = new List<ItemChangeInfo>(changes);
    }

    public long Key => User.DbKey;

    public User User { get; }

    /// <summary>저장할 변경분 — DB 스레드가 순회한다.</summary>
    public IReadOnlyList<ItemChangeInfo> Changes => _changes;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in _changes)
            {
                if (change.Count == 0)
                {
                    await tx.ExecuteAsync(
                        "DELETE FROM t_user_inventory WHERE user_id = @userId AND container = @container AND item_id = @itemId",
                        new { userId = User.Uid, container = (int)change.Container, itemId = change.ItemId });
                    continue;
                }

                await tx.ExecuteAsync(
                    @"INSERT INTO t_user_inventory (user_id, container, item_id, count, slot)
                      VALUES (@userId, @container, @itemId, @count, @slot)
                      ON CONFLICT (user_id, container, item_id) DO UPDATE SET count = excluded.count, slot = excluded.slot;",
                    new
                    {
                        userId = User.Uid, container = (int)change.Container, itemId = change.ItemId,
                        count  = change.Count, slot = change.Slot,
                    });
            }
        });
    }

    public void Apply()
    {
    }
}
