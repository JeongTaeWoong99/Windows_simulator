using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 골드 상점 폐기(2026-11-22 · T-209) — 캐릭터는 테마 상자에서 나온다. <c>ShopTable</c>은 데이터만 남고 <b>구매는 전부 거절된다.</b>
/// 이미 가진 뽑기권은 지금처럼 열면 같은 풀(32~35)에서 캐릭터가 나온다.
/// 깨지면: 상점 구매 길이 다시 열렸거나, 보유 뽑기권이 쓸모없는 아이템이 된 것이다.
/// </summary>
public class ShopBuyTest
{
    /// <summary>상점 1번 = 캐릭터 뽑기권 A · 1,000골드(데이터만 남음).</summary>
    private const int TicketAShopTid = 1;

    private const int TicketATid = 100071;

    public ShopBuyTest() => GameTableFixture.EnsureLoaded();

    /// <summary>골드를 쥐여 준 유저. 지급으로 쌓인 패킷·DB 작업은 지운다 — 구매만 세기 위해.</summary>
    private static (User User, TestUserBuilder B) UserWithGold(long gold)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        if (gold > 0)
        {
            user.GainGold(gold);
        }

        b.DB.Posted.Clear();
        b.Channel.Sent.Clear();
        return (user, b);
    }

    [Fact]
    public void 골드가_넉넉해도_구매는_거절되고_아무것도_바뀌지_않는다()
    {
        var (user, b) = UserWithGold(1_000_000);

        ShopService.Instance.Buy(user, TicketAShopTid, 3);

        var res = b.Channel.SentOf<S_ShopBuyResponse>().Single();
        res.Result.ShouldBe(EResultCode.InvalidShopRequest);
        res.ShopTID.ShouldBe(TicketAShopTid);
        user.Gold.ShouldBe(1_000_000);
        user.GetItemCount(TicketATid).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 상점_품목_전부가_거절된다()
    {
        var (user, b) = UserWithGold(1_000_000);

        foreach (var row in GameTable.ShopTable.All)
        {
            ShopService.Instance.Buy(user, row.ShopTID, 1);
        }

        b.Channel.SentOf<S_ShopBuyResponse>().Count().ShouldBe(4);
        b.Channel.SentOf<S_ShopBuyResponse>().ShouldAllBe(r => r.Result == EResultCode.InvalidShopRequest);
        user.Gold.ShouldBe(1_000_000);
    }

    [Fact]
    public void 이미_가진_뽑기권은_열면_희귀_이하_캐릭터_한_명이_나온다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainItem(TicketATid, 2);
        b.Channel.Sent.Clear();

        var catalog = new GachaPoolCatalog();
        catalog.LoadAll();
        new GachaService(catalog).OpenBox(user, TicketATid, 1, TestUserBuilder.Base);

        var res = b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem();
        res.Result.ShouldBe(EResultCode.Ok);
        var reward = res.Rewards!.ShouldHaveSingleItem();
        reward.RewardType.ShouldBe(EGachaRewardType.Character);
        ((int)reward.Rarity).ShouldBeLessThanOrEqualTo((int)EGlobalRarity.Rare);
        user.GetItemCount(TicketATid).ShouldBe(1);
    }
}
