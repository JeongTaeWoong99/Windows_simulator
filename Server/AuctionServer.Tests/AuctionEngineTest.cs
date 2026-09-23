namespace AuctionServer.Tests;

/// <summary>
/// 경매장 쓰기 경로 — 등록·예약·확정·취소·만료·예약 타임아웃·재시작.
/// 여기가 틀리면 한 매물이 두 번 팔리거나(골드·아이템 복제), 잠긴 아이템이 영원히 풀리지 않는다.
/// </summary>
public class AuctionEngineTest : IAsyncLifetime
{
    private readonly EngineFixture _f = new();

    private AuctionEngine Engine => _f.Engine;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _f.DisposeAsync();

    // ── 등록 ──

    [Fact]
    public async Task 등록한_매물은_검색에_나온다()
    {
        (await Engine.RegisterAsync(_f.Item(1))).ShouldBe(RegisterResult.Ok);

        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 같은_매물을_두_번_등록해도_한_건이다()
    {
        await Engine.RegisterAsync(_f.Item(1));

        (await Engine.RegisterAsync(_f.Item(1))).ShouldBe(RegisterResult.Ok);
        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 팔린_매물의_등록이_재전송돼도_다시_살아나지_않는다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(purchaseId: 9, listingId: 1, buyerId: 200, expectedTotal: 100);
        await Engine.ConfirmAsync(9, success: true);

        await Engine.RegisterAsync(_f.Item(1));

        _f.SearchIds().ShouldBeEmpty();
    }

    [Fact]
    public async Task 수량이_0인_매물은_거부한다()
    {
        (await Engine.RegisterAsync(_f.Item(1, count: 0))).ShouldBe(RegisterResult.Rejected);
    }

    [Fact]
    public async Task 단가가_0인_매물은_거부한다()
    {
        (await Engine.RegisterAsync(_f.Item(1, unitPrice: 0))).ShouldBe(RegisterResult.Rejected);
    }

    [Fact]
    public async Task 장비는_한_개가_아니면_거부한다()
    {
        (await Engine.RegisterAsync(_f.Equip(1) with { Count = 2 })).ShouldBe(RegisterResult.Rejected);
    }

    [Fact]
    public async Task 옵션이_네_줄이면_거부한다()
    {
        (await Engine.RegisterAsync(_f.Equip(1, options: new[] { 1, 2, 3, 4 }))).ShouldBe(RegisterResult.Rejected);
    }

    [Fact]
    public async Task 이미_만료_시각이_지난_매물은_거부한다()
    {
        (await Engine.RegisterAsync(_f.Item(1) with { ExpiresAt = _f.Now })).ShouldBe(RegisterResult.Rejected);
    }

    [Fact]
    public async Task 거부한_매물은_검색에_없다()
    {
        await Engine.RegisterAsync(_f.Item(1, count: 0));

        _f.SearchIds().ShouldBeEmpty();
    }

    // ── 예약 ──

    [Fact]
    public async Task 예약하면_판매자와_총액을_돌려준다()
    {
        await Engine.RegisterAsync(_f.Item(1, unitPrice: 30, count: 7, seller: 100));

        var outcome = await Engine.ReserveAsync(9, 1, buyerId: 200, expectedTotal: 210);

        // 30 × 7 = 210
        outcome.ShouldBe(new ReserveOutcome(ReserveResult.Ok, SellerId: 100, TotalPrice: 210));
    }

    [Fact]
    public async Task 예약한_매물은_검색에서_빠진다()
    {
        await Engine.RegisterAsync(_f.Item(1));

        await Engine.ReserveAsync(9, 1, 200, 100);

        _f.SearchIds().ShouldBeEmpty();
    }

    [Fact]
    public async Task 예약_중인_매물을_다른_사람이_사면_구매_중이다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);

