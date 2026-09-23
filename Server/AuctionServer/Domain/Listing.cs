namespace AuctionServer;

/// <summary>매물 상태. 값은 DB·proto(<c>ListingState</c>)와 같다 — 바꾸지 않는다.</summary>
public enum ListingState
{
    Listed    = 1,
    Reserved  = 2,
    Sold      = 3,
    Cancelled = 4,
    Expired   = 5,
}

/// <summary>매물 종류. 값은 메인(<c>EAuctionKind</c>)과 같다.</summary>
public enum ListingKind
{
    Item  = 1,
    Equip = 2,
}

/// <summary>
/// 등록 스냅샷. 메인이 등록 순간의 아이템 정보를 실어 보낸다 — 경매장은 GameData를 모른다.
/// <see cref="Category"/>는 종류에 따라 ItemType 또는 EquipKind 값이다.
/// </summary>
public sealed record Listing(
    long                ListingId,
    long                SellerId,
    ListingKind         Kind,
    int                 Tid,
    int                 Category,
    int                 Rarity,
    int                 Count,
    int                 EnchantGrade,
    IReadOnlyList<int>  Options,
    long                UnitPrice,
    DateTime            ExpiresAt)
{
    /// <summary>매물 전체 가격. 매물은 통째로만 팔린다.</summary>
    public long TotalPrice => UnitPrice * Count;
}

/// <summary>상태까지 붙인 매물 — 내 매물 목록·대사 응답용.</summary>
public sealed record ListingWithState(Listing Listing, ListingState State);
