// 캐릭터 크기 등급(소·중·대) → 사용자에게 보일 문구. 등급은 그림 목록('VisualCatalog')의 캐릭터 그림이 갖는다.
//
// 그림 저장소가 없거나 등급이 안 정해진 캐릭터는 문구가 없다(null) — 부르는 쪽이 배지·줄을 끈다.
public static class CharacterSizeLabel
{
    // 이 캐릭터 종류(TID)의 크기 등급. 그림이 없는 캐릭터는 대체 그림의 등급이다
    public static ECharacterSize Of(int characterTid) => VisualCatalog.Current != null ? VisualCatalog.Current.GetCharacter(characterTid)?.SizeClass ?? ECharacterSize.None : ECharacterSize.None;

    // 툴팁용 — '소형' · '중형' · '대형'
    public static string? Get(ECharacterSize size)
    {
        switch (size)
        {
            case ECharacterSize.Small:  return Lang.Get("UI.Character.SizeSmall");
            case ECharacterSize.Medium: return Lang.Get("UI.Character.SizeMedium");
            case ECharacterSize.Large:  return Lang.Get("UI.Character.SizeLarge");

            default: return null;
        }
    }

    // 칸 배지용 한 글자 — '소' · '중' · '대'
    public static string? GetShort(ECharacterSize size)
    {
        switch (size)
        {
            case ECharacterSize.Small:  return Lang.Get("UI.Character.SizeSmallShort");
            case ECharacterSize.Medium: return Lang.Get("UI.Character.SizeMediumShort");
            case ECharacterSize.Large:  return Lang.Get("UI.Character.SizeLargeShort");

            default: return null;
        }
    }

    // 툴팁 한 줄 — '크기  대형'. 등급이 없으면 줄을 넣지 않는다
    public static TooltipContent AddRow(TooltipContent content, int characterTid)
    {
        string? label = Get(Of(characterTid));

        return label != null ? content.Row(Lang.Get("UI.Character.Size"), label) : content;
    }
}
