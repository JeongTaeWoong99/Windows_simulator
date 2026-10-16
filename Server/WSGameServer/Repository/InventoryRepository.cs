using MikaProtocol;

namespace WSGameServer;

/// <summary>지급 변경분(칸마다 한 줄)을 한 트랜잭션으로 쓴다 — 한 번의 지급이 여러 칸에 걸칠 수 있다(T-160).</summary>
public sealed class AddItemRepository : IRepository
{
    private readonly List<ItemChangeInfo> _changes;

    public AddItemRepository(User user, List<ItemChangeInfo> changes)
    {
        User     = user;
        _changes = new List<ItemChangeInfo>(changes);
    }

    public long Key => User.DbKey;

    public User User { get; }

    public IReadOnlyList<ItemChangeInfo> Changes => _changes;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in _changes)
            {
                await SaveItemChangesRepository.WriteAsync(tx, User.Uid, change);
            }
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
                await WriteAsync(tx, User.Uid, change);
            }
        });
    }

    /// <summary>칸 하나의 확정 상태를 쓴다. 수량 0이면 행을 지운다 — 자원을 쓰는 저장은 전부 이 문장을 지난다.</summary>
    public static Task WriteAsync(DbConnection tx, long userId, ItemChangeInfo change)
    {
        // 칸이 키다(T-160) — 같은 자원이 여러 칸에 있을 수 있고, 한 칸에는 묶음 하나다.
        if (change.Count == 0)
        {
            return tx.ExecuteAsync(
                "DELETE FROM t_user_inventory WHERE user_id = @userId AND container = @container AND slot = @slot",
                new { userId, container = (int)change.Container, slot = change.Slot });
        }

        return tx.ExecuteAsync(
            @"INSERT INTO t_user_inventory (user_id, container, slot, item_id, count)
              VALUES (@userId, @container, @slot, @itemId, @count)
              ON CONFLICT (user_id, container, slot) DO UPDATE SET item_id = excluded.item_id, count = excluded.count;",
            new
            {
                userId, container = (int)change.Container, itemId = change.ItemId,
                count  = change.Count, slot = change.Slot,
            });
    }

    public void Apply()
    {
    }
}
