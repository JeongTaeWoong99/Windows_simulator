using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 산업 레벨 정보의 '나오는 자원' 한 줄 — 이름 · 확률 · 판매가.
//
// ■ 효율 계산 줄과 나눈 이유는 **값이 둘이기 때문**이다
// 'EfficiencyRowView'는 값 칸이 하나라, 확률과 판매가를 한 칸에 붙여 쓰면
// **칸을 넘겨 잘린다**(2026-09-24 실측 — "19.51% · 2,025 골드"가 120px 칸에서 잘렸다).
// 칸을 나눠 각각 고정폭으로 두면 잘리지 않고, 덤으로 **줄마다 세로 열이 맞는다** —
// 숫자 길이가 제각각(0.1% / 64.29%)이라 한 칸에 두면 소수점이 어긋난다.
//
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
public class IndustryDropRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("자원 이름 (예: 심해 새우)")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("나올 확률 (예: 19.51%)")]
    private TMP_Text rateText = null!;

    [SerializeField, Tooltip("판매가 (예: 2,025 골드)")]
    private TMP_Text priceText = null!;

    [SerializeField, Tooltip("줄 바탕 — 등급 색을 칠한다. 창고 칸과 같은 표('RarityPalette')다")]
    private Image backgroundImage = null!;

    // 씬에 찍힌 기본 바탕색. 'Clear'가 여기로 되돌린다 — 풀에서 재사용될 때 이전 등급 색이 남지 않게.
    private Color _defaultColor;

    private void Awake()
    {
        this.RequireRef(nameText,        nameof(nameText));
        this.RequireRef(rateText,        nameof(rateText));
        this.RequireRef(priceText,       nameof(priceText));
        this.RequireRef(backgroundImage, nameof(backgroundImage));

        _defaultColor = backgroundImage.color;
    }

    // 줄을 채운다 ('WorkStationSelectPresenter.RefreshIndustryLevelInfo'에서 호출).
    //   name  : 자원 이름
    //   rate  : 나올 확률 문구 — 비율은 화면이 'Weight' 합으로 만든다
    //   price : 판매가 문구 — 'ItemTable.BasePrice'
    public void Bind(string name, string rate, string price)
    {
        nameText.text  = name;
        rateText.text  = rate;
        priceText.text = price;
    }

    // 줄 바탕을 등급 색으로 칠한다.
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // 바탕색도 씬 기본값으로 되돌린다 — 안 되돌리면 다음 자원이 이전 등급 색을 쓴다.
    public void Clear()
    {
        Bind("", "", "");
        backgroundImage.color = _defaultColor;
    }
}
