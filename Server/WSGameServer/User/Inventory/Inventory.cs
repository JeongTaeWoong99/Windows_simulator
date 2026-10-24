using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 한 보관함(인벤토리 또는 창고)의 자원 탭. 칸 하나 = 묶음 하나이고, 같은 자원도 MaxStack을 넘으면 여러 칸에 나뉜다(T-160).
/// </summary>
public sealed class Inventory
{
    private readonly Func<int> _capacity;

    private Dictionary<int, Item> _slots = new();

    // 칸 수는 쓸 때 읽는다 — User가 표(Constants)보다 먼저 만들어질 수 있다.
    public Inventory(EContainer container, Func<int> capacity)
    {
        Container = container;
        _capacity = capacity;
    }

    public Inventory(EContainer container = EContainer.Inventory, int capacity = int.MaxValue)
        : this(container, () => capacity)
    {
    }

    public EContainer Container { get; }

    public int Capacity => _capacity();

    // 로그인 시 묶음을 적재한다. 칸이 겹치거나 격자 밖이면(마이그레이션이 비켜 둔 행 등) 첫 빈 칸으로 옮긴다 —
    // 그대로 두면 클라 격자에서 하나가 가려진다. 옮긴 칸은 다음 저장에 함께 실린다.
    public void Load(IEnumerable<Item> items)
    {
        // 제자리인 칸을 먼저 전부 잡는다 — 한 번에 돌면 비켜 갈 것이 뒤 항목의 제 칸을 빼앗는다.
        _slots = new Dictionary<int, Item>();
        var misplaced = new List<Item>();
        foreach (var item in items)
        {
            if (!IsInGrid(item.Slot) || !_slots.TryAdd(item.Slot, item))
            {
                misplaced.Add(item);
            }
        }

        foreach (var item in misplaced)
        {
            var slot = StorageSlots.FirstFree(_slots.Keys);
            _slots[slot] = new Item(item.Id, item.Count, slot);
        }
    }

    public IReadOnlyCollection<Item> Items => _slots.Values;

    public bool TryGetAt(int slot, out Item item) => _slots.TryGetValue(slot, out item!);

    public List<ItemInfo> Snapshot()
    {
        return _slots.Values
            .Select(item => new ItemInfo { ItemId = item.Id, Count = item.Count, Container = Container, Slot = item.Slot })
            .ToList();
    }

    /// <summary>찬 칸 수 — 0개짜리 묶음은 지워지므로 곧 쓰는 칸 수다.</summary>
    public int SlotCount => _slots.Count;

    /// <summary>보유 수량 — 그 자원의 칸들을 합친다. 없으면 0.</summary>
    public int GetCount(int itemId) => _slots.Values.Where(i => i.Id == itemId).Sum(i => i.Count);

    /// <summary>더 받을 수 있는 수량 — 덜 찬 칸의 남은 자리 + 빈 칸 × maxStack.</summary>
    public long RoomFor(int itemId, int maxStack)
    {
        var partial = _slots.Values.Where(i => i.Id == itemId).Sum(i => (long)Math.Max(0, maxStack - i.Count));
        return partial + (long)FreeSlotCount * maxStack;
    }

    // capacity가 int.MaxValue(테스트 기본)여도 넘치지 않게 뺀다.
    private int FreeSlotCount => Math.Max(0, Capacity - _slots.Count);

    /// <summary>
    /// 덜 찬 칸부터 maxStack까지 채우고, 남으면 첫 빈 칸부터 새 묶음을 놓는다. 칸이 모자라 못 넣은 몫은 버린다.
    /// 변경분은 칸마다 한 줄이고 Count는 그 칸의 갱신 후 수량이다.
    /// </summary>
    public List<ItemChangeInfo> AddItem(int itemId, int count, int maxStack, out int added)
    {
        var changes = TopUp(itemId, count, maxStack, out var toppedUp);
        var left    = maxStack > 0 ? Math.Max(0, count) - toppedUp : 0;

        while (left > 0 && FreeSlotCount > 0)
        {
            var put  = Math.Min(left, maxStack);
            var item = new Item(itemId, put, StorageSlots.FirstFree(_slots.Keys));
            _slots[item.Slot] = item;
            left -= put;
            changes.Add(ToChange(item, EItemChangeKind.Add));
        }

        added = maxStack > 0 ? Math.Max(0, count) - left : 0;
        return changes;
    }

