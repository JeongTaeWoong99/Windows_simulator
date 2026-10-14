using MikaProtocol;
using WSGameServer;

namespace WSGameServer;

public partial class User
{
    /// <summary>DB에서 읽은 인벤토리 Row를 도메인(Item)으로 변환해 적재한다(로그인 시 1회).</summary>
    public void LoadInventory(IReadOnlyList<InventoryRow> rows)
    {
        Inventory.Load(rows.Select(r => new Item(r.item_id, r.count, r.slot)));
    }

    // 현재 인벤토리 전체 스냅샷을 클라이언트로 전송한다.
    public void SendInventory()
    {
        Send(new S_InventoryResponse { Items = Inventory.Snapshot() });
    }

    // 메모리 갱신 + DB 반영만 하고 변경분을 돌려준다(응답은 호출자가). 지급 경로는 전부 여기를 지난다.
    // 최대 수량(MaxStack)을 넘는 몫은 버린다 — 수확·우편·상자·뽑기 모두 같다(T-157).
    public ItemChangeInfo GainItem(int itemId, int count)
    {
        var granted        = ClampToMaxStack(itemId, count);
        var itemChangeInfo = Inventory.AddItem(itemId, granted);
        if (granted <= 0)
        {
            return itemChangeInfo;
        }

        PostDBTask<AddItemRepository>(new (this, itemChangeInfo));
        return itemChangeInfo;
    }

    /// <summary>보유 수량. 없으면 0.</summary>
    public int GetItemCount(int itemId) => Inventory.GetCount(itemId);

    /// <summary>
    /// 아이템을 한꺼번에 뺀다(상자 개봉 등). 하나라도 모자라면 아무것도 바꾸지 않고 false.
    /// 통지는 호출자가 응답에 싣는다 — 여기서는 메모리 갱신과 저장만 한다.
    /// </summary>
    public bool TryConsumeItems(IReadOnlyDictionary<int, int> request, out List<ItemChangeInfo> changes)
    {
        if (!Inventory.TryRemoveItems(request, out changes))
        {
            return false;
        }

        PostDBTask(new SaveItemChangesRepository(this, changes));
        return true;
    }

    public void AddItem(int itemId, int count)
    {
        var itemChangeInfo = GainItem(itemId, count);

        Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = [itemChangeInfo] });
    }
}