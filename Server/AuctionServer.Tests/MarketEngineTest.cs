namespace AuctionServer.Tests;

/// <summary>
/// 거래소 — 한 종류를 최저가부터 원하는 수량만큼 여러 매물에 걸쳐 사는 경로, 가격대·시세.
/// 틀리면 비싼 매물부터 팔리거나, 같은 수량이 두 사람에게 잡히거나, 일부 팔린 매물의 나머지가 사라진다.
/// </summary>
public class MarketEngineTest : IAsyncLifetime
{
    private const int Carp = 10001;
    private const int Pike = 10002;

    private readonly EngineFixture _f = new();

    private AuctionEngine Engine => _f.Engine;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Task Seed(long id, long unitPrice, int count, long seller = 100, int tid = Carp)
        => Engine.RegisterAsync(_f.Item(id, unitPrice: unitPrice, count: count, seller: seller, tid: tid));

    private static IEnumerable<(long, int, long)> Lines(ReserveOutcome o) => o.Lines.Select(a => (a.ListingId, a.Quantity, a.UnitPrice));

    // ── 수량 예약 ──

    [Fact]
    public async Task 최저가부터_여러_매물에_걸쳐_잡는다()
    {
        await Seed(1, unitPrice: 32, count: 10);
        await Seed(2, unitPrice: 30, count: 10);

        var outcome = await Engine.ReserveQuantityAsync(9, buyerId: 200, Carp, quantity: 15, maxUnitPrice: 32);

        // 30 × 10 + 32 × 5 = 460
        Lines(outcome).ShouldBe(new[] { (2L, 10, 30L), (1L, 5, 32L) });
        outcome.TotalPrice.ShouldBe(460);
    }

    [Fact]
    public async Task 단가_상한보다_비싼_매물은_잡지_않는다()
    {
        await Seed(1, 30, 10);
        await Seed(2, 33, 10);

        (await Engine.ReserveQuantityAsync(9, 200, Carp, 15, maxUnitPrice: 32)).Result.ShouldBe(ReserveResult.NotEnough);
    }

