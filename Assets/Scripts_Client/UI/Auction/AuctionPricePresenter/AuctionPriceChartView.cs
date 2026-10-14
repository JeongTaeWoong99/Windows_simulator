using System;
using System.Collections.Generic;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// 시세 탭의 14일 그래프 — 날마다 막대(바닥 ~ 그날 평균 단가) · 막대 꼭대기를 잇는 선 · 아래 날짜, 왼쪽 가격 눈금 4줄.
// 'AuctionPricePresenter'가 일별 시세를 'Bind'로 넘긴다(종속 View). 칸은 씬에 14개 만들어 두고 앵커로 자리만 옮긴다.
//
// ■ 14일을 14칸으로 고르게 나누고 칸 가운데에 막대·선 점·날짜를 둔다 — 날짜는 매일 적는다(서버가 하루 단위로 준다).
// ■ 막대는 0부터 꽉 채운다 (2026-10-14 사용자 요청) — 예전엔 띠(최저~최고)였는데, 거래가 한 건인 날은 최저=최고라 점으로 보였다.
// ■ 체결이 없는 날은 막대를 숨기고 선은 건너뛴다 — 0을 그리면 시세가 폭락한 것처럼 보인다.
// ■ 세로 범위는 0 ~ (보이는 날 평균의 최고 × 1.1)을 눈금이 딱 떨어지게 올린 값 — 막대는 바닥이 0이어야 높이를 견줄 수 있다.
//   눈금 간격은 1 · 2 · 2.5 · 3 · 4 · 5 · 6 · 8 × 10ⁿ 중 하나다(0 · 5 · 10 · 15). 그냥 나누면 0 · 4 · 9 · 14처럼 읽기 어렵다.
// ■ 선은 막대 위에 그린다 — 씬에서 'Avg Line'이 막대들보다 뒤(아래쪽 형제)에 있어야 한다.
public class AuctionPriceChartView : MonoBehaviour
{
    [CenterHeader("그리는 칸")]
    [SerializeField, NonReorderable, FormerlySerializedAs("bands"), Tooltip("날마다의 막대 — 오래된 날부터. 부모는 그래프 영역")]
    private RectTransform[] bars = new RectTransform[0];

    [SerializeField, NonReorderable, Tooltip("날짜 글자 — 오래된 날부터. 칸 가운데에 코드가 놓는다")]
    private TMP_Text[] dateTexts = new TMP_Text[0];

    [SerializeField, NonReorderable, Tooltip("가격 눈금 — 아래부터 위로")]
    private TMP_Text[] axisTexts = new TMP_Text[0];

    [SerializeField, Tooltip("막대 꼭대기를 잇는 선")]
    private AuctionPriceLineGraphic line = null!;

    [SerializeField, Tooltip("체결이 하루도 없을 때만 보인다")]
    private GameObject emptyText = null!;

    // 막대 폭 — 칸 폭에 대한 비율.
    private const float BarWidthRatio = 0.6f;

    // 세로 범위 위 여유 — 최고 막대가 천장에 닿지 않게.
    private const double TopMargin = 1.1d;

    private readonly List<Vector2> _linePoints = new List<Vector2>();

    // 필수 참조 검증 — Presenter가 Bind를 부르기 전에 끝나 있어야 한다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(line,      nameof(line));
        this.RequireRef(emptyText, nameof(emptyText));
    }

    // 일별 시세로 다시 그린다. 'days'는 오래된 날 → 오늘(14칸).
    public void Bind(IReadOnlyList<MarketDailyPriceInfo> days)
    {
        long max = 0L;

        foreach (MarketDailyPriceInfo day in days)
        {
            if (day.Count > 0L)
            {
                max = Math.Max(max, day.AvgUnitPrice);
            }
        }

        bool hasAny = max > 0L;

        emptyText.SetActive(!hasAny);
        _linePoints.Clear();

        double high = hasAny ? NiceTop(max * TopMargin, Math.Max(axisTexts.Length - 1, 1)) : 1d;

        int   slots    = Math.Max(bars.Length, 1);
        float slotSpan = 1f / slots;

        for (int i = 0; i < bars.Length; i++)
        {
            MarketDailyPriceInfo? day = i < days.Count ? days[i] : null;
            bool                  on  = day != null && day.Count > 0L;
            float                 x   = (i + 0.5f) * slotSpan; // 칸 가운데

            if (i < dateTexts.Length)
            {
                PlaceDate(dateTexts[i], x);
                dateTexts[i].text = day != null ? FormatDate(day.DayStartUnixMs) : "";
            }

            bars[i].gameObject.SetActive(on);

            if (!on)
            {
                continue;
            }

            float top = (float)(day!.AvgUnitPrice / high);

            PlaceBar(bars[i], x, slotSpan * BarWidthRatio, top);
            _linePoints.Add(new Vector2(x, top));
        }

        line.SetPoints(_linePoints);

        for (int i = 0; i < axisTexts.Length; i++)
        {
            double value = high * i / Math.Max(axisTexts.Length - 1, 1);

            axisTexts[i].text = hasAny ? $"{(long)value:N0}" : "";
        }
    }

    // 눈금 간격 후보 — 10ⁿ에 곱한다.
    private static readonly double[] NiceSteps = { 1d, 2d, 2.5d, 3d, 4d, 5d, 6d, 8d, 10d };

    // 'value'를 덮는 가장 작은 '간격 × steps' — 간격은 'NiceSteps' × 10ⁿ. 최소 간격은 1(골드는 정수).
    private static double NiceTop(double value, int steps)
    {
        double raw  = Math.Max(value / steps, 1d);
        double unit = Math.Pow(10d, Math.Floor(Math.Log10(raw)));

        foreach (double nice in NiceSteps)
        {
            if (raw <= unit * nice)
            {
                return unit * nice * steps;
            }
        }

        return unit * 10d * steps;
    }

    // 막대를 칸 가운데 'x'에 바닥부터 'top'까지 놓는다 — 폭·높이 모두 앵커로(비율). 그래프 크기가 바뀌어도 따라간다.
    private static void PlaceBar(RectTransform bar, float x, float width, float top)
    {
        bar.anchorMin        = new Vector2(x - width * 0.5f, 0f);
        bar.anchorMax        = new Vector2(x + width * 0.5f, top);
        bar.sizeDelta        = Vector2.zero;
        bar.anchoredPosition = Vector2.zero;
    }

    // 날짜 글자를 칸 가운데 'x'에 놓는다 — 높이는 부모 줄을 채우고, 폭은 씬 값을 그대로 쓴다.
    private static void PlaceDate(TMP_Text text, float x)
    {
        RectTransform rect = text.rectTransform;

        rect.anchorMin        = new Vector2(x, 0f);
        rect.anchorMax        = new Vector2(x, 1f);
        rect.anchoredPosition = Vector2.zero;
    }

    // "10/14" — 그날 시작은 한국 자정이라 한국 날짜로 적는다(UTC + 9).
    private static string FormatDate(long dayStartUnixMs)
    {
        DateTime korea = DateTimeOffset.FromUnixTimeMilliseconds(dayStartUnixMs).UtcDateTime.AddHours(9);

        return $"{korea.Month}/{korea.Day}";
    }
}
