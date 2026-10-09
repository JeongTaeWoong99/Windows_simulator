using UnityEditor;

// 주기 수신 로그('ClientLogger.ShowPeriodic')를 켜고 끄는 에디터 설정. 버튼은 치트 창 도구 줄에 있다.
//
// 런타임 플래그는 정적 필드라 도메인 리로드(플레이 진입·컴파일)마다 꺼진 값으로 돌아간다.
// 그래서 값은 'EditorPrefs'에 두고, 로드될 때마다 런타임 쪽에 다시 밀어 넣는다.
// ※ 빌드에는 이 파일이 없다 — 빌드는 늘 꺼진 채로 돈다.
[InitializeOnLoad]
internal static class PeriodicLogSettings
{
    private static readonly string EnabledKey = ProjectPreferences.PrefsPrefix + "Log.Periodic";

    // 도메인 리로드 직후 저장된 값을 런타임에 건다 (InitializeOnLoad)
    static PeriodicLogSettings()
    {
        ClientLogger.ShowPeriodic = Enabled;
    }

    // 주기 수신 로그를 찍을지. 기본은 꺼짐.
    public static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledKey, false);
        set
        {
            EditorPrefs.SetBool(EnabledKey, value);
            ClientLogger.ShowPeriodic = value;
        }
    }
}
