using GameData;

// 캐릭터 종족('CharacterRace') → 사용자에게 보일 문구. 종족은 표시 전용이고 판정에 쓰지 않는다(캐릭터 기획 4장).
//
// ■ 왜 따로 두는가
// 인벤토리 칸 · 툴팁 · 작업슬롯 · 경매 · 우편 · 응축 창이 같은 이름을 쓴다. 'RarityLabel'과 같은 부류다.
//
// 'None'이나 모르는 값은 문구가 없다(null) — 부르는 쪽이 배지·줄을 끈다('CharacterSizeLabel'과 같다).
public static class RaceLabel
{
    // 이 캐릭터 종류(TID)의 종족 이름 — '인간'. 없으면 null
    public static string? Of(int characterTid) => Get(GameDataLoader.GetCharacterRace(characterTid));

    // 칸 배지용 한 글자 — '인'. 없으면 null
    public static string? ShortOf(int characterTid) => GetShort(GameDataLoader.GetCharacterRace(characterTid));

    public static string? Get(CharacterRace race)
    {
        switch (race)
        {
            case CharacterRace.Human:   return Lang.Get("UI.Character.RaceHuman");
            case CharacterRace.Monster: return Lang.Get("UI.Character.RaceMonster");
            case CharacterRace.Undead:  return Lang.Get("UI.Character.RaceUndead");
            case CharacterRace.Golem:   return Lang.Get("UI.Character.RaceGolem");

            default: return null;
        }
    }

    public static string? GetShort(CharacterRace race)
    {
        switch (race)
        {
            case CharacterRace.Human:   return Lang.Get("UI.Character.RaceHumanShort");
            case CharacterRace.Monster: return Lang.Get("UI.Character.RaceMonsterShort");
            case CharacterRace.Undead:  return Lang.Get("UI.Character.RaceUndeadShort");
            case CharacterRace.Golem:   return Lang.Get("UI.Character.RaceGolemShort");

            default: return null;
        }
    }

    // 툴팁 한 줄 — '종족  인간'. 종족이 없으면 줄을 넣지 않는다
    public static TooltipContent AddRow(TooltipContent content, int characterTid)
    {
        string? label = Of(characterTid);

        return label != null ? content.Row(Lang.Get("UI.Character.Race"), label) : content;
    }

    // 한 줄 문구 뒤에 ' · 인간'을 붙인다. 종족이 없으면 그대로 돌려준다
    public static string Append(string text, int characterTid)
    {
        string? label = Of(characterTid);

        return label != null ? $"{text}{UIRichText.Dot}{label}" : text;
    }
}
