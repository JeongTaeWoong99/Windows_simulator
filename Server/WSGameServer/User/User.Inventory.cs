using MikaProtocol;

namespace WSGameServer.User;

public partial class User
{
    public void AddItem(int itemId, int count)
    {
        var itemChangeInfo = Inventory.AddItem(itemId, count);
        PostDBTask<ItemChangeInfo>(this, itemChangeInfo);
    }
}