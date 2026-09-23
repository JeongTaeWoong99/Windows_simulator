using GameData;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>의 경매 경로 — 등록 검증·메모리에서 빼기·저장 요청, 구매 hold·예약·정산 결과, 취소·검색 중계, 우편 장비 수령.
/// 여기가 틀리면 등록 실패에도 아이템이 빠지거나, 예약을 기다리는 사이 같은 골드가 두 번 쓰인다.
/// </summary>
public class UserAuctionTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int  Carp      = 10001;   // 붕어 — 낚시 · Common · BasePrice 10
    private const int  SwordTid  = 1001;
    private const long Sword     = 55;
    private const long CharA     = 500;

    private readonly FakeAuctionClient _client = new();

    public UserAuctionTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) NewUser(bool withAuction = true, long uid = 7)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        if (withAuction)
        {
            b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };
        }

        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", GlobalRarity = GlobalRarity.Rare, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });
        b.Enchants.LoadAll();

        var user = b.Build(uid);
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.AddItem(Carp, 20);
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(
            new[] { new UserEquipRow { equip_id = Sword, equip_tid = SwordTid, slot_position = 0, enchant_grade = (int)GlobalRarity.Rare, enchant_1 = 101, enchant_2 = 102 } },
            Array.Empty<CharacterEquipRow>());

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    // ── 등록 ──

    [Fact]
    public void 경매장이_없으면_등록을_거절한다()
    {
        var (user, b) = NewUser(withAuction: false);

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 5, 0, 10, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionUnavailable);
        user.GetItemCount(Carp).ShouldBe(20);
    }

    [Fact]
    public void 판매중_건수를_읽기_전에는_등록을_거절한다()
    {
        var (user, b) = NewUser();
        var fresh = b.Build(uid: 9);

        fresh.TryRegisterAuction(EAuctionKind.Item, Carp, 5, 0, 10, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionUnavailable);
    }

    [Fact]
    public void 자원을_등록하면_창고에서_빠지고_등록비가_나간다()
    {
        var (user, _) = NewUser();

        // 단가 30 × 7개 = 210 → 등록비 1% = 2
        user.TryRegisterAuction(EAuctionKind.Item, Carp, 7, 0, 30, Now);

        user.GetItemCount(Carp).ShouldBe(13);
        user.Gold.ShouldBe(998);
    }

    [Fact]
    public void 등록은_물건과_가격을_담아_한_번에_저장을_요청한다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 7, 0, 30, Now);

        var saved = b.DB.PostedOf<RegisterAuctionRepository>().Single();
        (saved.Item.Kind, saved.Item.Tid, saved.Item.Count, saved.Item.Category, saved.Item.Rarity)
            .ShouldBe((EAuctionKind.Item, Carp, 7, (int)ItemType.Fishing, (int)GlobalRarity.Common));
    }

    [Fact]
    public void 등록이_저장되면_매물_ID를_알리고_릴레이를_깨운다()
    {
        var (user, b) = NewUser();
        var kicked = 0;
        b.Auction!.KickRelay = () => kicked++;

        user.OnAuctionRegistered(4242, new AuctionItemSnapshot { Kind = EAuctionKind.Item, Tid = Carp, Count = 7 }, 2, new List<ItemChangeInfo>());

        var response = Last<S_AuctionRegisterResponse>(b);
        (response.Result, response.ListingId, response.ListingFee).ShouldBe((EResultCode.Ok, 4242L, 2L));
        kicked.ShouldBe(1);
    }

    [Theory]
    [InlineData(9)]     // 하한 10 미만
    [InlineData(101)]   // 상한 100 초과
    public void 가격_밴드_밖이면_아무것도_바꾸지_않는다(long unitPrice)
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 5, 0, unitPrice, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionPriceOutOfBand);
        (user.GetItemCount(Carp), user.Gold).ShouldBe((20, 1000L));
        b.DB.PostedOf<RegisterAuctionRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 가진_것보다_많이는_올릴_수_없다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 21, 0, 10, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.NotEnoughItem);
    }

    [Fact]
    public void 없는_아이템은_거절한다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Item, 999999, 1, 0, 10, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionInvalidRequest);
    }

    [Fact]
    public void 등록비를_낼_골드가_없으면_거절한다()
    {
        var (user, b) = NewUser();
        user.TrySpendGold(1000);

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 5, 0, 10, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.GetItemCount(Carp).ShouldBe(20);
    }

    [Fact]
    public void 판매중_매물이_상한이면_거절한다()
    {
        var (user, b) = NewUser();
        user.OnAuctionStateLoaded(AuctionRules.MaxActiveListings);

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 1, 0, 10, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionListingLimit);
    }

    [Fact]
    public void 등록할_때마다_판매중_건수가_는다()
    {
        var (user, _) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Item, Carp, 1, 0, 10, Now);
        user.TryRegisterAuction(EAuctionKind.Item, Carp, 1, 0, 10, Now);

        user.ActiveListingCount.ShouldBe(2);
    }

    [Fact]
    public void 장비를_등록하면_메모리에서_빠진다()
    {
        var (user, _) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Equip, 0, 0, Sword, 70, Now);

        user.TryGetEquip(Sword, out _).ShouldBeFalse();
    }

    [Fact]
    public void 장비_등록은_인챈트까지_실어_저장한다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Equip, 0, 0, Sword, 70, Now);

        var item = b.DB.PostedOf<RegisterAuctionRepository>().Single().Item;
        (item.EquipId, item.Count, item.EnchantGrade, item.Category).ShouldBe((Sword, 1, (int)GlobalRarity.Rare, (int)EquipKind.Weapon));
        item.Options.ShouldBe(new[] { 101, 102 });
    }

    [Fact]
    public void 착용_중인_장비는_올릴_수_없다()
    {
        var (user, b) = NewUser();
        user.TryEquip(CharA, Sword, EquipSlot.Weapon, Now);

        user.TryRegisterAuction(EAuctionKind.Equip, 0, 0, Sword, 70, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionEquipped);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
    }

    [Fact]
    public void 없는_장비는_올릴_수_없다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Equip, 0, 0, 404, 70, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.EquipNotOwned);
    }

    // ── 구매 ──

    [Fact]
    public void 예약을_기다리는_동안_그_골드는_다른_데_못_쓴다()
    {
        var (user, _) = NewUser();
        var pending = new TaskCompletionSource<Proto.ReserveReply>();
        _client.Reserve = _ => pending.Task;

        user.TryBuyAuction(listingId: 1, expectedTotal: 800, Now);

        user.TrySpendGold(300).ShouldBeFalse();
        user.AvailableGold.ShouldBe(200);
    }

    [Fact]
    public void 쓸_수_있는_골드보다_비싸면_예약하지_않는다()
    {
        var (user, b) = NewUser();

        user.TryBuyAuction(1, 1001, Now);

        Last<S_AuctionBuyResponse>(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        _client.RequestsOf<Proto.ReserveRequest>().ShouldBeEmpty();
    }

    [Fact]
    public void 예약은_구매자와_본_총액을_싣는다()
    {
        var (user, _) = NewUser(uid: 7);
        _client.Reserve = _ => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.Sold });

        user.TryBuyAuction(1, 800, Now);

        var request = _client.RequestsOf<Proto.ReserveRequest>().Single();
        (request.ListingId, request.BuyerId, request.ExpectedTotal).ShouldBe((1L, 7L, 800L));
    }

    [Fact]
    public void 예약에_성공하면_골드를_빼고_정산을_저장한다()
    {
        var (user, b) = NewUser();
        _client.Reserve = r => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.Ok, SellerId = 8, TotalPrice = r.ExpectedTotal });

        user.TryBuyAuction(1, 800, Now);

        user.Gold.ShouldBe(200);
        user.HeldGold.ShouldBe(0);
        b.DB.PostedOf<SettleAuctionRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 예약_총액이_본_가격과_다르면_정산하지_않는다()
    {
        var (user, b) = NewUser();
        _client.Reserve = _ => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.Ok, TotalPrice = 5000 });

        user.TryBuyAuction(1, 800, Now);

        user.Gold.ShouldBe(1000);
        b.DB.PostedOf<SettleAuctionRepository>().ShouldBeEmpty();
        Last<S_AuctionBuyResponse>(b).Result.ShouldBe(EResultCode.AuctionPriceChanged);
    }

    [Theory]
    [InlineData(Proto.ReserveResult.InProgress, EResultCode.AuctionInProgress)]
    [InlineData(Proto.ReserveResult.Sold, EResultCode.AuctionSoldOut)]
    [InlineData(Proto.ReserveResult.Closed, EResultCode.AuctionClosed)]
    [InlineData(Proto.ReserveResult.PriceChanged, EResultCode.AuctionPriceChanged)]
    [InlineData(Proto.ReserveResult.OwnListing, EResultCode.AuctionOwnListing)]
    [InlineData(Proto.ReserveResult.NotFound, EResultCode.AuctionNotFound)]
    public void 예약_실패_사유를_구분해_알리고_hold를_푼다(Proto.ReserveResult reserve, EResultCode expected)
    {
        var (user, b) = NewUser();
        _client.Reserve = _ => Task.FromResult(new Proto.ReserveReply { Result = reserve });

        user.TryBuyAuction(1, 800, Now);

        Last<S_AuctionBuyResponse>(b).Result.ShouldBe(expected);
        (user.Gold, user.AvailableGold).ShouldBe((1000L, 1000L));
    }

    [Fact]
    public void 경매장에_닿지_않으면_hold를_풀고_알린다()
    {
        var (user, b) = NewUser();

        user.TryBuyAuction(1, 800, Now);

        Last<S_AuctionBuyResponse>(b).Result.ShouldBe(EResultCode.AuctionUnavailable);
        user.AvailableGold.ShouldBe(1000);
    }

    [Fact]
    public void 예약_뒤_유저가_나갔으면_정산하지_않는다()
    {
        var (user, b) = NewUser();
        var recording = new TestUserBuilder();   // Destroy는 예약만 되고 OnDestroy는 돌지 않는다
        var leaving   = recording.Build(uid: 9);
        leaving.Destroy();

        leaving.OnAuctionReserved(1, 1, 800, new Proto.ReserveReply { Result = Proto.ReserveResult.Ok, TotalPrice = 800 }, Now);

        recording.DB.PostedOf<SettleAuctionRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 정산에_실패하면_뺀_골드를_돌려준다()
    {
        var (user, b) = NewUser();
        _client.Reserve = r => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.Ok, TotalPrice = r.ExpectedTotal });
        user.TryBuyAuction(1, 800, Now);

        user.OnAuctionSettled(1, 800, new AuctionSettleResult(false, 8, null, null));

        user.Gold.ShouldBe(1000);
        Last<S_AuctionBuyResponse>(b).Result.ShouldBe(EResultCode.AuctionSoldOut);
    }

    [Fact]
    public void 정산에_성공하면_구매_우편이_도착한다()
    {
        var (user, b) = NewUser();
        var mail = new UserMailRow { mail_id = 31, template_tid = AuctionMail.PurchasedTemplateTid, items = "[[10001,5]]", received_at = MailDb.ToDb(Now) };

        user.OnAuctionSettled(1, 800, new AuctionSettleResult(true, 8, mail, mail with { mail_id = 32 }));

        user.TryGetMail(31, out _).ShouldBeTrue();
        Last<S_AuctionBuyResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 정산에_성공하면_접속_중인_판매자에게_대금_우편을_알린다()
    {
        var sellerBuilder = new TestUserBuilder();
        var seller = sellerBuilder.Build(uid: 8);
        seller.OnAuctionStateLoaded(3);

        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = uid => uid == 8 ? seller : null };
        var buyer = b.Build(uid: 7);
        var sellerMail = new UserMailRow { mail_id = 32, template_tid = AuctionMail.SoldTemplateTid, gold = 760, received_at = MailDb.ToDb(Now) };

        buyer.OnAuctionSettled(1, 800, new AuctionSettleResult(true, 8, sellerMail with { mail_id = 31 }, sellerMail));

        seller.TryGetMail(32, out var arrived).ShouldBeTrue();
        arrived.Attachment.Gold.ShouldBe(760);
        seller.ActiveListingCount.ShouldBe(2);
    }

    // ── 취소 ──

    [Fact]
    public void 취소가_받아들여지면_릴레이를_깨운다()
    {
        var (user, b) = NewUser();
        var kicked = 0;
        b.Auction!.KickRelay = () => kicked++;
        _client.Cancel = _ => Task.FromResult(new Proto.CancelReply { Result = Proto.CancelResult.Ok });

        user.TryCancelAuction(3);

        Last<S_AuctionCancelResponse>(b).Result.ShouldBe(EResultCode.Ok);
        kicked.ShouldBe(1);
    }

    [Fact]
    public void 취소는_내_Uid로_요청한다()
    {
        var (user, _) = NewUser(uid: 7);
        _client.Cancel = _ => Task.FromResult(new Proto.CancelReply { Result = Proto.CancelResult.NotOwner });

        user.TryCancelAuction(3);

        _client.RequestsOf<Proto.CancelRequest>().Single().SellerId.ShouldBe(7);
    }

    [Theory]
    [InlineData(Proto.CancelResult.NotOwner, EResultCode.AuctionNotOwner)]
    [InlineData(Proto.CancelResult.InProgress, EResultCode.AuctionInProgress)]
    [InlineData(Proto.CancelResult.Closed, EResultCode.AuctionClosed)]
    [InlineData(Proto.CancelResult.NotFound, EResultCode.AuctionNotFound)]
    public void 취소_거절_사유를_구분해_알린다(Proto.CancelResult cancel, EResultCode expected)
    {
        var (user, b) = NewUser();
        _client.Cancel = _ => Task.FromResult(new Proto.CancelReply { Result = cancel });

        user.TryCancelAuction(3);

        Last<S_AuctionCancelResponse>(b).Result.ShouldBe(expected);
    }

    // ── 검색 ──

    [Fact]
    public void 검색_조건을_그대로_경매장에_넘긴다()
    {
        var (user, _) = NewUser();
        _client.Search = _ => Task.FromResult(new Proto.SearchReply());

        user.TrySearchAuction(new C_AuctionSearchRequest
        {
            Kind = EAuctionKind.Equip, Category = 1, Tids = new() { 1001 }, MinEnchantGrade = 3, OptionTids = new() { 101 },
            CursorUnitPrice = 70, CursorListingId = 9, PageSize = 10,
        }, Now);

        var sent = _client.RequestsOf<Proto.SearchRequest>().Single();
        (sent.Kind, sent.Category, sent.MinEnchantGrade, sent.CursorUnitPrice, sent.CursorListingId, sent.PageSize)
            .ShouldBe((2, 1, 3, 70L, 9L, 10));
        (sent.Tids.Single(), sent.OptionTids.Single()).ShouldBe((1001, 101));
    }

    [Fact]
    public void 검색_결과를_클라_형식으로_옮긴다()
    {
        var (user, b) = NewUser();
        var view = new Proto.ListingView { ListingId = 3, Kind = 2, Tid = 1001, Count = 1, EnchantGrade = 3, UnitPrice = 70, TotalPrice = 70, State = Proto.ListingState.Listed };
        view.Options.AddRange(new[] { 101, 102 });
        var reply = new Proto.SearchReply { HasMore = true };
        reply.Listings.Add(view);
        _client.Search = _ => Task.FromResult(reply);

        user.TrySearchAuction(new C_AuctionSearchRequest(), Now);

        var response = Last<S_AuctionSearchResponse>(b);
        var listing  = response.Listings!.Single();
        (response.Result, response.HasMore, listing.ListingId, listing.Kind, listing.TotalPrice, listing.State)
            .ShouldBe((EResultCode.Ok, true, 3L, EAuctionKind.Equip, 70L, EAuctionListingState.Listed));
        listing.EnchantOptions.ShouldBe(new[] { 101, 102 });
    }

    [Fact]
    public void 검색은_순간_5회를_넘으면_거절한다()
    {
        var (user, b) = NewUser();
        _client.Search = _ => Task.FromResult(new Proto.SearchReply());

        for (var i = 0; i < 6; i++)
        {
            user.TrySearchAuction(new C_AuctionSearchRequest(), Now);
        }

        b.Channel.SentOf<S_AuctionSearchResponse>().Select(r => r.Result)
            .ShouldBe(Enumerable.Repeat(EResultCode.Ok, 5).Append(EResultCode.AuctionTooManyRequests));
    }

    [Fact]
    public void 내_매물에_구매_중_상태가_실린다()
    {
        var (user, b) = NewUser();
        var reply = new Proto.SellerListingsReply();
        reply.Listings.Add(new Proto.ListingView { ListingId = 3, State = Proto.ListingState.Reserved });
        _client.SellerListings = _ => Task.FromResult(reply);

        user.TryGetMyAuctionListings();

        Last<S_AuctionMyListingsResponse>(b).Listings!.Single().State.ShouldBe(EAuctionListingState.Reserved);
    }

    // ── 우편 장비 ──

    [Fact]
    public void 잠긴_장비_우편을_받으면_잠금_해제를_요청한다()
    {
        var (user, b) = NewUser();
        var attachment = new MailAttachment(0, 0, new(), new(), new(), new List<MailEquip> { new(77, SwordTid, (int)GlobalRarity.Rare, new List<int> { 101 }) });
        user.OnMailsArrived(new List<UserMailRow> { MailDb.ToRow(40, AuctionMail.PurchasedTemplateTid, attachment, Now) });

        user.TryClaimMail(40, Now);

        b.DB.PostedOf<UnlockMailEquipRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 잠금이_풀리면_인챈트까지_창고에_들어온다()
    {
        var (user, b) = NewUser();

        user.OnMailEquipUnlocked(new MailEquip(77, SwordTid, (int)GlobalRarity.Rare, new List<int> { 101, 102 }), slotPosition: 4, unlocked: true);

        user.TryGetEquip(77, out var equip).ShouldBeTrue();
        (equip.SlotPosition, equip.EnchantGrade).ShouldBe((4, GlobalRarity.Rare));
        equip.EnchantOptionTids.ShouldBe(new[] { 101, 102 });
    }

    [Fact]
    public void 잠금_해제가_안_됐으면_창고에_넣지_않는다()
    {
        var (user, _) = NewUser();

        user.OnMailEquipUnlocked(new MailEquip(77, SwordTid, 0, new List<int>()), 4, unlocked: false);

        user.TryGetEquip(77, out _).ShouldBeFalse();
    }
}
