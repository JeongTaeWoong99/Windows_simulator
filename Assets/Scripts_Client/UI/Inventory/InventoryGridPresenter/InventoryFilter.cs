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

    // 산업이 아닌 분류 ID만 남긴다 — 자원: 'ResourceSlotSource.GroupOf'(큐브 · 상자 · 유물 · 소모품 · 기타) · 장비: 장비/아티팩트.
    //   null이면 전체. 산업과 함께 걸리지 않는다(같은 드롭다운). 분류 ID는 [제외] 목록과 같은 값이다(2026-11-15 — 두 목록의 기준을 맞췄다).
    public readonly string? Group;

    public InventoryFilter(string text, GlobalRarity rarity, byte industry, string? group = null)
    {
        Text     = text.Trim();
        Rarity   = rarity;
        Industry = industry;
        Group    = group;
    }

    // 하나라도 걸려 있나. 'default'의 Text는 null이라 그것도 비었다고 본다.
    public bool IsActive => !string.IsNullOrEmpty(Text) || Rarity != GlobalRarity.None || Industry != 0 || Group != null;
}

