using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 큐브 자동 설정의 칩 하나 — 목표 등급 · 노릴 옵션 · 조합 · 상한. 눌리면 'Clicked'만 쏜다.
//
// 이 칩은 무엇을 뜻하는지 모른다 — 글씨·고름·누를 수 있음을 'Bind'로 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ※ 고를 수 없는 칩은 'interactable'을 끈다(사용자 결정 — 전설 장비의 목표는 신화만 눌린다).
public class EnchantChipView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("칩 글씨")]
    private TMP_Text label = null!;

    [SerializeField, Tooltip("칩 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [CenterHeader("색")]
    // ⚠️ 'Image.color'가 아니라 'ColorBlock'을 칠한다 — 'Selectable'이 실행 중 'Image.color'를 덮어쓴다('EnchantCubeView'와 같다).
    [SerializeField, Tooltip("고르지 않은 칩")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    [SerializeField, Tooltip("고른 칩")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("누를 수 없는 칩")]
    private UIThemeRole lockedRole = UIThemeRole.ButtonDisabled;

    // 이 칩을 눌렀다 ('EnchantAutoPanelView'가 구독).
    public event Action<EnchantChipView>? Clicked;

    // 자기 버튼만 배선한다 — 패널이 Bind를 부르기 전에 연결돼 있어야 해서 Awake (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(label,  nameof(label));
        this.RequireRef(button, nameof(button));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 칩을 그린다 ('EnchantAutoPanelView'가 호출).
    //   text       : 글씨 (리치 텍스트 색 가능)
    //   isSelected : 고른 칩인지
    //   canPress   : 누를 수 있는지 — 아니면 'interactable'을 끄고 흐린 색
    public void Bind(string text, bool isSelected, bool canPress)
    {
        gameObject.SetActive(true);

        label.text          = text;
        button.interactable = canPress;

        ApplyColor(isSelected ? selectedRole : normalRole);
    }

    // 쓰지 않는 칩을 끈다 (칸 수보다 많은 조합 칩).
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 고름에 맞춰 색을 칠한다 (Bind에서 호출). 누를 수 없으면 'disabledColor'가 보인다.
    private void ApplyColor(UIThemeRole role)
    {
        Color      target = UIThemePalette.Of(role);
        ColorBlock colors = button.colors;

        colors.normalColor      = target;
        colors.highlightedColor = Color.Lerp(target, Color.white, 0.1f);
        colors.pressedColor     = Color.Lerp(target, Color.black, 0.2f);
        colors.selectedColor    = target;
        colors.disabledColor    = UIThemePalette.Of(lockedRole);

        button.colors = colors;
    }
}
