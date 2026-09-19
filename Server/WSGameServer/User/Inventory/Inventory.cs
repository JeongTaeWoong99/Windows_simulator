using MikaProtocol;

namespace WSGameServer;

public sealed class Inventory
{
    private Dictionary<int, Item> _items = new();

    // 로그인 시 아이템을 적재한다. Row → Item 변환은 호출자(User.OnLoginDataLoaded)가 끝냈다 —
    // 인벤토리는 Repository의 Row도 네트워크 DTO도 모른다.
    public void Load(IEnumerable<Item> items)
    {
        _items = items.ToDictionary(item => item.Id);
    }

    // 현재 인벤토리 전체를 네트워크 전송용 ItemInfo 목록으로 변환한다.
    public List<ItemInfo> Snapshot()
    {
        return _items.Values
            .Select(item => new ItemInfo { ItemId = item.Id, Count = item.Count })
            .ToList();
    }
    
    /// <summary>보유 수량. 없으면 0.</summary>
    public int GetCount(int itemId) => _items.TryGetValue(itemId, out var item) ? item.Count : 0;

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

    /// <summary>
    /// 요청한 수량을 한꺼번에 뺀다. <b>하나라도 모자라면 아무것도 바꾸지 않고 false를 돌려준다</b> —
    /// 판매는 골드 지급과 짝이라, 부분 차감이 남으면 아이템만 사라진다.
    /// </summary>
    /// <returns>전부 차감했으면 true.</returns>
    public bool TryRemoveItems(IReadOnlyDictionary<int, int> request, out List<ItemChangeInfo> changes)
    {
        changes = new List<ItemChangeInfo>(request.Count);

        // 검증을 먼저 전부 끝낸다 — 검증과 차감을 한 번에 돌면 중간에 걸렸을 때 앞부분이 이미 빠져 있다.
        foreach (var (itemId, count) in request)
        {
            if (count <= 0)
            {
                return false;
            }

            if (!_items.TryGetValue(itemId, out var item) || item.Count < count)
            {
                return false;
            }
        }

        foreach (var (itemId, count) in request)
        {
            var item = _items[itemId];
            item.Count -= count;

            // 0개짜리 항목을 남기면 스냅샷에 빈 칸으로 실려 나간다. 목록에서 지우고 Remove로 알린다.
            if (item.Count == 0)
            {
                _items.Remove(itemId);
                changes.Add(new ItemChangeInfo
                {
                    ItemId = itemId, Count = 0, Kind = EItemChangeKind.Remove
                });
                continue;
            }

            changes.Add(new ItemChangeInfo
            {
                ItemId = itemId, Count = item.Count, Kind = EItemChangeKind.Update
            });
        }

        return true;
    }
}