    /// <summary>같은 자원의 덜 찬 칸만 칸 번호 앞부터 maxStack까지 채운다. 빈 칸에 새 묶음은 만들지 않는다.</summary>
    public List<ItemChangeInfo> TopUp(int itemId, int count, int maxStack, out int added)
    {
        var changes = new List<ItemChangeInfo>();
        var left    = maxStack > 0 ? Math.Max(0, count) : 0;

        foreach (var item in _slots.Values.Where(i => i.Id == itemId && i.Count < maxStack).OrderBy(i => i.Slot).ToList())
        {
            if (left == 0)
            {
                break;
            }

            var put = Math.Min(left, maxStack - item.Count);
            item.Count += put;
            left       -= put;
            changes.Add(ToChange(item, EItemChangeKind.Update));
        }

        added = maxStack > 0 ? Math.Max(0, count) - left : 0;
        return changes;
    }

    /// <summary>
    /// 요청한 수량을 한꺼번에 뺀다. <b>하나라도 모자라면 아무것도 바꾸지 않고 false를 돌려준다</b> —
    /// 판매는 골드 지급과 짝이라, 부분 차감이 남으면 아이템만 사라진다.
    /// </summary>
    public bool TryRemoveItems(IReadOnlyDictionary<int, int> request, out List<ItemChangeInfo> changes)
    {
        changes = new List<ItemChangeInfo>();

        // 검증을 먼저 전부 끝낸다 — 검증과 차감을 한 번에 돌면 중간에 걸렸을 때 앞부분이 이미 빠져 있다.
        foreach (var (itemId, count) in request)
        {
            if (count <= 0 || GetCount(itemId) < count)
            {
                return false;
            }
        }

        foreach (var (itemId, count) in request)
        {
            RemoveFromStacks(itemId, count, changes);
        }

        return true;
    }

    // 덜 찬 칸부터 뺀다 — 칸이 빨리 빈다. 같은 수량이면 뒤 칸부터라 앞 칸이 자리를 지킨다.
    private void RemoveFromStacks(int itemId, int count, List<ItemChangeInfo> changes)
    {
        var left   = count;
        var stacks = _slots.Values.Where(i => i.Id == itemId).OrderBy(i => i.Count).ThenByDescending(i => i.Slot).ToList();
        foreach (var item in stacks)
        {
            if (left == 0)
            {
                break;
            }

            var taken = Math.Min(left, item.Count);
            item.Count -= taken;
            left       -= taken;

            // 0개짜리 묶음을 남기면 스냅샷에 빈 칸으로 실려 나간다. 지우고 Remove로 알린다.
            if (item.Count == 0)
            {
                _slots.Remove(item.Slot);
                changes.Add(ToChange(item, EItemChangeKind.Remove));
                continue;
            }

            changes.Add(ToChange(item, EItemChangeKind.Update));
        }
    }

    /// <summary>
    /// 두 보관함에 걸쳐 뺀다 — first에서 먼저, 모자란 만큼 second에서(T-164). 합쳐도 모자라면 아무것도 바꾸지 않는다.
    /// </summary>
    public static bool TryRemoveAcross(Inventory first, Inventory second, IReadOnlyDictionary<int, int> request,
        out List<ItemChangeInfo> changes)
    {
        changes = new List<ItemChangeInfo>();

        var fromFirst  = new Dictionary<int, int>();
        var fromSecond = new Dictionary<int, int>();
        foreach (var (itemId, count) in request)
        {
            if (count <= 0 || first.GetCount(itemId) + second.GetCount(itemId) < count)
            {
                return false;
            }

            var taken = Math.Min(count, first.GetCount(itemId));
            if (taken > 0)
            {
                fromFirst[itemId] = taken;
            }
            if (count > taken)
            {
                fromSecond[itemId] = count - taken;
            }
        }

        // 위에서 양쪽 수량을 다 확인했으므로 둘 다 성공한다.
        first.TryRemoveItems(fromFirst, out var firstChanges);
        second.TryRemoveItems(fromSecond, out var secondChanges);
        changes.AddRange(firstChanges);
        changes.AddRange(secondChanges);
        return true;
    }

