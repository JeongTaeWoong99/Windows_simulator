using UnityEngine;

// 창 설정을 'PlayerPrefs'에 저장하고 되읽는다.
//
// ⚠️ Topmost·Scale·Anchor의 권위 소스는 실행 환경에 따라 다르다 —
// 에디터(편집·플레이)=인스펙터('setStart*'), 빌드(.exe)=저장값('WindowManager.LoadSettings',
// 'WidgetPositionLayout.LoadSavedPosition'도 같은 규칙). 그래서 이 저장값은 빌드에서만 로드에 쓰인다 —
// 에디터에서 오간 저장 쓰기는 로드가 무시한다(에디터/빌드 PlayerPrefs는 저장 위치도 다르다).
// ⚠️ TitleBar·Transparent·DynamicClickThrough는 별개 — 토글을 UI에서 걷어내 저장값을 읽지 않고
// 'setStart*'를 항상 고정한다. 저장 정책은 'Settings 규칙.md' 참조.
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

    // 위젯 칸이 캔버스(기준 해상도 1080) 좌표에서 차지하는 높이. '작업표시줄 맞춤' 배율의 분모다.
    // ⚠️ 창 크기가 아니라 씬 레이아웃에서 나오는 값이라 창 설정과 함께 저장해 둔다 —
    //   그래야 부팅 직후, 설정 패널을 한 번도 열지 않은 상태에서도 작업표시줄을 새로 재서
    //   맞춤 배율을 계산할 수 있다(모니터·DPI가 바뀌어도 따라간다).
    public const string WidgetSlotHeightKey    = Prefix + "WidgetSlotHeight";

    // 사용자가 드래그로 만든 창의 좌상단 좌표(데스크톱 절대 좌표).
    // ⚠️ 앵커(`AnchorKey`)와 배타다 — 이 키가 있으면 앵커 배치를 무시하고 이 좌표로 복원한다.
    //   위치·크기 드롭다운을 만지면 지운다('WindowManager.ClearCustomPosition').
    // ⚠️ 0도 음수도(보조 모니터) 유효한 좌표라 "값 없음"을 sentinel 로 못 만든다 → 키 존재로 판단('HasKey').
    public const string PositionXKey           = Prefix + "PosX";
    public const string PositionYKey           = Prefix + "PosY";

    // 프레임 제한 · FPS 텍스트 위치 — 창 모양이 아니라 표시 축이라 접두사를 따로 둔다('FrameRateManager').
    // ⚠️ 이 둘은 에디터에서도 저장값을 읽는다(창 설정의 "에디터=인스펙터" 규칙의 예외 — 'Settings 규칙.md' 1장).
    public const string FrameRateKey           = "Display.FrameRate";
    public const string FpsTextPositionKey     = "Display.FpsTextPosition";

    // 저장된 bool을 읽는다. 키가 없으면(첫 실행) 'fallback'을 돌려준다.
    public static bool LoadBool(string key, bool fallback)
    {
        return PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
    }

    // 저장된 int를 읽는다. 키가 없으면(첫 실행) 'fallback'을 돌려준다.
    public static int LoadInt(string key, int fallback)
    {
        return PlayerPrefs.GetInt(key, fallback);
    }

    // 저장된 float을 읽는다. 키가 없으면(첫 실행) 'fallback'을 돌려준다.
    public static float LoadFloat(string key, float fallback)
    {
        return PlayerPrefs.GetFloat(key, fallback);
    }

    // bool을 저장한다. PlayerPrefs에 bool 타입이 없어 0/1 int로 넣는다.
    public static void SaveBool(string key, bool value)
    {
        SaveInt(key, value ? 1 : 0);
    }

    // int를 저장한다. 값이 그대로면 기록하지 않는다.
    public static void SaveInt(string key, int value)
    {
        // 시작 시 불러온 값을 그대로 다시 적용하는 경로가 있어(InitializeWindow), 같은 값 쓰기를 걸러 낸다.
        if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == value)
        {
            return;
        }

        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

    // float을 저장한다. 값이 그대로면 기록하지 않는다('SaveInt'와 같은 이유).
    public static void SaveFloat(string key, float value)
    {
        if (PlayerPrefs.HasKey(key) && Mathf.Approximately(PlayerPrefs.GetFloat(key), value))
        {
            return;
        }

        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    // 그 키가 저장돼 있는지. "값이 없다"와 "0이 저장됐다"를 구분해야 할 때 쓴다(창 좌표).
    public static bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }

    // 저장을 지운다. 없는 키를 지워도 안전하다.
    public static void DeleteKey(string key)
    {
        if (!PlayerPrefs.HasKey(key))
        {
            return;
        }

        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
