using System.Collections.Generic;
using GameData;
using MikaProtocol;

// 자원 탭 — 인벤토리 아이템을 칸으로 내놓는다. 인벤토리의 기본 탭이다.
//
// 수량·등급의 주인은 서버와 테이블이다. 이 공급자는 그것을 화면 문구로 옮기기만 한다.
public class ResourceSlotSource : InventorySlotSource
{
    private readonly PlayerDataModel _data;

    // 이번 'Fill'에서 담은 이 보관함의 아이템별 수량(칸들의 합) — 획득 감지가 읽는다('AmountOf').
    // ※ 'GetItemCount(itemId)'를 쓰지 않는다 — 두 보관함의 합이라 이 격자에 들어온 양과 다르다.
    private readonly Dictionary<long, int> _counts = new Dictionary<long, int>();

    public ResourceSlotSource(PlayerDataModel data, EContainer container) : base(container)
    {
        _data = data;
    }

    // 이 보관함의 아이템을 서버 칸 번호와 함께 칸으로 옮긴다 (Rebuild에서 호출).
    //
    // ※ 여기서 거르지 않는다 — 찾기(검색·필터)는 기반 클래스가 칸 번호와 떼어 따로 한다('InventorySlotSource' 주석).
    // ※ 다른 보관함(인벤토리 ↔ 창고 · T-107)의 것은 그리지 않는다 — 같은 TID가 두 보관함에 따로 있을 수 있다.
    // ※ 칸 하나 = 묶음 하나다(T-160) — 'MaxStack'을 넘으면 같은 아이템이 여러 칸에 선다. 칸마다 그 묶음의 수량을 적는다.
    protected override void Fill(List<PlacedSlot> into)
    {
        _counts.Clear();

        foreach (ItemInfo item in _data.Inventory)
        {
            if (item.Count <= 0 || item.Container != Container)
            {
                continue;
            }

            var slot = new SlotData(
                item.ItemId,
                GameDataLoader.GetItemName(item.ItemId),
                Lang.Format("UI.Inventory.CountUnit", item.Count), // 숫자만 두면 수량인지 등급인지 레벨인지 칸만 보고 알 수 없다
                GameDataLoader.GetItemRarity(item.ItemId),
                VisualCatalog.ItemIconOf(item.ItemId));

            into.Add(new PlacedSlot(slot, item.Slot));
            _counts[item.ItemId] = AmountOf(item.ItemId) + item.Count;
        }
    }

    // 자원은 보유 수량으로 획득을 본다 — 이미 있는 칸에 더 들어와도 반짝인다 (획득 감지가 호출).
    protected override int AmountOf(long key) => _counts.TryGetValue(key, out int count) ? count : 0;

    // 자원은 종류(TID) 단위로 잠근다(T-159) — 같은 TID의 칸은 모두 자물쇠가 함께 켜진다.
    public override bool IsLocked(long key) => _data.IsItemLocked((int)key);

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

    // 큐브는 아이템 분류('ItemType')가 아니라 인챈트 표로 가린다 — 분류보다 먼저 본다. 분류 ID다(화면 이름은 'GroupName').
    public const string CubeGroup = "Cube";

    // 자원의 분류 ID — 큐브면 'Cube', 아니면 아이템 분류 ID(산업 · Box · Relic · Consumable · Etc).
    // 칸 툴팁의 '분류' 줄과 일괄 담기 [제외] 항목이 같은 분류를 쓴다 — 툴팁에서 본 이름을 [제외]에서 그대로 찾게.
    // ※ ID는 언어와 무관하다 — [제외] 체크가 이 값으로 저장된다. 이름은 경매장 분류 이름과 같은 출처다('AuctionText.GetItemGroupName').
    public static string GroupOf(int itemId)
        => GameDataLoader.IsCube(itemId) ? CubeGroup : AuctionText.GetItemCategoryId(GameDataLoader.GetItemType(itemId));

    // 분류 ID의 지금 언어 이름.
    public static string GroupName(string groupId) => AuctionText.GetItemGroupName(groupId);

