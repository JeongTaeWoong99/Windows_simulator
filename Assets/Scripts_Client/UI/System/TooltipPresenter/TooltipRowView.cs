using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 툴팁의 한 줄 — 라벨 · 값 · 보조 값.
//
// ■ 값 칸을 둘로 나눈 이유
// 한 칸에 "19.51% · 2,025 골드"를 붙여 쓰면 칸을 넘겨 잘린다(2026-09-24 실측).
// 칸마다 고정폭을 두면 잘리지 않고, **줄마다 세로 열이 맞는다** — 숫자 길이가 제각각(0.1% / 64.29%)이다.
// 보조 값이 빈 줄은 그 칸을 꺼서 값 칸이 오른쪽 끝에 붙게 한다.
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

    private void Awake()
    {
        this.RequireRef(labelText,       nameof(labelText));
        this.RequireRef(valueText,       nameof(valueText));
        this.RequireRef(subValueText,    nameof(subValueText));
        this.RequireRef(backgroundImage, nameof(backgroundImage));

        _defaultColor = backgroundImage.color;
    }

    // 줄을 채운다 ('TooltipPresenter.Render'에서 호출).
    public void Bind(TooltipContent.Line line)
    {
        labelText.text    = line.Label;
        valueText.text    = line.Value;
        subValueText.text = line.SubValue;

        subValueText.gameObject.SetActive(line.SubValue.Length > 0);
        backgroundImage.color = line.Background ?? _defaultColor;
    }
}
