namespace WSGameServer;

/// <summary>
/// 공용 상수 시트(Constants.xlsx) → 생성된 <c>Constants</c> → 서버 규칙까지 이어지는지.
/// 끊기면 서버 판정과 클라 표시가 서로 다른 숫자를 쓴다.
/// </summary>
public class ConstantsTest
{
    [Fact]
    public void 생성된_상수는_전부_데이터에서_읽힌다()
    {
        ConstantsCheck.EnsureAll().ShouldBeGreaterThan(0);
    }

    [Fact]
    public void 창고_한도는_시트_값을_따른다()
    {
        User.StorageCapacity.ShouldBe((int)GameData.Constants.StorageCapacity);
    }

    [Fact]
    public void 경매_등록_기간은_시간_단위_상수에서_온다()
    {
        AuctionRules.ListingDuration.ShouldBe(TimeSpan.FromHours(GameData.Constants.AuctionListingHours));
    }
}
