namespace WSGameServer;

/// <summary>
/// 가격 밴드·수수료 계산. 하한이 즉시 판매가보다 낮아지면 사서 바로 되파는 차익이 생기고,
/// 수수료가 0이 되면 자전거래가 공짜가 된다.
/// </summary>
public class AuctionRulesTest
{
    public AuctionRulesTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public void 하한은_기준가다()
    {
        AuctionRules.MinUnitPrice(37).ShouldBe(37);
    }

    [Fact]
    public void 기준가가_0이어도_하한은_1이다()
    {
        AuctionRules.MinUnitPrice(0).ShouldBe(1);
    }

    [Fact]
    public void 상한은_기준가와_무관한_1조다()
    {
        AuctionRules.MaxUnitPrice.ShouldBe(1_000_000_000_000);
    }

    [Fact]
    public void 한_번에_올리는_수량_상한은_9999개다()
    {
        AuctionRules.MaxListingCount.ShouldBe(9999);
    }

    [Fact]
    public void 최고_단가로_최대_수량을_올려도_수수료_계산이_넘치지_않는다()
    {
        // 1조 × 9,999개 = 9,999조 → 판매 수수료 5% = 499조 9,500억
        // 곱한 뒤 나누므로 중간값은 9,999조 × 50 ≈ 50경 — long 한계(약 922경) 안이어야 한다
        AuctionRules.SaleFee(1_000_000_000_000 * 9999).ShouldBe(499_950_000_000_000);
    }

    [Theory]
    [InlineData(36, false)]
    [InlineData(37, true)]
    [InlineData(371, true)]
    [InlineData(1_000_000_000_000, true)]
    [InlineData(1_000_000_000_001, false)]
    public void 밴드_경계를_포함한다(long unitPrice, bool inBand)
    {
        AuctionRules.IsInBand(37, unitPrice).ShouldBe(inBand);
    }

    [Fact]
    public void 등록비는_총액의_1퍼센트를_내림한다()
    {
        // 1,999 × 10 / 1000 = 19.99 → 19
        AuctionRules.ListingFee(1999).ShouldBe(19);
    }

    [Fact]
    public void 등록비는_최소_1이다()
    {
        AuctionRules.ListingFee(50).ShouldBe(1);
    }

    [Fact]
    public void 판매_수수료는_총액의_5퍼센트를_내림한다()
    {
        // 1,999 × 50 / 1000 = 99.95 → 99
        AuctionRules.SaleFee(1999).ShouldBe(99);
    }
}
