using GameData;

// 창고 칸 하나에 그릴 완성된 표시값. 공급자('StorageSlotSource')가 만들어 격자에 넘긴다.
//
// ■ 왜 완성값을 넘기는가
// 칸('InventorySlotView')이 "지금 자원 탭인가 캐릭터 탭인가"를 알아보고 분기하면,
// 탭이 늘어날 때마다 칸이 두꺼워진다. 무엇을 그릴지는 공급자가 정하고 칸은 받아 그리기만 한다
// ('Storage 규칙.md'의 "칸이 화면을 알아보고 분기하지 않는다").
public readonly struct StorageSlotData
{
    // 이 칸이 무엇인가 — 자원은 'ItemId', 캐릭터는 개체 번호.
    // 칸을 눌러 상세를 띄우거나 판매·배치로 이어질 때 필요한 값이라 표시용 문구와 함께 들고 다닌다.
    public readonly long Key;

    // 칸 이름 줄.
    public readonly string Name;

    // 이름 아래 보조 문구 한 줄 — 자원은 수량, 캐릭터는 배치 상태.
    // 칸 위젯 이름이 'Sub Text'인 것도 이 자리를 가리키기 위해서다.
    public readonly string Sub;

    // 등급 배경색의 근거. 등급이 없는 종류(캐릭터)는 'GlobalRarity.None'을 넘긴다
    // — 'RarityPalette.Get'이 'Unknown' 색으로 떨어뜨린다.
    public readonly GlobalRarity Rarity;

    public StorageSlotData(long key, string name, string sub, GlobalRarity rarity)
    {
        Key    = key;
        Name   = name;
        Sub    = sub;
        Rarity = rarity;
    }
}
