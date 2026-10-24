using MikaProtocol;
using WSGameServer;

namespace WSGameServer;

public partial class User
{
    /// <summary>DB에서 읽은 자원 Row를 보관함별로 적재한다(로그인 시 1회).</summary>
    public void LoadInventory(IReadOnlyList<InventoryRow> rows)
    {
        Inventory.Load(rows.Where(r => r.container == (int)EContainer.Inventory).Select(r => new Item(r.item_id, r.count, r.slot)));
        Warehouse.Load(rows.Where(r => r.container == (int)EContainer.Warehouse).Select(r => new Item(r.item_id, r.count, r.slot)));
    }

    // 자원 스냅샷 — 인벤토리와 창고를 한 목록에 싣는다. 클라가 Container로 가른다.
    public void SendInventory()
    {
        Send(new S_InventoryResponse
        {
            Items          = Inventory.Snapshot().Concat(Warehouse.Snapshot()).ToList(),
            LockedItemTids = _lockedItemTids.Order().ToList(),
        });
    }

    // 메모리 갱신 + DB 반영만 하고 변경분(칸마다 한 줄)을 돌려준다(응답은 호출자가). 지급 경로는 전부 여기를 지난다.
    // 창고의 덜 찬 같은 자원 칸부터 채우고(T-175), 남으면 인벤토리 — 칸이 전부 차서 못 넣은 몫은 버린다(T-160).
    public List<ItemChangeInfo> GainItem(int itemId, int count, out int granted)
    {
        var changes = Deposit(Warehouse, Inventory, itemId, count, out granted);
        if (granted > 0)
        {
            PostDBTask(new AddItemRepository(this, changes));
        }

        return changes;
    }

    public List<ItemChangeInfo> GainItem(int itemId, int count) => GainItem(itemId, count, out _);

    // 창고에는 이미 있는 자원만 쌓는다 — 창고는 플레이어가 고른 것만 두는 곳이다. 칸 수 검사도 사본으로 같은 순서를 밟는다.
    private static List<ItemChangeInfo> Deposit(Inventory warehouse, Inventory inventory, int itemId, int count, out int granted)
    {
        var maxStack = MaxStackOf(itemId);
        var changes  = warehouse.TopUp(itemId, count, maxStack, out var stored);
        changes.AddRange(inventory.AddItem(itemId, count - stored, maxStack, out var carried));

        granted = stored + carried;
        return changes;
    }

    /// <summary>보유 수량 — 인벤토리와 창고를 합친다. 없으면 0.</summary>
    public int GetItemCount(int itemId) => Inventory.GetCount(itemId) + Warehouse.GetCount(itemId);

    // 인벤토리 먼저, 모자란 만큼 창고에서. 하나라도 모자라면 아무것도 바꾸지 않는다 — 판매·경매·상자·큐브가 모두 여기를 지난다.
    private bool TryRemoveItems(IReadOnlyDictionary<int, int> request, out List<ItemChangeInfo> changes)
        => Inventory.TryRemoveAcross(Inventory, Warehouse, request, out changes);

    /// <summary>
    /// 아이템을 한꺼번에 뺀다(상자 개봉 등). 하나라도 모자라면 아무것도 바꾸지 않고 false.
    /// 통지는 호출자가 응답에 싣는다 — 여기서는 메모리 갱신과 저장만 한다.
    /// </summary>
    public bool TryConsumeItems(IReadOnlyDictionary<int, int> request, out List<ItemChangeInfo> changes)
    {
        if (!TryRemoveItems(request, out changes))
        {
            return false;
        }

        PostDBTask(new SaveItemChangesRepository(this, changes));
        return true;
    }

    public void AddItem(int itemId, int count)
    {
        var changes = GainItem(itemId, count);

        Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = changes });
    }
}