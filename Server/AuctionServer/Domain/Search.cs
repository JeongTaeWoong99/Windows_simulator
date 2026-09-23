namespace AuctionServer;

/// <summary>
/// 검색 조건. 0·빈 목록은 "조건 없음"이다. 정렬은 단가 오름차순 하나뿐이다.
/// 커서는 마지막으로 본 (단가, 매물 ID) — 첫 페이지는 둘 다 0이다.
/// </summary>
public sealed record SearchQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize     = 50;

    public ListingKind?         Kind            { get; init; }
    public int                  Category        { get; init; }
    public IReadOnlyCollection<int> Tids        { get; init; } = Array.Empty<int>();
    public int                  MinRarity       { get; init; }
    public int                  MaxRarity       { get; init; }
    public int                  MinEnchantGrade { get; init; }
    public IReadOnlyCollection<int> OptionTids  { get; init; } = Array.Empty<int>();
    public long                 MaxUnitPrice    { get; init; }
    public long                 CursorUnitPrice { get; init; }
    public long                 CursorListingId { get; init; }
    public int                  PageSize        { get; init; } = DefaultPageSize;
}

public sealed record SearchPage(IReadOnlyList<Listing> Listings, bool HasMore);
