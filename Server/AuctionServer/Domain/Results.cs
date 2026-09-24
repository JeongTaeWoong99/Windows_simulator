namespace AuctionServer;

/// <summary>값은 proto(<c>RegisterResult</c>)와 같다.</summary>
public enum RegisterResult
{
    Ok       = 0,
    Rejected = 1,
}

/// <summary>값은 proto(<c>ReserveResult</c>)와 같다.</summary>
public enum ReserveResult
{
    Ok           = 0,
    NotFound     = 1,
    InProgress   = 2,
    Sold         = 3,
    Closed       = 4,
    PriceChanged = 5,
    OwnListing   = 6,
    NotEnough    = 7,   // 거래소 — 가격 상한 안에서 원하는 수량을 다 채울 수 없다
}

/// <summary>예약 한 줄 — 이 매물에서 이 수량을 이 단가로 잡았다.</summary>
public sealed record Allocation(long ListingId, long SellerId, int Quantity, long UnitPrice)
{
    public long Price => UnitPrice * Quantity;
}

public sealed record ReserveOutcome(ReserveResult Result, long TotalPrice = 0, IReadOnlyList<Allocation>? Allocations = null)
{
    public IReadOnlyList<Allocation> Lines => Allocations ?? Array.Empty<Allocation>();

    /// <summary>매물 하나를 통째로 산 경우의 판매자(경매장 즉시구매).</summary>
    public long SellerId => Lines.Count > 0 ? Lines[0].SellerId : 0;
}

/// <summary>값은 proto(<c>CancelResult</c>)와 같다.</summary>
public enum CancelResult
{
    Ok         = 0,
    NotFound   = 1,
    NotOwner   = 2,
    InProgress = 3,
    Closed     = 4,
}

/// <summary>메인이 가져갈 이벤트 종류. 값은 DB·proto(<c>EventKind</c>)와 같다.</summary>
public enum AuctionEventKind
{
    Cancelled = 1,
    Expired   = 2,
}

public sealed record AuctionEvent(long EventId, AuctionEventKind Kind, long ListingId, long SellerId);

/// <summary>거래소 목록의 한 줄 — 아이템 종류별 요약. 시세가 없으면 0이다.</summary>
public sealed record MarketItem(
    int  Tid,
    int  Category,
    int  Rarity,
    long LowestUnitPrice,
    long Available,
    long RecentUnitPrice,
    long YesterdayAvgPrice);

/// <summary>가격대 한 칸 — 이 단가에 판매 중인 수량.</summary>
public sealed record PriceLevel(long UnitPrice, long Quantity);

/// <summary>종류별 시세 — 마지막 체결 단가와 전일 평균 단가(수량 가중).</summary>
public sealed record MarketStat(int Tid, long RecentUnitPrice, long YesterdayAvgPrice);
