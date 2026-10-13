using System;
using System.Collections.Generic;
using MikaProtocol;
using TMPro;
using UnityEngine;

// 시세 탭의 14일 그래프 — 날마다 띠(그날 최저~최고) · 점(평균) · 평균선 · 아래 날짜, 왼쪽 가격 눈금 4줄.
// 'AuctionPricePresenter'가 일별 시세를 'Bind'로 넘긴다(종속 View). 칸은 씬에 14개 만들어 두고 앵커로 자리만 옮긴다.
//
// ■ 체결이 없는 날은 띠·점을 숨기고 선은 건너뛴다 — 0을 그리면 그래프가 바닥으로 꺼져 시세가 폭락한 것처럼 보인다.
// ■ 세로 범위는 보이는 날의 최저 × 0.95 ~ 최고 × 1.05 — 목업과 같다.
public class AuctionPriceChartView : MonoBehaviour
{
    [CenterHeader("그리는 칸")]
    [SerializeField, NonReorderable, Tooltip("날마다의 띠(최저~최고) — 오래된 날부터. 부모는 그래프 영역")]
    private RectTransform[] bands = new RectTransform[0];

    [SerializeField, NonReorderable, Tooltip("날마다의 평균 점 — 오래된 날부터")]
    private RectTransform[] dots = new RectTransform[0];

    [SerializeField, NonReorderable, Tooltip("날짜 글자 — 오래된 날부터. 하루 걸러 적는다(마지막 날은 언제나)")]
    private TMP_Text[] dateTexts = new TMP_Text[0];

    [SerializeField, NonReorderable, Tooltip("가격 눈금 — 아래부터 위로")]
    private TMP_Text[] axisTexts = new TMP_Text[0];

    [SerializeField, Tooltip("평균선")]
    private AuctionPriceLineGraphic line = null!;

    [SerializeField, Tooltip("체결이 하루도 없을 때만 보인다")]
    private GameObject emptyText = null!;

    // 띠의 폭(px).
    private const float BandWidth = 12f;

    // 점의 크기(px).
    private const float DotSize = 9f;

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
        long min = long.MaxValue;
        long max = 0L;

        foreach (MarketDailyPriceInfo day in days)
        {
            if (day.Count <= 0L)
            {
                continue;
            }

            min = Math.Min(min, day.MinUnitPrice);
            max = Math.Max(max, day.MaxUnitPrice);
        }

        bool hasAny = max > 0L;

        emptyText.SetActive(!hasAny);
        _linePoints.Clear();

        double low  = hasAny ? min * 0.95d : 0d;
        double high = hasAny ? Math.Max(max * 1.05d, low + 1d) : 1d;

        float Y(long price) => (float)((price - low) / (high - low));

        int slots = Math.Max(bands.Length, 2);

        for (int i = 0; i < bands.Length; i++)
        {
            MarketDailyPriceInfo? day = i < days.Count ? days[i] : null;
            bool                  on  = day != null && day.Count > 0L;
            float                 x   = i / (float)(slots - 1);

            bands[i].gameObject.SetActive(on);

            if (i < dots.Length)
            {
                dots[i].gameObject.SetActive(on);
            }

            if (i < dateTexts.Length)
            {
                bool label = day != null && (i % 2 == 0 || i == days.Count - 1);

                dateTexts[i].text = label ? FormatDate(day!.DayStartUnixMs) : "";
            }

            if (!on)
            {
                continue;
            }

            Place(bands[i], x, Y(day!.MinUnitPrice), Y(day.MaxUnitPrice), BandWidth);

            if (i < dots.Length)
            {
                float y = Y(day.AvgUnitPrice);

                Place(dots[i], x, y, y, DotSize);
                _linePoints.Add(new Vector2(x, y));
            }
        }

        line.SetPoints(_linePoints);

        for (int i = 0; i < axisTexts.Length; i++)
        {
            double value = low + (high - low) * i / Math.Max(axisTexts.Length - 1, 1);

            axisTexts[i].text = hasAny ? $"{(long)value:N0}" : "";
        }
    }

    // 칸 하나를 (x, 아래~위) 자리에 놓는다 — 앵커로 정하고 폭만 픽셀로. 위·아래가 같으면 정사각 점이다.
    private static void Place(RectTransform target, float x, float bottom, float top, float width)
    {
        target.anchorMin = new Vector2(x, bottom);
        target.anchorMax = new Vector2(x, top);
        target.sizeDelta = new Vector2(width, Mathf.Approximately(bottom, top) ? width : 0f);
        target.anchoredPosition = Vector2.zero;
    }

    // "10/14" — 그날 시작은 한국 자정이라 한국 날짜로 적는다(UTC + 9).
    private static string FormatDate(long dayStartUnixMs)
    {
        DateTime korea = DateTimeOffset.FromUnixTimeMilliseconds(dayStartUnixMs).UtcDateTime.AddHours(9);

        return $"{korea.Month}/{korea.Day}";
    }
}
