using MikaProtocol;

namespace WSGameServer;

/// <summary>한 보관함(인벤토리 또는 창고)의 자원 탭. 같은 자원은 보관함마다 칸 하나다.</summary>
public sealed class Inventory(EContainer container = EContainer.Inventory)
{
    private Dictionary<int, Item> _items = new();

    public EContainer Container { get; } = container;

    // 로그인 시 아이템을 적재한다. 칸이 겹치면(마이그레이션 전 행 등) 뒤의 것을 첫 빈 칸으로 옮긴다 —
    // 겹친 채 두면 클라 격자에서 하나가 가려진다. 옮긴 칸은 다음 증분 저장에 함께 실린다.
    public void Load(IEnumerable<Item> items)
    {
        // 겹치지 않은 칸을 먼저 전부 잡는다 — 한 번에 돌면 겹친 것이 뒤 항목의 제 칸을 빼앗는다.
        _items = new Dictionary<int, Item>();
        var used     = new HashSet<int>();
        var overlaps = new List<Item>();
        foreach (var item in items)
        {
            _items[item.Id] = item;
            if (!used.Add(item.Slot))
            {
                overlaps.Add(item);
            }
        }

        foreach (var item in overlaps)
        {
            item.Slot = StorageSlots.FirstFree(used);
            used.Add(item.Slot);
        }
    }

    public IReadOnlyCollection<Item> Items => _items.Values;

    public bool TryGet(int itemId, out Item item) => _items.TryGetValue(itemId, out item!);

    // 현재 인벤토리 전체를 네트워크 전송용 ItemInfo 목록으로 변환한다.
    public List<ItemInfo> Snapshot()
    {
        return _items.Values
            .Select(item => new ItemInfo { ItemId = item.Id, Count = item.Count, Container = Container, Slot = item.Slot })
            .ToList();
    }
    
    /// <summary>보유 종류 수 — 0개짜리 항목은 지워지므로 곧 창고 칸 수다.</summary>
    public int KindCount => _items.Count;

    /// <summary>보유 수량. 없으면 0.</summary>
    public int GetCount(int itemId) => _items.TryGetValue(itemId, out var item) ? item.Count : 0;

    public ItemChangeInfo AddItem(int itemId, int count)
    {
        if (_items.TryGetValue(itemId, out var item))
        {
            item.Count += count;
            return new ItemChangeInfo
            {
                ItemId = itemId, Count = item.Count, Kind = EItemChangeKind.Update, Container = Container, Slot = item.Slot
            };
        }

        var added = new Item(itemId, count, StorageSlots.FirstFree(_items.Values.Select(i => i.Slot)));
        _items[itemId] = added;
        return new ItemChangeInfo
        {
            ItemId = itemId, Count = added.Count, Kind = EItemChangeKind.Add, Container = Container, Slot = added.Slot
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
                    ItemId = itemId, Count = 0, Kind = EItemChangeKind.Remove, Container = Container, Slot = item.Slot
                });
                continue;
            }

            changes.Add(new ItemChangeInfo
            {
                ItemId = itemId, Count = item.Count, Kind = EItemChangeKind.Update, Container = Container, Slot = item.Slot
            });
        }

        return true;
    }

    /// <summary>그 칸의 자원. 비었으면 false.</summary>
    public bool TryGetAt(int slot, out Item item)
    {
        item = _items.Values.FirstOrDefault(i => i.Slot == slot)!;
        return item is not null;
    }

    /// <summary>
    /// 수량·칸을 그대로 덮어쓴다(창고 이동). 0이면 지운다 — 칸이 빈다.
    /// MaxStack·칸 수 검사는 부른 쪽이 먼저 끝낸다.
    /// </summary>
    public ItemChangeInfo SetItem(int itemId, int count, int slot)
    {
        if (count == 0)
        {
            _items.Remove(itemId);
            return new ItemChangeInfo { ItemId = itemId, Count = 0, Kind = EItemChangeKind.Remove, Container = Container, Slot = slot };
        }

        var kind = _items.ContainsKey(itemId) ? EItemChangeKind.Update : EItemChangeKind.Add;
        _items[itemId] = new Item(itemId, count, slot);
        return new ItemChangeInfo { ItemId = itemId, Count = count, Kind = kind, Container = Container, Slot = slot };
    }
}
