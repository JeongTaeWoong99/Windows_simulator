using System.Collections.Generic;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시세 탭의 "지금 단가별 판매 수량" — 싼 것부터 10줄. 줄마다 단가 · 막대 · 수량.
// 'AuctionPricePresenter'가 가격대를 'Bind'로 넘긴다(종속 View). 줄은 씬에 10개 만들어 두고 켜고 끈다.
// 막대는 오른쪽 앵커('anchorMax.x')로 길이를 정한다 — 가장 많은 줄이 끝까지다.
// ※ 'Filled' 이미지를 쓰지 않는다 — 둥근 9-slice 스프라이트를 채우기로 자르면 끝이 뾰족하게 일그러진다.
public class AuctionPriceLevelsView : MonoBehaviour
{
    [CenterHeader("줄")]
    [SerializeField, NonReorderable, Tooltip("줄 루트 — 위(싼 것)부터")]
    private GameObject[] rows = new GameObject[0];

    [SerializeField, NonReorderable, Tooltip("단가 글자 — 줄 순서")]
    private TMP_Text[] priceTexts = new TMP_Text[0];

    [SerializeField, NonReorderable, Tooltip("막대 (부모 폭을 꽉 채운 Image) — 줄 순서. 길이는 코드가 anchorMax.x로 정한다")]
    private Image[] bars = new Image[0];

    [SerializeField, NonReorderable, Tooltip("수량 글자 — 줄 순서")]
    private TMP_Text[] countTexts = new TMP_Text[0];

    [SerializeField, Tooltip("판매 중인 매물이 없을 때만 보인다")]
    private GameObject emptyText = null!;

    // 아주 적은 줄도 막대가 보이게 하는 최소 길이 — 목업의 4%.
    private const float MinFill = 0.04f;

    // 필수 참조 검증 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(emptyText, nameof(emptyText));
    }

    // 가격대로 다시 그린다 — 'levels'는 싼 순.
    public void Bind(IReadOnlyList<MarketPriceLevelInfo> levels)
    {
        long most = 1L;

        foreach (MarketPriceLevelInfo level in levels)
        {
            most = System.Math.Max(most, level.Count);
        }

        for (int i = 0; i < rows.Length; i++)
        {
            bool on = i < levels.Count;

            rows[i].SetActive(on);

            if (!on)
            {
                continue;
            }

            priceTexts[i].text = UIRichText.Gold(levels[i].UnitPrice);
            countTexts[i].text = $"{levels[i].Count:N0}개";
            RectTransform bar = bars[i].rectTransform;
            bar.anchorMax = new Vector2(Mathf.Max(MinFill, levels[i].Count / (float)most), bar.anchorMax.y);
        }

        emptyText.SetActive(levels.Count == 0);
    }
}
