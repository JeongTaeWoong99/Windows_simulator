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
