namespace AuctionServer.Tests;

/// <summary>
/// 시세 — 장비·캐릭터 경매의 종류별 요약(T-118)과 일별 시세(시세 탭 그래프). 하루 경계는 거래소와 같은 한국 자정이다.
/// 틀리면 그래프의 날짜가 하루 밀리거나, 체결이 없는 날이 빠져 막대가 어긋난다.
/// </summary>
public class PriceHistoryEngineTest : IAsyncLifetime
{
    private const int Carp  = 10001;
    private const int Sword = 5001;

    private readonly EngineFixture _f = new();

    private AuctionEngine Engine => _f.Engine;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task Deal(long id, long unitPrice, int count, long purchaseId)
    {
        await Engine.RegisterAsync(_f.Item(id, unitPrice: unitPrice, count: count));
        await Engine.ReserveQuantityAsync(purchaseId, buyerId: 200, Carp, count, unitPrice);
        await Engine.ConfirmAsync(purchaseId, true);
    }

    private async Task SellSword(long id, long unitPrice, long purchaseId)
    {
        await Engine.RegisterAsync(_f.Equip(id, unitPrice: unitPrice));
        await Engine.ReserveAsync(purchaseId, id, buyerId: 200, expectedTotal: unitPrice);
        await Engine.ConfirmAsync(purchaseId, true);
    }

    // ── 장비·캐릭터 요약 ──

    [Fact]
    public async Task 장비도_종류별_최저가와_최근가가_나온다()
    {
        await SellSword(1, unitPrice: 500, purchaseId: 9);
        await Engine.RegisterAsync(_f.Equip(2, unitPrice: 700));
        await Engine.RegisterAsync(_f.Equip(3, unitPrice: 650));

        var item = (await Engine.MarketItemsAsync(0, new[] { Sword }, ListingKind.Equip)).Single();

        (item.LowestUnitPrice, item.Available, item.RecentUnitPrice).ShouldBe((650L, 2L, 500L));
    }

    // ── 일별 시세 ──

    [Fact]
    public async Task 날마다_평균_수량_최저_최고가_나오고_체결이_없는_날도_빈_칸으로_온다()
    {
        // 시작 = UTC 00:00 = 한국 09:00. 첫날 30×10 + 40×5, 이틀 비우고, 넷째 날 20×2.
        await Deal(1, 30, 10, purchaseId: 11);
        await Deal(2, 40, 5, purchaseId: 12);
        _f.Advance(TimeSpan.FromDays(3));
        await Deal(3, 20, 2, purchaseId: 13);

        var days = await Engine.PriceHistoryAsync(ListingKind.Item, Carp, days: 4);

        // (30×10 + 40×5) / 15 = 500 / 15 = 33.3 → 33
        days.Select(d => (d.Quantity, d.AvgUnitPrice, d.MinUnitPrice, d.MaxUnitPrice)).ShouldBe(new[]
        {
            (15L, 33L, 30L, 40L),
            (0L, 0L, 0L, 0L),
            (0L, 0L, 0L, 0L),
            (2L, 20L, 20L, 20L),
        });
    }

    [Fact]
    public async Task 날짜는_한국_자정에_갈린다()
    {
        // 한국 23:30(UTC 14:30) 체결과 다음 날 00:30(UTC 15:30) 체결은 서로 다른 날이다.
        _f.Advance(TimeSpan.FromHours(14.5));
        await Deal(1, 30, 1, purchaseId: 11);
        _f.Advance(TimeSpan.FromHours(1));
        await Deal(2, 50, 1, purchaseId: 12);

        var days = await Engine.PriceHistoryAsync(ListingKind.Item, Carp, days: 2);

        days.Select(d => d.AvgUnitPrice).ShouldBe(new[] { 30L, 50L });
        // 마지막 칸이 오늘 — 시작은 한국 자정 = UTC 전날 15:00
        days[^1].DayStart.ShouldBe(new DateTime(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task 다른_종류의_체결은_섞이지_않는다()
    {
        await SellSword(1, unitPrice: 500, purchaseId: 9);

        var days = await Engine.PriceHistoryAsync(ListingKind.Item, Sword, days: 1);

        days.Single().Quantity.ShouldBe(0);
    }
}