    // 자원 칸 툴팁 — 등급 · 분류 · 보유(칸 상한) · 보관 · 즉시 판매가(개당 · 전부) · 경매 등록가 범위 · 조작 안내 (격자가 칸에 올린 순간 호출).
    //
    // ※ '보유'는 이 칸의 묶음 수량이고, 같은 아이템이 다른 칸(이 보관함·반대 보관함)에도 있으면 옆에 전체를 적는다(T-160).
    //   판매·경매·개봉은 전체에서 쓴다 — 칸 수량만 보이면 "왜 더 팔리나"가 된다. 판매가 '전부'도 전체 기준이다.
    // ※ 두 가격을 나란히 적는다 — 우클릭 판매는 즉시 판매가(정해진 값), 경매장은 그 값 이상에서 직접 정한다.
    // ※ 판매가를 합계와 함께 적는다 — 칸에는 수량만 있어 "이 칸을 다 팔면 얼마인가"를 알 길이 없었다.
    // ※ 조작 안내를 입력마다 한 줄씩 둔다(입력 이름은 왼쪽 칸에 끝까지) — 좌클릭(상자 개봉)·우클릭(판매 담기)은 칸에 아무 표시가 없어 눌러 봐야 안다.
    // ⚠️ 엑셀 'Description'은 쓰지 않는다 — 기획 메모 컬럼이라 플레이어용 문구가 아니다(T-093).
    public override TooltipContent? BuildTooltip(long key, int slot)
    {
        int       itemId = (int)key;
        ItemInfo? stack  = _data.FindItemAt(Container, slot);
        int       total  = _data.GetItemCount(itemId);

        if (stack == null || stack.ItemId != itemId || total <= 0)
        {
            return null; // 화면이 아직 낡았다 — 뒤이어 올 InventoryChanged가 이 칸을 지운다
        }

        int    basePrice = GameDataLoader.GetItemPrice(itemId);
        long   price     = AuctionModel.InstantSellPrice(basePrice);
        int    maxStack  = GameDataLoader.GetItemMaxStack(itemId);
        string totalText = total > stack.Count ? Lang.Format("UI.Inventory.TotalCount", $"{total:N0}") : "";
        var    content   = new TooltipContent(GameDataLoader.GetItemName(itemId));

        // '보유'에 칸 상한을 붙인다('60 / 9,999 개') — 아이템마다 상한이 달라(1,000 · 9,999) 칸이 언제 넘치는지 보여야 한다.
        string ownedText = maxStack == int.MaxValue
            ? Lang.Format("UI.Inventory.CountUnit", $"{stack.Count:N0}")
            : Lang.Format("UI.Inventory.StackOfMax", $"{stack.Count:N0}", $"{maxStack:N0}");

        AddRarityRow(content, GameDataLoader.GetItemRarity(itemId))
            .Row(Lang.Get("UI.Inventory.Group"), GroupName(GroupOf(itemId)))
            .Row(Lang.Get("UI.Inventory.Owned"), ownedText, totalText, null);

        AddStorageRow(content);

        // 잠긴 종류는 상태 줄로 말한다(캐릭터·장비 칸과 같은 모양) — 판매·경매에서 빠지는 이유가 툴팁에서 보이게.
        bool locked = IsLocked(key);

        if (locked)
        {
            content.Row(Lang.Get("UI.Inventory.State"), Lang.Get("UI.WorkStation.Locked"));
        }

        // 유물은 감정 비용(켜 둔 아티팩트 할인 반영)을 적는다 — 왼쪽 클릭이 골드를 쓴다는 것이 눌러 보기 전에 보여야 한다(T-151)
        long appraiseCost = _data.GetAppraiseCost(itemId);

        if (appraiseCost > 0L)
        {
            content.Row(Lang.Get("UI.Inventory.AppraiseCost"), Lang.Format("UI.Equip.Gold", $"{appraiseCost:N0}"));
        }

        content.Row(Lang.Get("UI.Equip.InstantSellPrice"), Lang.Format("UI.Equip.Gold", $"{price:N0}"),
                    Lang.Format("UI.Inventory.AllGold", $"{AuctionModel.InstantSellTotal(basePrice, total):N0}"), null)
               .Row(Lang.Get("UI.Equip.AuctionPrice"), AuctionModel.FormatBand(basePrice));

        // 조작 안내 — 입력 하나에 한 줄, 왼쪽 칸에 입력을 끝까지 적는다(2026-10-15 사용자 지적 — 조합키를 오른쪽 칸에 붙이면 "응축 Ctrl 잠그기"가 한 문장으로 읽혔다).
        // 길이는 다른 줄을 줄여 감당한다(빈 장비 한 줄 · 잠금은 상태 줄).
        return AddControls(content, rows =>
        {
            if (GameDataLoader.IsBox(itemId))
            {
                rows.Row(Lang.Get("UI.Inventory.LeftClick"), Lang.Get(appraiseCost > 0L ? "UI.Inventory.Appraise" : "UI.Inventory.Open"));
            }

            AddCommonControls(rows, locked);
        });
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
