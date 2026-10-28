using GameData;

namespace WSGameServer;

public partial class User
{
    /// <summary>창고 탭마다의 칸 수(자원·캐릭터·장비 각각) — Constants.xlsx. 클라 칸 프레임 수와 같아야 한다.</summary>
    public static int StorageCapacity => (int)Constants.StorageCapacity;

    // DB가 PK를 발급(또는 우편 잠금 해제)하기 전의 캐릭터 칸. 세지 않으면 응답 전 연속 뽑기가 한도와 칸을 지나친다.
    private readonly HashSet<int> _pendingCharacterSlots = new();

    /// <summary>자원 칸 — 찬 칸 수. 같은 자원도 MaxStack을 넘으면 여러 칸이다(T-160).</summary>
    public int ItemSlotsUsed => Inventory.SlotCount;

    /// <summary>캐릭터 칸 — 보유 개체 + 지급 대기 중인 개체</summary>
    public int CharacterSlotsUsed => _characters.Count + _pendingCharacterSlots.Count;

    /// <summary>장비 칸 — 보유 개체 + 지급 대기 중인 개체 + 같은 격자의 아티팩트 칸(T-188)</summary>
    public int EquipSlotsUsed => _equips.Count + _pendingEquipPositions.Count + Artifacts.SlotCount;

    // 이번 지급이 남은 칸 안에 전부 드는가. 자원은 사본에 넣어 본다 — 덜 찬 칸을 채우고 남는 만큼만 새 칸을 쓴다.
    // consumed — 지급 전에 빠지는 자원(상자를 여는 경우). 인벤토리 것부터 빠지므로 그만큼 칸이 빌 수 있다.
    public bool HasStorageFor(IEnumerable<(int Tid, int Count)> items, int characterCount, int equipCount,
        IReadOnlyDictionary<int, int>? consumed = null)
    {
        return ItemsFit(items, consumed) &&
               CharacterSlotsUsed + characterCount <= StorageCapacity &&
               EquipSlotsUsed + equipCount <= StorageCapacity;
    }

    // 창고 사본도 함께 쓴다 — 지급이 창고의 같은 자원 칸부터 채우므로(T-175) 그 몫은 인벤토리 칸을 쓰지 않는다.
    private bool ItemsFit(IEnumerable<(int Tid, int Count)> items, IReadOnlyDictionary<int, int>? consumed)
    {
        var inventory = Inventory.Clone();
        var warehouse = Warehouse.Clone();
        foreach (var (tid, count) in consumed ?? new Dictionary<int, int>())
        {
            var fromInventory = Math.Min(count, inventory.GetCount(tid));
            var taken         = fromInventory + Math.Min(count - fromInventory, warehouse.GetCount(tid));
            if (taken > 0)
            {
                Inventory.TryRemoveAcross(inventory, warehouse, new Dictionary<int, int> { [tid] = taken }, out _);
            }
        }

        foreach (var group in items.GroupBy(i => i.Tid))
        {
            var count = group.Sum(i => i.Count);
            Deposit(warehouse, inventory, group.Key, count, out var added);
            if (added < count)
            {
                return false;
            }
        }

        return true;
    }

    // 칸 하나의 최대 수량(ItemTable.MaxStack). 표에 없는 TID는 상한 없이 둔다.
    public static int MaxStackOf(int itemTid)
    {
        if (!GameTable.ItemTable.TryGet(itemTid, out var row) || row.MaxStack <= 0)
        {
            return int.MaxValue;
        }

        return row.MaxStack;
    }
}
