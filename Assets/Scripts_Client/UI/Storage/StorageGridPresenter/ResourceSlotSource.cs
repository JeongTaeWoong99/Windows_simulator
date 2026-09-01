using System.Collections.Generic;
using MikaProtocol;

// 자원 탭 — 인벤토리 아이템을 칸으로 내놓는다. 창고의 기본 탭이다.
//
// 수량·등급의 주인은 서버와 테이블이다. 이 공급자는 그것을 화면 문구로 옮기기만 한다.
public class ResourceSlotSource : StorageSlotSource
{
    private readonly PlayerDataModel _data;

    public ResourceSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 아이템을 칸으로 옮긴다 (Rebuild에서 호출).
    //
    // ★ 산업별 걸러 내기·정렬이 붙을 자리가 여기다 (T-015).
    //   'ItemTable.ItemType'이 이미 산업 축이라 조건 몇 줄이면 되고,
    //   격자는 받은 순서대로 그리므로 격자 쪽은 고치지 않는다.
    protected override void Fill(List<StorageSlotData> into)
    {
        foreach (ItemInfo item in _data.Inventory)
        {
            // 서버가 0개가 된 아이템도 실어 보낸다(감소도 같은 경로로 온다). 화면에서는 뺀다.
            if (item.Count <= 0)
            {
                continue;
            }

            into.Add(new StorageSlotData(
                item.ItemId,
                GameDataLoader.GetItemName(item.ItemId),
                item.Count.ToString(),
                GameDataLoader.GetItemRarity(item.ItemId)));
        }
    }

    // 인벤토리 변경 구독 (Subscribe에서 호출)
    protected override void OnSubscribe()
    {
        _data.InventoryChanged += Rebuild;
    }

    // 구독 해제 (Unsubscribe에서 호출)
    protected override void OnUnsubscribe()
    {
        _data.InventoryChanged -= Rebuild;
    }
}
