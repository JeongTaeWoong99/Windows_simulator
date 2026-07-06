using MikaProtocol;

namespace WSGameServer.User.Inventory;

public sealed class Inventory
{
    private Dictionary<int, Item> _items = new();

    // 로그인 시 DB에서 읽은 ItemInfo(네트워크/DTO)를 도메인 모델 Item으로 변환해 적재
    public void Load(IEnumerable<ItemInfo> itemInfos)
    {
        _items = itemInfos.ToDictionary(
            info => info.ItemId,
            info => new Item(info.ItemId, info.Count));
    }
    
    public ItemChangeInfo AddItem(int itemId, int count)
    {
        if (_items.TryGetValue(itemId, out var item))
        {
            item.Count += count;
            return new ItemChangeInfo
            {
                ItemId = itemId, Count = item.Count, Kind = EItemChangeKind.Update
            };
        }

        var added = new Item(itemId, count);
        _items[itemId] = added;
        return new ItemChangeInfo
        {
            ItemId = itemId, Count = added.Count, Kind = EItemChangeKind.Add
        };
    }
}