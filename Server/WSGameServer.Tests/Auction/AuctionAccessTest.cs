using MikaProtocol;

namespace WSGameServer;

public sealed class AuctionAccessTest
{
    [Theory]
    [InlineData(29, EResultCode.AuctionAccountLevelLocked)]
    [InlineData(30, EResultCode.Ok)]
    public void 계정_레벨에_따라_거래소_접근을_판정한다(int level, EResultCode expected)
    {
        var user = new TestUserBuilder().Build();
        user.LoadAccount(new AccountRow { level = level });

        ClientPacketHandler.CheckAuctionAccess(user).ShouldBe(expected);
    }

    [Fact]
    public void 로그인하지_않으면_로그인_필요를_반환한다()
    {
        ClientPacketHandler.CheckAuctionAccess(null).ShouldBe(EResultCode.NotLoggedIn);
    }
}
