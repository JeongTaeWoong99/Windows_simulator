using MikaProtocol;
using WSGameServer;

namespace WSGameServer;

public partial class User
{
    /// <summary>DB에서 읽은 인벤토리 Row를 도메인(Item)으로 변환해 적재한다(로그인 시 1회).</summary>
    private void LoadInventory(IReadOnlyList<InventoryRow> rows)
    {
        Inventory.Load(rows.Select(r => new Item(r.item_id, r.count)));
    }

    // 현재 인벤토리 전체 스냅샷을 클라이언트로 전송한다.
    public void SendInventory()
    {
        Send(new S_InventoryResponse { Items = Inventory.Snapshot() });
    }

    // 패킷 전송 없이 메모리 갱신 + DB 반영만 수행하고 변경분을 반환한다.
    // 호출자(AddItem, GachaService 등)가 응답 구성을 담당한다.
    public ItemChangeInfo GainItem(int itemId, int count)
    {
        var itemChangeInfo = Inventory.AddItem(itemId, count);

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