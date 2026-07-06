using System.Data;
using Dapper;
using MikaProtocol;
using WSGameServer.User.Inventory;

namespace WSGameServer.Repository;

public class AddItemRepository : IRepository
{
    public long Key { get; }
    
    public User.User User { get; init; }
    public ItemChangeInfo ItemChangeInfo { get; init; }

    public AddItemRepository(User.User user, ItemChangeInfo itemChangeInfo)
    {
        User = user;
        ItemChangeInfo = itemChangeInfo;
    }
    
    public async Task ExecuteAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_inventory (user_id, item_id, count)
              VALUES (@userId, @itemId, @count)
              ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count;",
            new { userId = User.Uid, itemId = ItemChangeInfo.ItemId, count = ItemChangeInfo.Count });
    }

    public void Apply()
    {
        
    }
}