using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 툴팁의 한 줄 — 라벨 · 값 · 보조 값.
//
// ■ 값 칸을 둘로 나눈 이유
// 한 칸에 "19.51% · 2,025 골드"를 붙여 쓰면 칸을 넘겨 잘린다(2026-09-24 실측).
// 칸을 나누면 **줄마다 세로 열이 맞는다** — 숫자 길이가 제각각(0.1% / 64.29%)이다.
// 보조 값이 빈 줄은 그 칸을 꺼서 값 칸이 오른쪽 끝에 붙게 한다.
//
// ■ 열 폭은 툴팁마다 잰다 (2026-09-26 · T-050)
// 한때 값 80px · 보조 값 130px 고정이었는데, '슬롯 1 · 낚시 Lv.1' 같은 긴 값이 '…'로 잘렸다.
// 이제 'TooltipPresenter'가 이 툴팁의 줄 전부에서 **열마다 가장 긴 글자 폭**을 재어('MeasureColumns')
// 모든 줄에 같은 폭을 준다('SetColumnWidths') — 잘리지 않으면서 열도 맞는다. 패널은 그만큼 넓어진다.
//
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
public class TooltipRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("라벨 (예: 기준 주기 · 심해 새우)")]
    private TMP_Text labelText = null!;

    [SerializeField, Tooltip("값 (예: 3초 · 19.51%)")]
    private TMP_Text valueText = null!;

    [SerializeField, Tooltip("보조 값 (예: 2,025 골드). 비면 칸째 꺼진다")]
    private TMP_Text subValueText = null!;

    [SerializeField, Tooltip("줄 바탕 — 등급색 등을 칠한다")]
    private Image backgroundImage = null!;

    // 프리팹에 찍힌 기본 바탕색. 풀에서 재사용될 때 이전 줄의 색이 남지 않게 여기로 되돌린다.
    private Color _defaultColor;

    // 값 · 보조 값 칸의 폭을 정하는 레이아웃 요소 — 줄의 HorizontalLayoutGroup이 이 값으로 칸을 나눈다.
    private LayoutElement _valueLayout    = null!;
    private LayoutElement _subValueLayout = null!;

    private bool _isReady; // 참조 확보 완료 여부

    private void Awake()
    {
        EnsureInitialized();
    }

    // 참조를 확보한다 (Awake · 공개 메서드 첫머리에서 호출).
    //
    // ⚠️ 'Awake'만 믿지 않는다 — **꺼진 부모 아래에 만든 줄은 부모가 켜질 때까지 'Awake'가 미뤄진다.**
    //   그 사이 'Bind'·'SetColumnWidths'가 불려 NRE가 났다(2026-09-26 — 간단형 툴팁이 줄 영역을 끈 직후).
    //   부르는 쪽 순서에 기대지 않고 여기서 스스로 보장한다. 두 번 불려도 한 번만 돈다.
    private void EnsureInitialized()
    {
        if (_isReady)
        {
            return;
        }

        this.RequireRef(labelText,       nameof(labelText));
        this.RequireRef(valueText,       nameof(valueText));
        this.RequireRef(subValueText,    nameof(subValueText));
        this.RequireRef(backgroundImage, nameof(backgroundImage));

        _defaultColor   = backgroundImage.color;
        _valueLayout    = RequireLayout(valueText);
        _subValueLayout = RequireLayout(subValueText);

        // ★ 끝까지 왔을 때만 세운다 — 위에서 예외가 나면 다음 호출이 다시 시도하고 같은 원인을 드러낸다.
        _isReady = true;
    }

    // 이 줄의 값 · 보조 값이 잘리지 않을 폭 ('TooltipPresenter.Render'가 열 폭을 정할 때 호출).
    // 꺼진 보조 값 칸은 0이다 — 그 줄은 보조 값 열을 쓰지 않는다.
    public (float Value, float SubValue) MeasureColumns()
    {
        EnsureInitialized();

        float value = MeasureText(valueText);
        float sub   = subValueText.gameObject.activeSelf ? MeasureText(subValueText) : 0f;

        return (value, sub);
    }

    // 값 · 보조 값 칸 폭을 정한다 — 한 툴팁의 모든 줄에 같은 값을 준다 ('TooltipPresenter.Render'에서 호출).
    public void SetColumnWidths(float value, float subValue)
    {
        EnsureInitialized();

        _valueLayout.minWidth          = value;
        _valueLayout.preferredWidth    = value;
        _subValueLayout.minWidth       = subValue;
        _subValueLayout.preferredWidth = subValue;
    }

    // 글자가 한 줄에 다 들어갈 폭. 빈 문자열이면 0 (MeasureColumns에서 호출).
    // ※ 소수점 폭에서 반올림 오차로 '…'가 붙지 않게 올림한다.
    private static float MeasureText(TMP_Text text)
        => text.text.Length == 0 ? 0f : Mathf.Ceil(text.GetPreferredValues(text.text).x);

    // 칸의 LayoutElement를 얻는다. 없으면 여기서 멈춘다 — 폭을 못 정하면 조용히 잘린다 (Awake에서 호출).
    private LayoutElement RequireLayout(TMP_Text text)
    {
        var layout = text.GetComponent<LayoutElement>();

        this.RequireRef(layout, $"{text.name}의 LayoutElement");

        return layout;
    }

    // 줄을 채운다 ('TooltipPresenter.Render'에서 호출).
    public void Bind(TooltipContent.Line line)
    {
        EnsureInitialized();

        labelText.text    = line.Label;
        valueText.text    = line.Value;
        subValueText.text = line.SubValue;

        subValueText.gameObject.SetActive(line.SubValue.Length > 0);
        backgroundImage.color = line.Background ?? _defaultColor;
    }
}
