using System;
using MikaProtocol;

// 시세 요약 문구 — 구매 확인 창 · 등록 창 · 시세 탭이 같은 말로 시세를 적는다 (T-119 · T-147).
//
// 요약('MarketItemInfo')은 종류·TID 하나의 최저가 · 판매 중 수량 · 최근 거래가 · 전일 평균이다. 매물·체결이 없으면 0으로 온다.
public static class AuctionPriceText
{
    // 단가가 전일 평균과 "같다"로 보는 폭(%) — 목업의 1%.
    private const double SameThresholdPercent = 1d;

    // "최저가 120 G · 판매 중 340개 · 최근 거래가 118 G · 전일 평균 121 G". 요약이 없으면 받지 못했다고 적는다.
    public static string FormatSummaryLine(MarketItemInfo? summary)
    {
        if (summary == null)
        {
            return UIRichText.Label(Lang.Get("UI.Auction.NoSummary"));
        }

        string dot = UIRichText.Dot;

        return $"{UIRichText.Label(Lang.Get("UI.Auction.LowestPrice"))} {FormatPrice(summary.AvailableCount > 0 ? summary.LowestUnitPrice : 0L, Lang.Get("UI.Auction.NoListings"))}" +
               $"{dot}{UIRichText.Label(Lang.Get("UI.Auction.OnSale"))} {Lang.Format("UI.Inventory.CountUnitShort", summary.AvailableCount.ToString("N0"))}" +
               $"{dot}{UIRichText.Label(Lang.Get("UI.Auction.RecentPrice"))} {FormatPrice(summary.RecentUnitPrice, "-")}" +
               $"{dot}{UIRichText.Label(Lang.Get("UI.Auction.YesterdayAvg"))} {FormatPrice(summary.YesterdayAvgPrice, "-")}";
    }

    // "이 매물 단가는 전일 평균보다 12.5% 비쌉니다" — 비싸면 경고색, 싸면 좋은 색. 전일 거래가 없으면 비교할 수 없다고 적는다.
    public static string FormatCompare(long unitPrice, MarketItemInfo? summary)
    {
        if (summary == null || summary.YesterdayAvgPrice <= 0L)
        {
            return UIRichText.Label(Lang.Get("UI.Auction.NoCompare"));
        }

        double percent = (unitPrice - summary.YesterdayAvgPrice) * 100d / summary.YesterdayAvgPrice;

        if (Math.Abs(percent) < SameThresholdPercent)
        {
            return $"{UIRichText.Label(Lang.Get("UI.Auction.ThisUnitPrice"))} {Lang.Get("UI.Auction.SameAsAvg")}";
        }

        string diff = percent > 0d
            ? UIRichText.Paint(Lang.Format("UI.Auction.PricierBy", percent.ToString("0.0")), UIThemeRole.Negative)
            : UIRichText.Paint(Lang.Format("UI.Auction.CheaperBy", (-percent).ToString("0.0")), UIThemeRole.Positive);

        return $"{UIRichText.Label(Lang.Get("UI.Auction.ThanAvg"))} {diff}";
    }

    // 최근 거래가가 전일 평균보다 오른 비율 — "▲ 3.2%" · "▼ 1.0%". 둘 중 하나라도 없으면 빈 문자열.
    public static string FormatTrend(long recent, long yesterdayAvg)
    {
        if (recent <= 0L || yesterdayAvg <= 0L)
        {
            return "";
        }

        double percent = (recent - yesterdayAvg) * 100d / yesterdayAvg;

        return percent >= 0d
            ? UIRichText.Paint($"▲ {percent:0.0}%", UIThemeRole.Positive)
            : UIRichText.Paint($"▼ {-percent:0.0}%", UIThemeRole.Negative);
    }

    // 가격 — 0이면 'empty' 문구.
    private static string FormatPrice(long price, string empty)
        => price > 0L ? UIRichText.Gold(price) : UIRichText.Label(empty);
}
