namespace AuctionServer.Tests;

/// <summary>
/// 메모리 검색 인덱스. 단가순 정렬·필터·커서 페이지가 틀리면 싼 매물이 안 보이거나(구매 기회 손실)
/// 페이지를 넘길 때 같은 매물이 두 번 나오거나 빠진다.
/// </summary>
public class ListingIndexTest
{
    private static readonly DateTime Now = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    private static Listing Item(long id, long unitPrice, int tid = 10001, int category = 1, int rarity = 1, int count = 1)
        => new(id, SellerId: 100, ListingKind.Item, tid, category, rarity, count, EnchantGrade: 0, Array.Empty<int>(), unitPrice, Now.AddHours(1));

    private static Listing Equip(long id, long unitPrice, int grade, params int[] options)
        => new(id, SellerId: 100, ListingKind.Equip, Tid: 5001, Category: 1, Rarity: 3, Count: 1, grade, options, unitPrice, Now.AddHours(1));

    private static List<long> Ids(SearchPage page) => page.Listings.Select(l => l.ListingId).ToList();

    [Fact]
    public void 단가가_낮은_순으로_나온다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 300)).Add(Item(2, 100)).Add(Item(3, 200));

        Ids(index.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 2, 3, 1 });
    }

    [Fact]
    public void 단가가_같으면_매물_ID_순이다()
    {
        var index = ListingIndex.Empty.Add(Item(9, 100)).Add(Item(4, 100)).Add(Item(7, 100));

        Ids(index.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 4, 7, 9 });
    }

    [Fact]
    public void 자원_묶음은_총액이_아니라_단가로_정렬된다()
    {
        // 1번: 10개 × 50 = 500 / 2번: 1개 × 80 = 80. 총액은 2번이 싸지만 단가는 1번이 싸다.
        var index = ListingIndex.Empty.Add(Item(1, 50, count: 10)).Add(Item(2, 80, count: 1));

        Ids(index.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 1, 2 });
    }

    [Fact]
    public void 뺀_매물은_검색에_안_나온다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(2, 200)).Remove(1);

        Ids(index.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void 없는_매물을_빼도_그대로다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100));

        index.Remove(99).Count.ShouldBe(1);
    }

    [Fact]
    public void 새_버전을_만들어도_이전_버전은_바뀌지_않는다()
    {
        var before = ListingIndex.Empty.Add(Item(1, 100));
        before.Add(Item(2, 50)).Remove(1);

        Ids(before.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 1 });
    }

    [Fact]
    public void 종류와_분류로_거른다()
    {
        var index = ListingIndex.Empty
            .Add(Item(1, 100, category: 1))
            .Add(Item(2, 100, category: 2))
            .Add(Equip(3, 100, grade: 0));

        var query = new SearchQuery { Kind = ListingKind.Item, Category = 2 };

        Ids(index.Search(query, Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void 분류_없이_종류만으로도_거른다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Equip(2, 50, grade: 0));

        Ids(index.Search(new SearchQuery { Kind = ListingKind.Equip }, Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void TID_목록에_든_매물만_나온다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100, tid: 10001)).Add(Item(2, 100, tid: 10002)).Add(Item(3, 100, tid: 10003));

        Ids(index.Search(new SearchQuery { Tids = new[] { 10001, 10003 } }, Now)).ShouldBe(new long[] { 1, 3 });
    }

    [Fact]
    public void 희귀도_범위로_거른다()
    {
        var index = ListingIndex.Empty
            .Add(Item(1, 100, rarity: 1)).Add(Item(2, 100, rarity: 3)).Add(Item(3, 100, rarity: 5));

        Ids(index.Search(new SearchQuery { MinRarity = 2, MaxRarity = 4 }, Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void 인챈트_등급_하한으로_거른다()
    {
        var index = ListingIndex.Empty.Add(Equip(1, 100, grade: 2)).Add(Equip(2, 100, grade: 4));

        Ids(index.Search(new SearchQuery { MinEnchantGrade = 3 }, Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void 옵션은_요청한_것을_전부_가진_매물만_나온다()
    {
        var index = ListingIndex.Empty
            .Add(Equip(1, 100, 3, 701, 702))
            .Add(Equip(2, 100, 3, 701))
            .Add(Equip(3, 100, 3, 702, 703, 701));

        Ids(index.Search(new SearchQuery { OptionTids = new[] { 701, 702 } }, Now)).ShouldBe(new long[] { 1, 3 });
    }

    [Fact]
    public void 단가_상한을_넘는_매물은_안_나온다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(2, 101));

        Ids(index.Search(new SearchQuery { MaxUnitPrice = 100 }, Now)).ShouldBe(new long[] { 1 });
    }

    [Fact]
    public void 만료_시각이_지난_매물은_청소_전이라도_안_나온다()
    {
        var expired = Item(1, 100) with { ExpiresAt = Now };
        var index   = ListingIndex.Empty.Add(expired).Add(Item(2, 200));

        Ids(index.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public void 페이지를_채우면_더_있다고_알린다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(2, 200)).Add(Item(3, 300));

        var page = index.Search(new SearchQuery { PageSize = 2 }, Now);

        Ids(page).ShouldBe(new long[] { 1, 2 });
        page.HasMore.ShouldBeTrue();
    }

    [Fact]
    public void 마지막_페이지는_더_없다고_알린다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(2, 200));

        index.Search(new SearchQuery { PageSize = 2 }, Now).HasMore.ShouldBeFalse();
    }

    [Fact]
    public void 커서_다음부터_이어서_나온다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(2, 100)).Add(Item(3, 200));

        var page = index.Search(new SearchQuery { CursorUnitPrice = 100, CursorListingId = 1 }, Now);

        Ids(page).ShouldBe(new long[] { 2, 3 });
    }

    [Fact]
    public void 페이지_사이에_앞쪽_매물이_빠져도_다음_페이지가_밀리지_않는다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(2, 200)).Add(Item(3, 300)).Add(Item(4, 400));
        var first = index.Search(new SearchQuery { PageSize = 2 }, Now);
        var last  = first.Listings[^1];

        // 첫 페이지를 본 뒤 1번이 팔렸다. 오프셋(2번째부터)이면 3번을 건너뛴다.
        var second = index.Remove(1).Search(
            new SearchQuery { PageSize = 2, CursorUnitPrice = last.UnitPrice, CursorListingId = last.ListingId }, Now);

        Ids(second).ShouldBe(new long[] { 3, 4 });
    }

    [Fact]
    public void 페이지_크기는_상한으로_자른다()
    {
        var index = ListingIndex.Empty;
        for (var i = 1; i <= 60; i++)
        {
            index = index.Add(Item(i, i));
        }

        index.Search(new SearchQuery { PageSize = 1000 }, Now).Listings.Count.ShouldBe(50);
    }

    [Fact]
    public void 페이지_크기가_0_이하면_기본값을_쓴다()
    {
        var index = ListingIndex.Empty;
        for (var i = 1; i <= 30; i++)
        {
            index = index.Add(Item(i, i));
        }

        index.Search(new SearchQuery { PageSize = 0 }, Now).Listings.Count.ShouldBe(20);
    }

    [Fact]
    public void 같은_매물_ID를_다시_넣으면_새_값으로_바뀐다()
    {
        var index = ListingIndex.Empty.Add(Item(1, 100)).Add(Item(1, 300)).Add(Item(2, 200));

        Ids(index.Search(new SearchQuery(), Now)).ShouldBe(new long[] { 2, 1 });
        index.Count.ShouldBe(2);
    }
}
