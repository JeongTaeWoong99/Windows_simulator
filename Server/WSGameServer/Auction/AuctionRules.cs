using GameData;

namespace WSGameServer;

/// <summary>
/// 경매·거래소 수치. 값은 <c>Constants.xlsx</c>에 있다(테스트값 — 기획 거래 4장).
/// 가격 하한이 즉시 판매가(<c>BasePrice</c>)와 같다는 것만은 확정이다 → Server/docs/경매장.md 7장.
/// </summary>
public static class AuctionRules
{
    public static TimeSpan ListingDuration => TimeSpan.FromHours(Constants.AuctionListingHours);

    /// <summary>판매 중 매물 상한 — 1인 등록 건수 제한(시세 조작 대응).</summary>
    public static int MaxActiveListings => (int)Constants.AuctionMaxActiveListings;

    /// <summary>단가 상한 = 하한 × 이 배수(가격 밴드).</summary>
    public static long PriceBandMultiplier => Constants.AuctionPriceBandMultiplier;

    // 천분율(1000 = 100%). 등록비는 만료면 환급, 취소면 환급하지 않는다.
    public static long ListingFeePermille => Constants.AuctionListingFeePermille;
    public static long SaleFeePermille    => Constants.AuctionSaleFeePermille;

    /// <summary>검색 빈도 — 순간 허용 횟수와, 한 번이 회복되는 간격.</summary>
    public static int SearchBurst => (int)Constants.AuctionSearchBurst;
    public static TimeSpan SearchRefill => TimeSpan.FromSeconds(Constants.AuctionSearchRefillSeconds);

    /// <summary>단가 하한. 즉시 판매가가 곧 바닥이다 — 누구도 즉시 판매보다 싸게 올리지 않는다. 0원 아이템도 1은 받는다.</summary>
    public static long MinUnitPrice(int basePrice) => Math.Max(basePrice, 1);

    public static long MaxUnitPrice(int basePrice) => MinUnitPrice(basePrice) * PriceBandMultiplier;

    public static bool IsInBand(int basePrice, long unitPrice)
        => unitPrice >= MinUnitPrice(basePrice) && unitPrice <= MaxUnitPrice(basePrice);

    /// <summary>등록비. 최소 1 — 0이면 등록·취소 반복이 공짜가 된다.</summary>
    public static long ListingFee(long totalPrice) => Math.Max(1, totalPrice * ListingFeePermille / 1000);

    /// <summary>판매 수수료. 판매자 대금에서 뺀다.</summary>
    public static long SaleFee(long totalPrice) => totalPrice * SaleFeePermille / 1000;
}