        (await Engine.ReserveAsync(10, 1, 300, 100)).Result.ShouldBe(ReserveResult.InProgress);
    }

    [Fact]
    public async Task 팔린_매물을_사면_판매_완료다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        await Engine.ConfirmAsync(9, true);

        (await Engine.ReserveAsync(10, 1, 300, 100)).Result.ShouldBe(ReserveResult.Sold);
    }

    [Fact]
    public async Task 없는_매물을_사면_없음이다()
    {
        (await Engine.ReserveAsync(9, 404, 200, 100)).Result.ShouldBe(ReserveResult.NotFound);
    }

    [Fact]
    public async Task 본_가격과_다르면_가격_변경이다()
    {
        await Engine.RegisterAsync(_f.Item(1, unitPrice: 100));

        (await Engine.ReserveAsync(9, 1, 200, expectedTotal: 99)).Result.ShouldBe(ReserveResult.PriceChanged);
    }

    [Fact]
    public async Task 가격이_달라_거절된_매물은_계속_팔린다()
    {
        await Engine.RegisterAsync(_f.Item(1, unitPrice: 100));
        await Engine.ReserveAsync(9, 1, 200, expectedTotal: 99);

        (await Engine.ReserveAsync(10, 1, 200, 100)).Result.ShouldBe(ReserveResult.Ok);
    }

    [Fact]
    public async Task 자기_매물은_살_수_없다()
    {
        await Engine.RegisterAsync(_f.Item(1, seller: 100));

        (await Engine.ReserveAsync(9, 1, buyerId: 100, 100)).Result.ShouldBe(ReserveResult.OwnListing);
    }

    [Fact]
    public async Task 취소된_매물을_사면_종료다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.CancelAsync(1, 100);

        (await Engine.ReserveAsync(9, 1, 200, 100)).Result.ShouldBe(ReserveResult.Closed);
    }

    [Fact]
    public async Task 만료_시각이_지난_매물은_청소_전이라도_살_수_없다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        _f.Advance(TimeSpan.FromHours(48));

        (await Engine.ReserveAsync(9, 1, 200, 100)).Result.ShouldBe(ReserveResult.Closed);
    }

    [Fact]
    public async Task 같은_구매_ID로_다시_예약하면_처음_결과를_돌려준다()
    {
        await Engine.RegisterAsync(_f.Item(1, seller: 100));
        await Engine.ReserveAsync(9, 1, 200, 100);

        // 메인이 응답을 못 받고 재시도한 경우 — 자기 예약에 "구매 중"이라고 답하면 안 된다.
        (await Engine.ReserveAsync(9, 1, 200, 100)).ShouldBe(new ReserveOutcome(ReserveResult.Ok, 100, 100));
    }

    [Fact]
    public async Task 동시에_여러_명이_사도_한_명만_예약한다()
    {
        await Engine.RegisterAsync(_f.Item(1));

        var attempts = Enumerable.Range(0, 20)
            .Select(i => Engine.ReserveAsync(purchaseId: 1000 + i, listingId: 1, buyerId: 200 + i, expectedTotal: 100));
        var outcomes = await Task.WhenAll(attempts);

        outcomes.Count(o => o.Result == ReserveResult.Ok).ShouldBe(1);
    }

    // ── 확정 ──

    [Fact]
    public async Task 정산_실패면_다시_검색에_나온다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);

        await Engine.ConfirmAsync(9, success: false);

        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 정산_실패로_풀린_매물은_다른_사람이_살_수_있다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        await Engine.ConfirmAsync(9, false);

        (await Engine.ReserveAsync(10, 1, 300, 100)).Result.ShouldBe(ReserveResult.Ok);
    }

    [Fact]
    public async Task 확정은_두_번_와도_한_번만_반영된다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        await Engine.ConfirmAsync(9, false);
        await Engine.ReserveAsync(10, 1, 300, 100);

        // 9번 실패가 재전송됐다. 10번의 예약을 풀면 안 된다.
        await Engine.ConfirmAsync(9, false);

        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Reserved);
    }

    [Fact]
    public async Task 예약_타임아웃_뒤에_늦게_온_성공도_판매로_반영한다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        _f.Advance(_f.Options.ReservationTimeout);
        await Engine.SweepAsync();

        // 메인은 이미 정산을 커밋했다 — 그 판매는 일어난 사실이다.
        await Engine.ConfirmAsync(9, true);

        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Sold);
        _f.SearchIds().ShouldBeEmpty();
    }

    [Fact]
    public async Task 예약_타임아웃_뒤에_늦게_온_실패는_다른_예약을_풀지_않는다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        _f.Advance(_f.Options.ReservationTimeout);
        await Engine.SweepAsync();
        await Engine.ReserveAsync(10, 1, 300, 100);

        await Engine.ConfirmAsync(9, false);

        (await Engine.ReserveAsync(11, 1, 400, 100)).Result.ShouldBe(ReserveResult.InProgress);
    }

    [Fact]
    public async Task 모르는_구매의_확정은_무시한다()
    {
        await Engine.RegisterAsync(_f.Item(1));

        await Engine.ConfirmAsync(404, true);

        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    // ── 취소 ──

    [Fact]
    public async Task 취소하면_검색에서_빠지고_이벤트가_남는다()
    {
        await Engine.RegisterAsync(_f.Item(1, seller: 100));

        (await Engine.CancelAsync(1, 100)).ShouldBe(CancelResult.Ok);

        _f.SearchIds().ShouldBeEmpty();
        (await Engine.FetchEventsAsync(10)).Select(e => (e.Kind, e.ListingId, e.SellerId))
            .ShouldBe(new[] { (AuctionEventKind.Cancelled, 1L, 100L) });
    }

    [Fact]
    public async Task 남의_매물은_취소할_수_없다()
    {
        await Engine.RegisterAsync(_f.Item(1, seller: 100));

        (await Engine.CancelAsync(1, 999)).ShouldBe(CancelResult.NotOwner);
    }

    [Fact]
    public async Task 구매_중인_매물은_취소할_수_없다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);

        (await Engine.CancelAsync(1, 100)).ShouldBe(CancelResult.InProgress);
    }

    [Fact]
    public async Task 팔린_매물은_취소할_수_없다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        await Engine.ConfirmAsync(9, true);

        (await Engine.CancelAsync(1, 100)).ShouldBe(CancelResult.Closed);
    }

    [Fact]
    public async Task 없는_매물은_취소할_수_없다()
    {
        (await Engine.CancelAsync(404, 100)).ShouldBe(CancelResult.NotFound);
    }

    [Fact]
    public async Task 취소를_두_번_보내도_이벤트는_하나다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.CancelAsync(1, 100);

        (await Engine.CancelAsync(1, 100)).ShouldBe(CancelResult.Ok);
        (await Engine.FetchEventsAsync(10)).Count.ShouldBe(1);
    }

    // ── 만료 ──

    [Fact]
    public async Task 만료_시각이_지나면_청소가_만료_이벤트를_남긴다()
    {
        await Engine.RegisterAsync(_f.Item(1, seller: 100));
        _f.Advance(TimeSpan.FromHours(48));

        await Engine.SweepAsync();

        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Expired);
        (await Engine.FetchEventsAsync(10)).Select(e => (e.Kind, e.ListingId))
            .ShouldBe(new[] { (AuctionEventKind.Expired, 1L) });
    }

    [Fact]
    public async Task 만료_전이면_청소해도_그대로다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        _f.Advance(TimeSpan.FromHours(47));

        await Engine.SweepAsync();

        (await Engine.FetchEventsAsync(10)).ShouldBeEmpty();
        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 예약_중인_매물은_만료되지_않는다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        _f.Advance(TimeSpan.FromHours(48) - TimeSpan.FromSeconds(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        _f.Advance(TimeSpan.FromSeconds(30));

        await Engine.SweepAsync();

        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Reserved);
    }

    [Fact]
    public async Task 예약_타임아웃이_지나면_다시_팔린다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        _f.Advance(_f.Options.ReservationTimeout);

        await Engine.SweepAsync();

        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 예약_타임아웃_전이면_예약이_유지된다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);
        _f.Advance(_f.Options.ReservationTimeout - TimeSpan.FromSeconds(1));

        await Engine.SweepAsync();

        _f.SearchIds().ShouldBeEmpty();
    }

    // ── 이벤트 ──

    [Fact]
    public async Task 확인한_이벤트는_다시_오지_않는다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.RegisterAsync(_f.Item(2));
        await Engine.CancelAsync(1, 100);
        await Engine.CancelAsync(2, 100);
        var events = await Engine.FetchEventsAsync(10);

        await Engine.AckEventsAsync(new[] { events[0].EventId });

        (await Engine.FetchEventsAsync(10)).Select(e => e.ListingId).ShouldBe(new long[] { 2 });
    }

    [Fact]
    public async Task 이벤트는_요청한_개수만큼만_온다()
    {
        for (var id = 1; id <= 5; id++)
        {
            await Engine.RegisterAsync(_f.Item(id));
            await Engine.CancelAsync(id, 100);
        }

        (await Engine.FetchEventsAsync(3)).Select(e => e.ListingId).ShouldBe(new long[] { 1, 2, 3 });
    }

    // ── 조회 ──

    [Fact]
    public async Task 모르는_매물의_상태는_null이다()
    {
        (await Engine.GetStatesAsync(new long[] { 404 }))[404].ShouldBeNull();
    }

    [Fact]
    public async Task 내_매물은_판매_중과_구매_중만_보인다()
    {
        await Engine.RegisterAsync(_f.Item(1, seller: 100));
        await Engine.RegisterAsync(_f.Item(2, seller: 100));
        await Engine.RegisterAsync(_f.Item(3, seller: 100));
        await Engine.RegisterAsync(_f.Item(4, seller: 555));
        await Engine.ReserveAsync(9, 2, 200, 100);
        await Engine.CancelAsync(3, 100);

        var mine = await Engine.GetSellerListingsAsync(100);

        mine.Select(m => (m.Listing.ListingId, m.State))
            .ShouldBe(new[] { (1L, ListingState.Listed), (2L, ListingState.Reserved) });
    }

    // ── 재시작 ──

    [Fact]
    public async Task 재시작하면_판매_중인_매물로_인덱스를_다시_만든다()
    {
        await Engine.RegisterAsync(_f.Equip(1, unitPrice: 300, grade: 4, options: new[] { 701, 702 }));
        await Engine.RegisterAsync(_f.Item(2, unitPrice: 100));
        await Engine.RegisterAsync(_f.Item(3, unitPrice: 200));
        await Engine.CancelAsync(3, 100);

        await _f.Restart();

        _f.SearchIds().ShouldBe(new long[] { 2, 1 });
        _f.SearchIds(new SearchQuery { OptionTids = new[] { 702 }, MinEnchantGrade = 4 }).ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 재시작해도_예약은_유지되고_타임아웃으로_풀린다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.ReserveAsync(9, 1, 200, 100);

        await _f.Restart();
        _f.SearchIds().ShouldBeEmpty();

        _f.Advance(_f.Options.ReservationTimeout);
        await Engine.SweepAsync();
        _f.SearchIds().ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task 재시작해도_확인_안_한_이벤트는_남아_있다()
    {
        await Engine.RegisterAsync(_f.Item(1));
        await Engine.CancelAsync(1, 100);

        await _f.Restart();

        (await Engine.FetchEventsAsync(10)).Select(e => e.ListingId).ShouldBe(new long[] { 1 });
    }
}
