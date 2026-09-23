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
}

public sealed record ReserveOutcome(ReserveResult Result, long SellerId = 0, long TotalPrice = 0);

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
