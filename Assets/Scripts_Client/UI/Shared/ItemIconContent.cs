using System.Collections.Generic;
using GameData;

// 아이콘 칸 하나에 넘기는 완성값 — 등급 바탕 · 글자 · 모서리 수량 · 능력치 칸 ('ItemIconView'가 그린다).
//
// ■ 왜 글자인가
// 아이콘 에셋이 아직 없다(🎨). 그동안은 **이름의 첫 글자**를 등급 바탕 위에 크게 적는다 —
// 줄마다 같은 회색 네모가 서 있으면 목록이 한 덩어리로 보이고, 글자만 있어도 눈이 줄을 가른다.
// 아이콘이 들어오면 'IconKey'로 스프라이트를 찾고 글자는 숨긴다(칸 쪽 한 곳만 바뀐다).
//
// ■ 팩토리는 여기, 그리기는 칸에
// 자원·장비·캐릭터·골드가 이름·등급을 다른 테이블에서 읽는다. 부르는 화면마다 그 조회를 반복하면
// 경매장과 우편이 같은 장비를 다르게 그린다 — 'SlotData'와 같은 이유로 완성값을 넘긴다.
public readonly struct ItemIconContent
{
    // 칸 바탕의 등급. 'None'이면 등급을 모르는 회색이다.
    public readonly GlobalRarity Rarity;

    // 아이콘 자리에 적을 글자 — 이름의 첫 글자.
    public readonly string Glyph;

    // 오른쪽 아래 수량 — 'x12'. 비어 있으면 숨긴다(장비·캐릭터는 한 개체라 적지 않는다).
    public readonly string Count;

    // 능력치 칸의 등급 — 길이가 칸 수, 'None'은 빈 칸. null이면 줄을 끈다(장비만 값이 있다).
    // ⚠️ 받은 목록을 그대로 쥔다 — 부르는 쪽이 버퍼를 재사용하면 'Bind' 전에 바꾸지 않는다.
    public readonly IReadOnlyList<GlobalRarity>? Sockets;

    public ItemIconContent(GlobalRarity rarity, string glyph, string count, IReadOnlyList<GlobalRarity>? sockets)
    {
        Rarity  = rarity;
        Glyph   = glyph;
        Count   = count;
        Sockets = sockets;
    }

    // 자원 — 수량이 1보다 크면 모서리에 적는다.
    public static ItemIconContent ForItem(int itemTid, long count)
        => new ItemIconContent(
            GameDataLoader.GetItemRarity(itemTid),
            GlyphOf(GameDataLoader.GetItemName(itemTid)),
            count > 1L ? $"x{count:N0}" : "",
            null);

    // 장비 — 능력치 칸까지 그린다. 옵션을 모르면(null) 칸 수만큼 빈 칸이다.
    public static ItemIconContent ForEquip(int equipTid, IReadOnlyList<int>? optionTids)
    {
        var options = new List<EnchantOptionTableRow?>();
        var grades  = new List<GlobalRarity>();

        EquipLabel.ReadStatOptions(equipTid, optionTids, options);
        EquipLabel.ToSocketGrades(options, grades);

        return new ItemIconContent(
            GameDataLoader.GetEquipRarity(equipTid),
            GlyphOf(GameDataLoader.GetEquipName(equipTid)),
            "",
            grades);
    }

    // 캐릭터 — 같은 종류가 여러 명이면 모서리에 명 수를 적는다(우편 첨부).
    public static ItemIconContent ForCharacter(int characterTid, int count = 1)
        => new ItemIconContent(
            GameDataLoader.GetCharacterRarity(characterTid),
            GlyphOf(GameDataLoader.GetCharacterName(characterTid)),
            count > 1 ? $"x{count}" : "",
            null);

    // 골드 — 등급이 없다. 글자는 'G'.
    public static ItemIconContent ForGold()
        => new ItemIconContent(GlobalRarity.None, "G", "", null);

    // 이름의 첫 글자. 모르는 이름('?#12')이나 빈 이름은 '?'.
    private static string GlyphOf(string name)
        => string.IsNullOrEmpty(name) || name[0] == '?' ? "?" : name.Substring(0, 1);
}
