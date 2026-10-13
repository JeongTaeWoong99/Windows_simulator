namespace AuctionServer.Tests;

/// <summary>
/// 거래소의 아티팩트(T-152) — 자원과 같은 수량 구매 길을 <b>종류로 갈라</b> 탄다.
/// 틀리면 같은 번호(TID)의 자원과 아티팩트가 서로의 매물로 팔린다.
/// </summary>
public class ArtifactMarketEngineTest : IAsyncLifetime
{
    private const int SameTid = 7;   // 자원·아티팩트에 같은 번호를 일부러 쓴다 — TID는 종류 안에서만 유일하다

    private readonly EngineFixture _f = new();

    private AuctionEngine Engine => _f.Engine;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Listing Artifact(long id, long unitPrice, int count)
        => new(id, 100, ListingKind.Artifact, SameTid, Category: 0, Rarity: 1, count, EnchantGrade: 0, Array.Empty<int>(), unitPrice, _f.Now.AddHours(48));

    [Fact]
    public async Task 아티팩트_매물을_등록할_수_있다()
    {
        (await Engine.RegisterAsync(Artifact(1, 400, 3))).ShouldBe(RegisterResult.Ok);
    }

    [Fact]
    public async Task 아티팩트로_사면_같은_번호의_자원_매물은_잡지_않는다()
    {
        await Engine.RegisterAsync(_f.Item(1, unitPrice: 10, count: 5, tid: SameTid));
        await Engine.RegisterAsync(Artifact(2, 400, 3));

        var outcome = await Engine.ReserveQuantityAsync(9, buyerId: 200, SameTid, quantity: 2, maxUnitPrice: 500, ListingKind.Artifact);

        outcome.Lines.Select(a => (a.ListingId, a.Quantity)).ShouldBe(new[] { (2L, 2) });
    }

    [Fact]
    public async Task 자원으로_사면_아티팩트_매물을_잡지_않는다()
    {
        await Engine.RegisterAsync(Artifact(2, 400, 3));

        (await Engine.ReserveQuantityAsync(9, 200, SameTid, 1, 500)).Result.ShouldBe(ReserveResult.NotEnough);
    }

    [Fact]
    public async Task 목록과_가격대도_종류별로_따로_본다()
    {
        await Engine.RegisterAsync(_f.Item(1, unitPrice: 10, count: 5, tid: SameTid));
        await Engine.RegisterAsync(Artifact(2, 400, 3));

        (await Engine.MarketItemsAsync(0, Array.Empty<int>(), ListingKind.Artifact)).Single().LowestUnitPrice.ShouldBe(400);
        (await Engine.MarketItemsAsync(0, Array.Empty<int>())).Single().LowestUnitPrice.ShouldBe(10);
        Engine.PriceLadder(SameTid, 10, ListingKind.Artifact).ShouldBe(new[] { new PriceLevel(400, 3) });
    }
}
