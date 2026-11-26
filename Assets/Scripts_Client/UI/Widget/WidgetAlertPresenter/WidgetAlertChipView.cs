using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 위젯 알림 줄의 띠 하나 — 왼쪽 색 막대 + 한 줄 글씨. 누르면 'Clicked'.
// 색이 종류를 말한다: 경고(빨강 · 놔두면 손해) · 진행(노랑) · 결과(초록). 글씨는 부르는 쪽이 채운다.
public class WidgetAlertChipView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("띠 전체 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [SerializeField, Tooltip("왼쪽 색 막대 — 종류 색")]
    private Image bar = null!;

    [SerializeField, Tooltip("알림 한 줄")]
    private TMP_Text messageText = null!;

    // 띠를 눌렀다 ('WidgetAlertPresenter'가 구독)
    public event Action? Clicked;

    // 참조 확인 → 배선 (Unity 메시지). 견본을 복제해 쓰므로 Awake에서 건다 — 복제 직후 'Bind'가 먼저 온다.
    private void Awake()
    {
        this.RequireRef(button,      nameof(button));
        this.RequireRef(bar,         nameof(bar));
        this.RequireRef(messageText, nameof(messageText));

        button.onClick.AddListener(() => Clicked?.Invoke());
    }

    // 글씨와 종류 색을 쓴다 (WidgetAlertPresenter.Redraw)
    public void Bind(string message, UIThemeRole role)
    {
        messageText.text = message;
        bar.color        = UIThemePalette.Of(role);
    }
}
