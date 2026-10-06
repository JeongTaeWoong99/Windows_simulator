using System.Collections.Generic;
using GameData;
using MikaProtocol;

// 자원 탭 — 인벤토리 아이템을 칸으로 내놓는다. 인벤토리의 기본 탭이다.
//
// 수량·등급의 주인은 서버와 테이블이다. 이 공급자는 그것을 화면 문구로 옮기기만 한다.
public class ResourceSlotSource : InventorySlotSource
{
    private readonly PlayerDataModel _data;

    public ResourceSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 아이템을 서버 칸 번호와 함께 칸으로 옮긴다 (Rebuild에서 호출).
    //
    // ※ 여기서 거르지 않는다 — 찾기(검색·필터)는 기반 클래스가 칸 번호와 떼어 따로 한다('InventorySlotSource' 주석).
    // ※ 창고(T-107)에 맡긴 것은 인벤토리 격자에 그리지 않는다 — 같은 TID가 두 보관함에 따로 있을 수 있다.
    protected override void Fill(List<PlacedSlot> into)
    {
        foreach (ItemInfo item in _data.Inventory)
        {
            // 서버가 0개가 된 아이템도 실어 보낸다(감소도 같은 경로로 온다). 화면에서는 뺀다.
            if (item.Count <= 0 || item.Container != EContainer.Inventory)
            {
                continue;
            }

            var slot = new SlotData(
                item.ItemId,
                GameDataLoader.GetItemName(item.ItemId),
                $"{item.Count} 개", // 숫자만 두면 수량인지 등급인지 레벨인지 칸만 보고 알 수 없다
                GameDataLoader.GetItemRarity(item.ItemId),
                VisualCatalog.ItemIconOf(item.ItemId));

            into.Add(new PlacedSlot(slot, item.Slot));
        }
    }

    // 자원만 보유 수량으로 줄 세울 수 있다 — 캐릭터·장비는 개체라 수량이 늘 1이다 (도구 줄이 호출).
    public override bool SupportsSortKey(InventorySortKey key) => true;

    // 자원의 산업 = 'ItemType' (Matches에서 호출).
    // ※ 기타·특수(상자 등)는 산업이 아니다 — 분류 드롭다운의 '상자' · '기타'로 따로 거른다('MatchesGroup').
    //   'ItemType'과 'IndustryType'은 1~5가 같은 값이다.
    protected override bool MatchesIndustry(SlotData slot, byte industry)
        => (byte)GameDataLoader.GetItemType((int)slot.Key) == industry;

    // 자원 탭은 상자 · 기타 묶음으로도 거른다 (2026-10-05).
    public override bool SupportsGroupFilter => true;

    // 상자 = 열 수 있는 것('OpenGachaId' — 서버와 같은 판정). 기타 = 산업 자원도 상자도 아닌 것(큐브 · 구슬 등) (Matches에서 호출).
    protected override bool MatchesGroup(SlotData slot, InventoryItemGroup group)
    {
        int  itemId = (int)slot.Key;
        bool isBox  = GameDataLoader.IsBox(itemId);

        return group switch
        {
            InventoryItemGroup.Box   => isBox,
            InventoryItemGroup.Other => !isBox && !IsIndustryItem(itemId),
            _                        => true,
        };
    }

    // 산업 자원인가 — 'ItemType' 1~5(농사 ~ 사냥). 기타 · 특수는 아니다.
    private static bool IsIndustryItem(int itemId)
    {
        ItemType type = GameDataLoader.GetItemType(itemId);

        return type >= ItemType.Farming && type <= ItemType.Hunting;
    }

    // 자원 칸 툴팁 — 등급 · 보유 · 즉시 판매가(개당 · 전부) · 경매 등록가 범위 · 조작 안내 (격자가 칸에 올린 순간 호출).
    //
    // ※ 두 가격을 나란히 적는다 — 우클릭 판매는 즉시 판매가(정해진 값), 경매장은 그 값 이상에서 직접 정한다.
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

        int  basePrice = GameDataLoader.GetItemPrice(itemId);
        long price     = AuctionModel.InstantSellPrice(basePrice);
        var  content   = new TooltipContent(GameDataLoader.GetItemName(itemId));

        AddRarityRow(content, GameDataLoader.GetItemRarity(itemId))
            .Row("보유", $"{count:N0} 개")
            .Row("즉시 판매가", $"{price:N0} 골드", $"전부 {AuctionModel.InstantSellTotal(basePrice, count):N0} 골드", null)
            .Row("경매 등록가", AuctionModel.FormatBand(basePrice));

        content.Row(GameDataLoader.IsBox(itemId) ? "좌클릭 열기 · 우클릭 판매 담기" : "우클릭 판매 담기", "")
               .Row("Shift+우클릭 경매 등록", "");

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