    [Fact]
    public async Task 다_못_채우면_아무것도_잡지_않는다()
    {
        await Seed(1, 30, 10);

        await Engine.ReserveQuantityAsync(9, 200, Carp, 11, 100);

        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 10) });
    }

    [Fact]
    public async Task 자기_매물은_건너뛴다()
    {
        await Seed(1, 30, 10, seller: 200);
        await Seed(2, 31, 10, seller: 100);

        var outcome = await Engine.ReserveQuantityAsync(9, buyerId: 200, Carp, 5, 100);

        Lines(outcome).ShouldBe(new[] { (2L, 5, 31L) });
    }

    [Fact]
    public async Task 다른_종류는_잡지_않는다()
    {
        await Seed(1, 10, 10, tid: Pike);

        (await Engine.ReserveQuantityAsync(9, 200, Carp, 1, 100)).Result.ShouldBe(ReserveResult.NotEnough);
    }

    [Fact]
    public async Task 잡힌_수량은_가격대에서_빠지고_나머지는_남는다()
    {
        await Seed(1, 30, 10);

        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);

        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 6) });
    }

    [Fact]
    public async Task 잡힌_수량은_다른_구매자가_다시_잡지_못한다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 8, 30);

        (await Engine.ReserveQuantityAsync(10, 300, Carp, 3, 30)).Result.ShouldBe(ReserveResult.NotEnough);
    }

    [Fact]
    public async Task 동시에_여러_명이_사도_수량을_넘겨_잡지_않는다()
    {
        await Seed(1, 30, 10);

        var attempts = Enumerable.Range(0, 20).Select(i => Engine.ReserveQuantityAsync(1000 + i, 200 + i, Carp, 3, 30));
        var outcomes = await Task.WhenAll(attempts);

        // 10개를 3개씩 — 셋만 잡히고 1개가 남는다.
        outcomes.Count(o => o.Result == ReserveResult.Ok).ShouldBe(3);
        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 1) });
    }

    [Fact]
    public async Task 같은_구매_ID의_재시도는_처음_결과를_돌려준다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);

        var again = await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);

        Lines(again).ShouldBe(new[] { (1L, 4, 30L) });
        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 6) });
    }

    [Fact]
    public async Task 수량이_0이면_거절한다()
    {
        await Seed(1, 30, 10);

        (await Engine.ReserveQuantityAsync(9, 200, Carp, 0, 30)).Result.ShouldBe(ReserveResult.NotEnough);
    }

    // ── 확정 ──

    [Fact]
    public async Task 확정하면_남은_수량이_줄고_매물은_계속_팔린다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);

        await Engine.ConfirmAsync(9, true);

        (await Engine.GetSellerListingsAsync(100)).Single().Listing.Count.ShouldBe(6);
        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Listed);
    }

    [Fact]
    public async Task 남은_수량을_다_팔면_판매_완료다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 10, 30);

        await Engine.ConfirmAsync(9, true);

        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Sold);
        Engine.PriceLadder(Carp, 10).ShouldBeEmpty();
    }

    [Fact]
    public async Task 실패로_확정하면_잡은_수량이_돌아온다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);

        await Engine.ConfirmAsync(9, false);

        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 10) });
    }

    [Fact]
    public async Task 예약_타임아웃이면_잡은_수량이_돌아온다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);
        _f.Advance(_f.Options.ReservationTimeout);

        await Engine.SweepAsync();

        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 10) });
    }

    // ── 취소·만료 ──

    [Fact]
    public async Task 일부_팔린_매물도_남은_수량은_취소할_수_있다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);
        await Engine.ConfirmAsync(9, true);

        (await Engine.CancelAsync(1, 100)).ShouldBe(CancelResult.Ok);
        Engine.PriceLadder(Carp, 10).ShouldBeEmpty();
    }

    [Fact]
    public async Task 누가_일부라도_잡고_있으면_취소할_수_없다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 1, 30);

        (await Engine.CancelAsync(1, 100)).ShouldBe(CancelResult.InProgress);
    }

    [Fact]
    public async Task 잡힌_매물은_예약이_풀린_뒤에_만료된다()
    {
        await Seed(1, 30, 10);
        _f.Advance(TimeSpan.FromHours(48) - TimeSpan.FromSeconds(1));
        await Engine.ReserveQuantityAsync(9, 200, Carp, 1, 30);
        _f.Advance(TimeSpan.FromSeconds(1));

        await Engine.SweepAsync();
        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Reserved);

        _f.Advance(_f.Options.ReservationTimeout);
        await Engine.SweepAsync();
        (await Engine.GetStatesAsync(new long[] { 1 }))[1].ShouldBe(ListingState.Expired);
    }

    // ── 경매장 즉시구매와 섞일 때 ──

    [Fact]
    public async Task 수량이_일부_잡힌_매물은_통째로_살_수_없다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 1, 30);

        (await Engine.ReserveAsync(10, 1, 300, 300)).Result.ShouldBe(ReserveResult.InProgress);
    }

    [Fact]
    public async Task 일부_팔린_매물을_통째로_사면_남은_수량만큼이다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);
        await Engine.ConfirmAsync(9, true);

        // 남은 6개 × 30 = 180
        (await Engine.ReserveAsync(10, 1, 300, 180)).Result.ShouldBe(ReserveResult.Ok);
    }

    // ── 목록·시세 ──

    [Fact]
    public async Task 목록은_종류별_최저가와_판매_중_수량이다()
    {
        await Seed(1, 32, 10);
        await Seed(2, 30, 5);
        await Seed(3, 12, 7, tid: Pike);

        var items = await Engine.MarketItemsAsync(0, Array.Empty<int>());

        items.Select(m => (m.Tid, m.LowestUnitPrice, m.Available)).ShouldBe(new[] { (Carp, 30L, 15L), (Pike, 12L, 7L) });
    }

    [Fact]
    public async Task 목록은_TID로_거른다()
    {
        await Seed(1, 30, 10);
        await Seed(2, 12, 7, tid: Pike);

        (await Engine.MarketItemsAsync(0, new[] { Pike })).Select(m => m.Tid).ShouldBe(new[] { Pike });
    }

    [Fact]
    public async Task 매물이_없어도_요청한_종류는_시세와_함께_나온다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 10, 30);
        await Engine.ConfirmAsync(9, true);

        var item = (await Engine.MarketItemsAsync(0, new[] { Carp })).Single();

        (item.Available, item.RecentUnitPrice).ShouldBe((0L, 30L));
    }

    [Fact]
    public async Task 최근가는_마지막_체결_단가다()
    {
        await Seed(1, 30, 10);
        await Seed(2, 34, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 12, 34);
        await Engine.ConfirmAsync(9, true);

        // 30 × 10 → 34 × 2 순으로 체결 — 마지막은 34
        (await Engine.MarketItemsAsync(0, new[] { Carp })).Single().RecentUnitPrice.ShouldBe(34);
    }

    [Fact]
    public async Task 전일_평균가는_어제_체결의_수량_가중_평균이다()
    {
        // 시작 시각은 UTC 00:00 = 한국 09:00. 오늘(한국) 체결 → 다음 날로 넘기면 "어제"가 된다.
        await Seed(1, 30, 10);
        await Seed(2, 40, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 13, 40);
        await Engine.ConfirmAsync(9, true);
        _f.Advance(TimeSpan.FromDays(1));

        // (30 × 10 + 40 × 3) / 13 = 420 / 13 = 32.3 → 32
        (await Engine.MarketItemsAsync(0, new[] { Carp })).Single().YesterdayAvgPrice.ShouldBe(32);
    }

    [Fact]
    public async Task 오늘_체결은_전일_평균에_들지_않는다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 3, 30);
        await Engine.ConfirmAsync(9, true);

        (await Engine.MarketItemsAsync(0, new[] { Carp })).Single().YesterdayAvgPrice.ShouldBe(0);
    }

    [Fact]
    public async Task 가격대는_같은_단가를_합친다()
    {
        await Seed(1, 30, 10);
        await Seed(2, 30, 5);
        await Seed(3, 31, 2);

        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 15), new PriceLevel(31, 2) });
    }

    [Fact]
    public async Task 가격대는_요청한_칸_수까지만_준다()
    {
        await Seed(1, 30, 1);
        await Seed(2, 31, 1);
        await Seed(3, 32, 1);

        Engine.PriceLadder(Carp, 2).Select(l => l.UnitPrice).ShouldBe(new[] { 30L, 31L });
    }

    [Fact]
    public async Task 재시작해도_잡힌_수량은_빠진_채로_인덱스를_만든다()
    {
        await Seed(1, 30, 10);
        await Engine.ReserveQuantityAsync(9, 200, Carp, 4, 30);

        await _f.Restart();

        Engine.PriceLadder(Carp, 10).ShouldBe(new[] { new PriceLevel(30, 6) });
    }
}
