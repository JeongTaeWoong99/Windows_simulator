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
    // ※ 산업별로 걸러 내지 않는다 — 칸 200개를 탭이 함께 쓰는 격자에서 일부만 보이면 빈 칸이 "사라진 아이템"처럼 읽힌다.
    //   정리는 [정렬] 한 번으로 한다('CompareForSort').
    protected override void Fill(List<SlotData> into)
    {
        foreach (ItemInfo item in _data.Inventory)
        {
            // 서버가 0개가 된 아이템도 실어 보낸다(감소도 같은 경로로 온다). 화면에서는 뺀다.
            if (item.Count <= 0)
            {
                continue;
            }

            into.Add(new SlotData(
                item.ItemId,
                GameDataLoader.GetItemName(item.ItemId),
                $"{item.Count} 개", // 숫자만 두면 수량인지 등급인지 레벨인지 칸만 보고 알 수 없다
                GameDataLoader.GetItemRarity(item.ItemId)));
        }
    }

    // 자원 [정렬] 규칙 — 등급 높은 순 → 산업 순 → TID 순 (Sort에서 호출).
    //
    // 산업 순은 'ItemType' 값 순서다 — 농사·낚시·채광·벌목·사냥·기타·특수로, 배치 화면의 산업 버튼·적성 스트립과 같다.
    // TID 대역(낚시 1xxxx · 농사 2xxxx)으로 바로 줄 세우면 낚시가 농사보다 앞에 와서 두 화면의 순서가 어긋난다.
    // ※ enum끼리 'CompareTo'를 부르면 박싱이 일어나 숫자로 바꿔 비교한다.
    protected override int CompareForSort(SlotData a, SlotData b)
    {
        int byRarity = ((byte)b.Rarity).CompareTo((byte)a.Rarity);

        if (byRarity != 0)
        {
            return byRarity;
        }

        int itemA = (int)a.Key;
        int itemB = (int)b.Key;

        int byType = ((byte)GameDataLoader.GetItemType(itemA)).CompareTo((byte)GameDataLoader.GetItemType(itemB));

        if (byType != 0)
        {
            return byType;
        }

        return itemA.CompareTo(itemB);
    }

    // 자원 칸 툴팁 — 등급 · 보유 · 판매가(개당 · 전부) · 조작 안내 (격자가 칸에 올린 순간 호출).
    //
    // ※ 판매가를 합계와 함께 적는다 — 칸에는 수량만 있어 "이 칸을 다 팔면 얼마인가"를 알 길이 없었다.
    // ※ 조작 안내를 한 줄 둔다 — 좌클릭(상자 개봉)·우클릭(판매 담기)은 칸에 아무 표시가 없어 눌러 봐야 안다.
    // ⚠️ 엑셀 'Description'은 쓰지 않는다 — 기획 메모 컬럼이라 플레이어용 문구가 아니다(T-093).
    public override TooltipContent? BuildTooltip(long key)
    {
        int itemId = (int)key;
        int count  = _data.GetItemCount(itemId);

        if (count <= 0)
        {
            return null; // 화면이 아직 낡았다 — 뒤이어 올 InventoryChanged가 이 칸을 지운다
        }

        long price   = GameDataLoader.GetItemPrice(itemId);
        var  content = new TooltipContent(GameDataLoader.GetItemName(itemId));

        AddRarityRow(content, GameDataLoader.GetItemRarity(itemId))
            .Row("보유", $"{count:N0} 개")
            .Row("판매가", $"{price:N0} 골드", $"전부 {price * count:N0} 골드", null);

        content.Row(GameDataLoader.IsBox(itemId) ? "좌클릭 열기 · 우클릭 판매 담기" : "우클릭 판매 담기", "");

        return content;
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