    /// <summary>칸 하나를 그대로 덮어쓴다(창고 이동). 0이면 칸을 비운다. MaxStack·칸 수 검사는 부른 쪽이 먼저 끝낸다.</summary>
    public ItemChangeInfo SetSlot(int slot, int itemId, int count)
    {
        if (count == 0)
        {
            _slots.Remove(slot);
            return new ItemChangeInfo { ItemId = itemId, Count = 0, Kind = EItemChangeKind.Remove, Container = Container, Slot = slot };
        }

        var kind = _slots.ContainsKey(slot) ? EItemChangeKind.Update : EItemChangeKind.Add;
        _slots[slot] = new Item(itemId, count, slot);
        return ToChange(_slots[slot], kind);
    }

    /// <summary>칸 전체를 next로 바꾸고 달라진 칸만 돌려준다(자리 이동·정렬). 비게 된 칸은 Count 0이다.</summary>
    public List<ItemChangeInfo> Rewrite(IReadOnlyCollection<Item> next)
    {
        var before  = _slots;
        var changes = new List<ItemChangeInfo>();

        _slots = next.ToDictionary(i => i.Slot, i => new Item(i.Id, i.Count, i.Slot));
        foreach (var item in _slots.Values)
        {
            if (!before.TryGetValue(item.Slot, out var old))
            {
                changes.Add(ToChange(item, EItemChangeKind.Add));
            }
            else if (old.Id != item.Id || old.Count != item.Count)
            {
                changes.Add(ToChange(item, EItemChangeKind.Update));
            }
        }

        foreach (var old in before.Values.Where(o => !_slots.ContainsKey(o.Slot)))
        {
            changes.Add(new ItemChangeInfo { ItemId = old.Id, Count = 0, Kind = EItemChangeKind.Remove, Container = Container, Slot = old.Slot });
        }

        return changes;
    }

    /// <summary>같은 자원을 칸 번호 앞부터 maxStack까지 다시 채운다. 칸 순서는 그대로이고 비게 된 뒤 칸은 Count 0으로 알린다.</summary>
    public List<ItemChangeInfo> Merge(Func<int, int> maxStackOf)
    {
        var next = new List<Item>();
        foreach (var group in _slots.Values.GroupBy(i => i.Id))
        {
            var maxStack = maxStackOf(group.Key);
            var left     = group.Sum(i => (long)i.Count);
            foreach (var slot in group.Select(i => i.Slot).Order())
            {
                if (left == 0)
                {
                    break;
                }

                var put = (int)Math.Min(left, maxStack);
                next.Add(new Item(group.Key, put, slot));
                left -= put;
            }
        }

        return Rewrite(next);
    }

    /// <summary>칸 수 검사용 사본 — 지급을 미리 넣어 보고 버린다.</summary>
    public Inventory Clone()
    {
        var copy = new Inventory(Container, _capacity);
        copy._slots = _slots.Values.ToDictionary(i => i.Slot, i => new Item(i.Id, i.Count, i.Slot));
        return copy;
    }

    private bool IsInGrid(int slot) => slot >= 0 && slot < Capacity;

    private ItemChangeInfo ToChange(Item item, EItemChangeKind kind)
        => new() { ItemId = item.Id, Count = item.Count, Kind = kind, Container = Container, Slot = item.Slot };
}
