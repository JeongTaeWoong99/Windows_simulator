using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 자원 잠금(T-159 · 이슈 #72). 자원은 개체가 없어 <b>종류(TID) 단위</b>로 잠그고, 잠긴 종류는 즉시 판매·경매 등록에서 빠진다.
/// 소비(상자 개봉·큐브)는 막지 않는다. 틀리면 아끼는 자원을 일괄 판매로 날리거나, 잠근 상자를 열지 못하게 된다.
/// </summary>
public class UserItemLockTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int CarpTid   = 10001;   // 붕어 — BasePrice 10
    private const int LegendTid = 10006;   // 전설의 잉어 — BasePrice 1000

    private readonly FakeAuctionClient _client = new();

    public UserItemLockTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) NewUser(params int[] lockedTids)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };

        var user = b.Build();
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.GainItem(CarpTid, 5);
        user.GainItem(LegendTid, 2);
        user.LoadItemLocks(lockedTids.Select(tid => new UserItemLockRow { item_id = tid }));

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    private static List<ItemInfo> Request(params (int Tid, int Count)[] items)
        => items.Select(i => new ItemInfo { ItemId = i.Tid, Count = i.Count }).ToList();

    // ── 잠금 · 해제 ──

    [Fact]
    public void 자원을_잠그면_응답하고_저장한다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Resource, CarpTid, locked: true);

        var res = Last<S_EntityLockResponse>(b);
        (res.Result, res.Target, res.Id, res.Locked).ShouldBe((EResultCode.Ok, ELockTarget.Resource, (long)CarpTid, true));
        var saved = b.DB.PostedOf<EntityLockRepository>().Single();
        (saved.Target, saved.Id, saved.Locked).ShouldBe((ELockTarget.Resource, (long)CarpTid, true));
    }

    [Fact]
    public void 잠긴_종류는_인벤토리_목록에_실린다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Resource, CarpTid, locked: true);
        user.SendInventory();

        Last<S_InventoryResponse>(b).LockedItemTids.ShouldBe(new[] { CarpTid });
    }

    [Fact]
    public void 로그인_때_저장된_잠금을_읽는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.OnLoginDataLoaded(new PlayerLoginData(
            new List<InventoryRow>(),
            null,
            new List<CharacterRow> { new() { character_id = 500, character_tid = 1001, level = 1 } },
            new List<WorkStationSlotRow>(),
            new List<UserUnlockRow>(),
            new List<UserEquipRow>(),
            new List<CharacterEquipRow>(),
            ItemLockRows: new List<UserItemLockRow> { new() { item_id = LegendTid } }), Now);

        Last<S_InventoryResponse>(b).LockedItemTids.ShouldBe(new[] { LegendTid });
    }

    [Fact]
    public void 해제하면_다시_팔_수_있다()
    {
        var (user, b) = NewUser(CarpTid);

        user.TrySetLock(ELockTarget.Resource, CarpTid, locked: false);
        ShopService.Instance.Sell(user, Request((CarpTid, 5)));

        Last<S_ItemSellResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 아이템_테이블에_없는_TID는_거절하고_저장하지_않는다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Resource, 999999, locked: true);

        Last<S_EntityLockResponse>(b).Result.ShouldBe(EResultCode.InvalidLockTarget);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 보유가_0개여도_잠근다()
    {
        // 다 써도 잠금은 남아야 한다 — 다시 얻었을 때 그대로 잠겨 있게
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Resource, 10002, locked: true);

        Last<S_EntityLockResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }

    // ── 없애는 동작은 막는다 ──

    [Fact]
    public void 잠긴_종류가_섞인_판매는_아무것도_팔지_않는다()
    {
        var (user, b) = NewUser(LegendTid);

        ShopService.Instance.Sell(user, Request((CarpTid, 5), (LegendTid, 1)));

        Last<S_ItemSellResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        (user.GetItemCount(CarpTid), user.GetItemCount(LegendTid), user.Gold).ShouldBe((5, 2, 1000L));
    }

    [Fact]
    public void 잠긴_종류는_경매에_올릴_수_없다()
    {
        var (user, b) = NewUser(LegendTid);

        user.TryRegisterAuction(EAuctionKind.Item, LegendTid, 1, 0, 1000, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.GetItemCount(LegendTid).ShouldBe(2);
    }

    [Fact]
    public void 다른_종류의_잠금은_판매를_막지_않는다()
    {
        var (user, b) = NewUser(LegendTid);

        ShopService.Instance.Sell(user, Request((CarpTid, 5)));

        Last<S_ItemSellResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }

    // ── 쓰는 동작은 그대로 ──

    [Fact]
    public void 잠긴_종류도_소비는_된다()
    {
        var (user, _) = NewUser(CarpTid);

        user.TryConsumeItems(new Dictionary<int, int> { [CarpTid] = 3 }, out _).ShouldBeTrue();

        user.GetItemCount(CarpTid).ShouldBe(2);
    }
}
