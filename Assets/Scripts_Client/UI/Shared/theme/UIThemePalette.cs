using System;
using UnityEngine;

// 역할('UIThemeRole') → 색 표. 아트가 들어오기 전까지 화면 톤을 한 곳에서 맞추는 임시 팔레트다.
// 톤은 에셋 값이 정한다 — 지금은 양피지 시험(양피지 창·버튼 + 진한 글씨 · 어두운 칸 + 밝은 글씨 · 진홍 제목 띠). 'Art 규칙.md' UI 절.
// 글씨 역할은 바탕 밝기로 고른다 — 밝은 바탕 위는 TextDark·HighlightDark, 어두운 바탕 위는 TextMain·Highlight.
//
// 에셋은 'Resources/UIThemePalette' 하나다. 값을 고치면 열린 씬의 'UIThemeColor'가 바로 따라온다
// (에디터 쪽 'UIThemeBaker'). 프리팹은 메뉴 'Window/DesktopWindowControl/UI 테마/적용'으로 굽는다.
[CreateAssetMenu(menuName = "DesktopWindowControl/UI Theme Palette", fileName = "UIThemePalette")]
public class UIThemePalette : ScriptableObject
{
    // 'Resources.Load' 경로
    public const string ResourcePath = "UIThemePalette";

    [CenterHeader("바탕")]
    [SerializeField] private Color panelBg  = new Color32(0x14, 0x20, 0x2E, 0xEB);
    [SerializeField] private Color panelSub = new Color32(0x1F, 0x30, 0x44, 0xFF);
    [SerializeField] private Color slot     = new Color32(0x26, 0x39, 0x4F, 0xFF);
    [SerializeField] private Color border   = new Color32(0x3E, 0x5A, 0x78, 0xFF);
    [SerializeField] private Color header   = new Color32(0x8B, 0x1B, 0x19, 0xFF);

    [CenterHeader("버튼")]
    [SerializeField] private Color button         = new Color32(0x2B, 0x6F, 0x9E, 0xFF);
    [SerializeField] private Color buttonSelected = new Color32(0xB8, 0x74, 0x1A, 0xFF);
    [SerializeField] private Color buttonPrimary  = new Color32(0xC9, 0x83, 0x14, 0xFF);
    [SerializeField] private Color buttonDisabled = new Color32(0x3A, 0x46, 0x54, 0xFF);

    [CenterHeader("강조")]
    [SerializeField] private Color accent    = new Color32(0x4F, 0xC8, 0xF0, 0xFF);
    [SerializeField] private Color highlight = new Color32(0xFF, 0xD4, 0x47, 0xFF);
    [SerializeField] private Color highlightDark = new Color32(0x8A, 0x4A, 0x00, 0xFF);

    [CenterHeader("글씨")]
    [SerializeField] private Color textMain     = new Color32(0xF2, 0xF6, 0xFA, 0xFF);
    [SerializeField] private Color textSub      = new Color32(0x9F, 0xB3, 0xC8, 0xFF);
    [SerializeField] private Color textDisabled = new Color32(0x5E, 0x6F, 0x80, 0xFF);
    [SerializeField] private Color textDark     = new Color32(0x1A, 0x23, 0x30, 0xFF);

    [CenterHeader("의미색")]
    [SerializeField] private Color positive = new Color32(0x6D, 0xDB, 0x7A, 0xFF);
    [SerializeField] private Color negative = new Color32(0xFF, 0x6B, 0x5E, 0xFF);
    [SerializeField] private Color overlay  = new Color32(0x00, 0x00, 0x00, 0xFF);

#if UNITY_EDITOR
    // 인스펙터에서 값이 바뀌었다 — 에디터 쪽 'UIThemeBaker'가 열린 씬에 다시 굽는다.
    public static event Action<UIThemePalette>? Changed;
#endif

    private static UIThemePalette? _current;

    // 지금 쓰는 팔레트. 에셋이 없으면 기본값 인스턴스로 버틴다(색이 틀릴 뿐 화면은 선다).
    public static UIThemePalette Current
    {
        get
        {
            if (_current == null)
            {
                _current = Resources.Load<UIThemePalette>(ResourcePath);
            }

            if (_current == null)
            {
                _current = CreateInstance<UIThemePalette>();
            }

            return _current;
        }
    }

    // 역할의 색 (Presenter가 상태 색을 칠할 때 호출)
    public static Color Of(UIThemeRole role) => Current.Get(role);

    // 그 글씨의 바탕에 맞춘 역할 색 — 글씨 태그가 TextDark(밝은 바탕 위)면 글씨·금 역할을 밝은 바탕용으로 바꾼다 (양피지 · 2026-11-14).
    //   Presenter가 상태로 글씨색을 칠할 때('warnText.color = …') 바탕을 몰라도 묻히지 않게 한다.
    public static Color OfFor(Component text, UIThemeRole role)
    {
        bool onLight = text.TryGetComponent(out UIThemeColor tag) && tag.Role == UIThemeRole.TextDark;

        if (!onLight)
        {
            return Of(role);
        }

        Color dark = Of(UIThemeRole.TextDark);

        return role switch
        {
            UIThemeRole.TextMain     => dark,
            UIThemeRole.TextSub      => new Color(dark.r, dark.g, dark.b, 0.72f),
            UIThemeRole.TextDisabled => new Color(dark.r, dark.g, dark.b, 0.47f),
            UIThemeRole.Highlight    => Of(UIThemeRole.HighlightDark),
            _                        => Of(role),
        };
    }

    public Color Get(UIThemeRole role) => role switch
    {
        UIThemeRole.PanelBg        => panelBg,
        UIThemeRole.PanelSub       => panelSub,
        UIThemeRole.Slot           => slot,
        UIThemeRole.Border         => border,
        UIThemeRole.Header         => header,
        UIThemeRole.Button         => button,
        UIThemeRole.ButtonSelected => buttonSelected,
        UIThemeRole.ButtonPrimary  => buttonPrimary,
        UIThemeRole.ButtonDisabled => buttonDisabled,
        UIThemeRole.Accent         => accent,
        UIThemeRole.Highlight      => highlight,
        UIThemeRole.HighlightDark  => highlightDark,
        UIThemeRole.TextMain       => textMain,
        UIThemeRole.TextSub        => textSub,
        UIThemeRole.TextDisabled   => textDisabled,
        UIThemeRole.TextDark       => textDark,
        UIThemeRole.Positive       => positive,
        UIThemeRole.Negative       => negative,
        UIThemeRole.Overlay        => overlay,
        _                          => Color.magenta, // 표에 없는 역할 — 눈에 띄게
    };

#if UNITY_EDITOR
    // (Unity 메시지)
    private void OnValidate() => Changed?.Invoke(this);
#endif
}
