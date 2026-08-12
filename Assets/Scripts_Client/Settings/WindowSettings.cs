using UnityEngine;

/// <summary>창 설정을 'PlayerPrefs'에 저장하고 되읽는다.</summary>
/// <remarks>
/// ⚠️ 'WindowManager'의 'setStart*' 필드는 "저장값이 없을 때 쓰는 공장 초기값"이다 —
/// 한 번 조작하면 저장값이 이긴다. 저장 정책과 그 이유는 'Settings 규칙.md' 참조.
/// </remarks>
public static class WindowSettings
{
    // 키에 접두사를 붙여 다른 설정(사운드 등)이 생겨도 섞이지 않게 한다.
    private const string Prefix = "Window.";

    public const string TitleBarKey            = Prefix + "TitleBar";
    public const string TransparentKey         = Prefix + "Transparent";
    public const string TopmostKey             = Prefix + "Topmost";
    public const string DynamicClickThroughKey = Prefix + "DynamicClickThrough";
    public const string ScaleKey               = Prefix + "Scale";
    public const string AnchorKey              = Prefix + "Anchor";

    // 위젯이 창 안 어느 칸(6칸)에 놓이는가. 창을 데스크톱 어디에 두는가(AnchorKey, 9분할)와는 다른 축이다.
    public const string WidgetPositionKey      = "Widget.Position";

    /// <summary>저장된 bool을 읽는다. 키가 없으면(첫 실행) 'fallback'을 돌려준다.</summary>
    public static bool LoadBool(string key, bool fallback)
    {
        return PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
    }

    /// <summary>저장된 int를 읽는다. 키가 없으면(첫 실행) 'fallback'을 돌려준다.</summary>
    public static int LoadInt(string key, int fallback)
    {
        return PlayerPrefs.GetInt(key, fallback);
    }

    /// <summary>bool을 저장한다. PlayerPrefs에 bool 타입이 없어 0/1 int로 넣는다.</summary>
    public static void SaveBool(string key, bool value)
    {
        SaveInt(key, value ? 1 : 0);
    }

    /// <summary>int를 저장한다. 값이 그대로면 기록하지 않는다.</summary>
    public static void SaveInt(string key, int value)
    {
        // 시작 시 불러온 값을 그대로 다시 적용하는 경로가 있어(InitializeWindow), 같은 값 쓰기를 걸러 낸다.
        if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == value)
            return;

        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
}
