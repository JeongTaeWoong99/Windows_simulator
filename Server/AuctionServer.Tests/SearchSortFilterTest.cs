namespace AuctionServer.Tests;

/// <summary>
/// 검색 확장(T-155) — 정렬 · 결과 건수 · 단가 범위 · 수치 속성(<see cref="Listing.Attrs"/>) 필터.
/// 정렬은 받은 것 중이 아니라 <b>전체 순서</b>로 페이지를 넘겨야 한다 — 틀리면 2쪽에 1쪽보다 비싼 매물이 섞이거나 같은 매물이 두 번 나온다.
/// </summary>
public class SearchSortFilterTest
{
    private static readonly DateTime Now = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    private const int Level  = 10;   // 속성 키는 메인이 정한다 — 경매장은 숫자로만 본다
    private const int Speed  = 2;

    private static Listing Equip(long id, long unitPrice, int rarity = 3, int hours = 10, Dictionary<int, long>? attrs = null)
        => new(id, SellerId: 100, ListingKind.Equip, Tid: 5001, Category: 1, rarity, Count: 1, EnchantGrade: 0, Array.Empty<int>(),
               unitPrice, Now.AddHours(hours)) { Attrs = attrs ?? new Dictionary<int, long>() };

    private static ListingIndex Index(params Listing[] listings)
        => listings.Aggregate(ListingIndex.Empty, (index, l) => index.Add(l));

    private static List<long> Ids(SearchPage page) => page.Listings.Select(l => l.ListingId).ToList();

    // 첫 쪽 → 응답 커서로 다음 쪽 → … 끝까지 이어 붙인다.
    private static List<long> AllPages(ListingIndex index, SearchQuery query)
    {
        var ids  = new List<long>();
        var page = index.Search(query with { PageSize = 2 }, Now);
        ids.AddRange(Ids(page));
        while (page.HasMore)
        {
            page = index.Search(query with { PageSize = 2, CursorSortValue = page.NextCursorSortValue, CursorListingId = page.NextCursorListingId }, Now);
            ids.AddRange(Ids(page));
        }

        return ids;
    }

    // ── 정렬 ──

    [Fact]
    public void 높은_가격순은_전체에서_비싼_것부터_쪽을_넘긴다()
    {
        var index = Index(Equip(1, 300), Equip(2, 500), Equip(3, 100), Equip(4, 400), Equip(5, 200));

        AllPages(index, new SearchQuery { Sort = SearchSort.PriceDesc }).ShouldBe(new long[] { 2, 4, 1, 5, 3 });
    }

    [Fact]
    public void 마감_임박순은_만료가_가까운_것부터다()
    {
        var index = Index(Equip(1, 100, hours: 30), Equip(2, 100, hours: 5), Equip(3, 100, hours: 12));

        AllPages(index, new SearchQuery { Sort = SearchSort.ExpiresSoon }).ShouldBe(new long[] { 2, 3, 1 });
    }

    [Fact]
    public void 등급_높은순은_같은_등급이면_매물_ID_순이다()
    {
        var index = Index(Equip(1, 100, rarity: 2), Equip(2, 100, rarity: 5), Equip(3, 100, rarity: 2), Equip(4, 100, rarity: 4));

        AllPages(index, new SearchQuery { Sort = SearchSort.RarityDesc }).ShouldBe(new long[] { 2, 4, 1, 3 });
    }

    [Fact]
    public void 속성_내림차순은_속성이_없는_매물을_0으로_보고_맨_뒤에_둔다()
    {
        var index = Index(
            Equip(1, 100, attrs: new() { [Level] = 7 }),
            Equip(2, 100),
            Equip(3, 100, attrs: new() { [Level] = 30 }),
            Equip(4, 100, attrs: new() { [Level] = 12 }));

        AllPages(index, new SearchQuery { Sort = SearchSort.AttrDesc, SortAttr = Level }).ShouldBe(new long[] { 3, 4, 1, 2 });
    }

    [Fact]
    public void 기본_정렬은_예전처럼_단가_낮은순이고_커서도_단가다()
    {
        var index = Index(Equip(1, 300), Equip(2, 100), Equip(3, 200));

        var page = index.Search(new SearchQuery { PageSize = 1 }, Now);

        (Ids(page).Single(), page.NextCursorSortValue, page.NextCursorListingId).ShouldBe((2L, 100L, 2L));
    }

    // ── 건수 ──

    [Fact]
    public void 결과_건수는_쪽과_상관없이_조건에_맞는_전부다()
    {
        var index = Index(Equip(1, 100), Equip(2, 200), Equip(3, 300), Equip(4, 900));

        var page = index.Search(new SearchQuery { PageSize = 2, MaxUnitPrice = 300 }, Now);

        (page.Listings.Count, page.TotalCount, page.HasMore).ShouldBe((2, 3, true));
    }

    // ── 범위 · 속성 필터 ──

    [Fact]
    public void 단가_하한보다_싼_매물은_빠진다()
    {
        var index = Index(Equip(1, 100), Equip(2, 200), Equip(3, 300));

        Ids(index.Search(new SearchQuery { MinUnitPrice = 200, MaxUnitPrice = 250 }, Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void 속성_필터는_하한과_상한을_함께_건다()
    {
        // 작업속도 150 이상 · 400 이하 — 100은 모자라고 500은 넘는다.
        var index = Index(
            Equip(1, 100, attrs: new() { [Speed] = 100 }),
            Equip(2, 100, attrs: new() { [Speed] = 150 }),
            Equip(3, 100, attrs: new() { [Speed] = 400 }),
            Equip(4, 100, attrs: new() { [Speed] = 500 }));

        var query = new SearchQuery { Attrs = new[] { new AttrRange(Speed, Min: 150, Max: 400) } };

        Ids(index.Search(query, Now)).ShouldBe(new long[] { 2, 3 });
    }

    [Fact]
    public void 상한이_0이면_하한만_본다()
    {
        var index = Index(Equip(1, 100, attrs: new() { [Level] = 5 }), Equip(2, 100, attrs: new() { [Level] = 99 }));

        Ids(index.Search(new SearchQuery { Attrs = new[] { new AttrRange(Level, Min: 10, Max: 0) } }, Now)).ShouldBe(new long[] { 2 });
    }
}
