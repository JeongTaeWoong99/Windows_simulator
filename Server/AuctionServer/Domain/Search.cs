namespace AuctionServer;

/// <summary>검색 정렬. 같은 값이면 매물 ID 순이다 — 커서가 한 점을 가리키려면 전순서여야 한다 (T-155).</summary>
public enum SearchSort
{
    PriceAsc    = 0,   // 단가 낮은순 (기본)
    PriceDesc   = 1,   // 단가 높은순
    ExpiresSoon = 2,   // 마감 임박순
    RarityDesc  = 3,   // 등급 높은순
    AttrDesc    = 4,   // 속성 큰 순 — SortAttr의 값
}

/// <summary>수치 속성 범위. <see cref="Max"/>가 0이면 상한 없음이다.</summary>
public sealed record AttrRange(int Key, long Min, long Max);

/// <summary>
/// 검색 조건. 0·빈 목록은 "조건 없음"이다.
/// 커서는 마지막으로 본 (정렬 값, 매물 ID) — 첫 페이지는 둘 다 0이다. 단가 낮은순이면 정렬 값이 곧 단가다.
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
    public long                 MinUnitPrice    { get; init; }
    public long                 MaxUnitPrice    { get; init; }
    public IReadOnlyCollection<AttrRange> Attrs { get; init; } = Array.Empty<AttrRange>();
    public SearchSort           Sort            { get; init; }
    public int                  SortAttr        { get; init; }
    public long                 CursorSortValue { get; init; }
    public long                 CursorListingId { get; init; }
    public int                  PageSize        { get; init; } = DefaultPageSize;
}

/// <summary>한 쪽. <see cref="TotalCount"/>는 쪽과 상관없이 조건에 맞는 전부이고, 다음 쪽은 Next 커서로 묻는다.</summary>
public sealed record SearchPage(IReadOnlyList<Listing> Listings, bool HasMore, int TotalCount = 0, long NextCursorSortValue = 0, long NextCursorListingId = 0);
