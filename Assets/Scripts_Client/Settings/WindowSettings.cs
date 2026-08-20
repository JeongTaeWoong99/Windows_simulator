using UnityEngine;

/// <summary>창 설정을 'PlayerPrefs'에 저장하고 되읽는다.</summary>
/// <remarks>
/// ⚠️ Topmost·Scale·Anchor의 권위 소스는 <b>실행 환경에 따라 다르다</b> —
/// <b>에디터(편집·플레이)=인스펙터('setStart*'), 빌드(.exe)=저장값</b>('WindowManager.LoadSettings',
/// 'WidgetPositionLayout.LoadSavedPosition'도 같은 규칙). 그래서 이 저장값은 <b>빌드에서만 로드에 쓰인다</b> —
/// 에디터에서 오간 저장 쓰기는 로드가 무시한다(에디터/빌드 PlayerPrefs는 저장 위치도 다르다).
/// ⚠️ TitleBar·Transparent·DynamicClickThrough는 별개 — 토글을 UI에서 걷어내 저장값을 읽지 않고
/// 'setStart*'를 항상 고정한다. 저장 정책은 'Settings 규칙.md' 참조.
/// </remarks>
public static class WindowSettings
{
    // 키에 접두사를 붙여 다른 설정(사운드 등)이 생겨도 섞이지 않게 한다.
    private const string Prefix = "Window.";

    // ⚠️ 예약(현재 미기록) — 타이틀바·투명·동적 클릭스루는 UI 토글을 걷어낸 고정 설정이라
    //   지금은 저장·로드 어느 쪽도 쓰지 않는다. 재활성화 시 키 이름을 위해 남겨 둔다('WindowManager' 주석).
    public const string TitleBarKey            = Prefix + "TitleBar";
    public const string TransparentKey         = Prefix + "Transparent";
    public const string DynamicClickThroughKey = Prefix + "DynamicClickThrough";

    // 실제로 저장·로드에 쓰는 키 — 빌드에서 실행 간 유지된다(Topmost·Scale·Anchor).
    public const string TopmostKey             = Prefix + "Topmost";
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
