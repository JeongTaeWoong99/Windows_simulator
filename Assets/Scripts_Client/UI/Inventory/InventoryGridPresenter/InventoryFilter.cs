using GameData;

// 인벤토리 찾기 조건 — 이름 일부 · 등급 · 산업 · 묶음 (T-069). 도구 줄이 만들고 공급자가 거른다.
//
// ※ 기본값(default)이 곧 "거르지 않음"이다 — 탭 전환·창 닫기 때 이것으로 비운다.
public readonly struct InventoryFilter
{
    // 이름에 들어 있어야 할 글자. 비었으면 이름으로 거르지 않는다.
    public readonly string Text;

    // 이 등급만 남긴다. 'None'이면 전체.
    public readonly GlobalRarity Rarity;

    // 이 산업만 남긴다('IndustryType' 값). 0이면 전체 — 산업 축이 없는 탭(캐릭터)은 늘 0이다.
    public readonly byte Industry;

    // 산업이 아닌 묶음(상자 · 기타)만 남긴다. 'None'이면 전체 — 자원 탭만 쓴다. 산업과 함께 걸리지 않는다(같은 드롭다운).
    public readonly InventoryItemGroup Group;

    public InventoryFilter(string text, GlobalRarity rarity, byte industry, InventoryItemGroup group = InventoryItemGroup.None)
    {
        Text     = text.Trim();
        Rarity   = rarity;
        Industry = industry;
        Group    = group;
    }

    // 하나라도 걸려 있나. 'default'의 Text는 null이라 그것도 비었다고 본다.
    public bool IsActive => !string.IsNullOrEmpty(Text) || Rarity != GlobalRarity.None || Industry != 0 || Group != InventoryItemGroup.None;
}

// 자원 탭의 산업 아닌 묶음 — 분류 드롭다운의 산업 뒤에 붙는다 (2026-10-05).
public enum InventoryItemGroup : byte
{
    None  = 0,
    Box   = 1, // 열 수 있는 상자 ('GameDataLoader.IsBox')
    Other = 2, // 산업 자원도 상자도 아닌 것 — 인챈트 큐브 · 구슬 · 상자 전용 아이템
}
