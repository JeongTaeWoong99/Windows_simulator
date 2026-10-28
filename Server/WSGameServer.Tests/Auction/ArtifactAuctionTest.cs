using GameData;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 거래소(T-152 · T-181) — 인벤토리 아티팩트 탭에서 빼서 등록 · 거래소 수량 구매 · 우편으로 받아 아티팩트 탭에 넣기 · 반환.
/// 도감에 장착한 1개는 인벤토리에 없어 올릴 수 없다. 틀리면 칸 수량과 원장이 어긋나 아티팩트가 복제·증발한다.
/// </summary>
public class ArtifactAuctionTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int  FishermanCharm = 1;   // 어부의 낡은 부적 — 일반 · 전 산업 작업속도 +1‰ · BasePrice 400
    private const long Seller         = 7;
    private const long Buyer          = 8;

    private readonly FakeAuctionClient _client = new();
    private readonly SqliteFixture     _db     = new();

    public ArtifactAuctionTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private (User User, TestUserBuilder B) NewUser(int charms, bool equipped = false)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };

        var user = b.Build(Seller);
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.LoadArtifacts(
            equipped ? new[] { new UserArtifactRow { artifact_tid = FishermanCharm } } : Array.Empty<UserArtifactRow>(),
            charms > 0 ? new[] { new UserArtifactStackRow { slot = 0, artifact_tid = FishermanCharm, count = charms } } : Array.Empty<UserArtifactStackRow>());

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    private static void Register(User user, int count, long unitPrice = 400)
        => user.TryRegisterAuction(EAuctionKind.Artifact, FishermanCharm, count, 0, unitPrice, Now);

    // ── 등록 ──

    [Fact]
    public void 올리면_인벤토리_칸에서_빠지고_도감_효과는_그대로다()
    {
        var (user, b) = NewUser(charms: 1, equipped: true);

        Register(user, 1);

        user.GetArtifactCount(FishermanCharm).ShouldBe(0);
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(1);
        var item = b.DB.PostedOf<RegisterAuctionRepository>().Single().Item;
        (item.Kind, item.Tid, item.Count, item.Rarity).ShouldBe((EAuctionKind.Artifact, FishermanCharm, 1, (int)GlobalRarity.Common));
        Last<S_ArtifactInventoryResponse>(b).Stacks.ShouldBeEmpty();
    }

    [Fact]
    public void 도감에_장착한_것은_올릴_수_없다()
    {
        var (user, b) = NewUser(charms: 0, equipped: true);

        Register(user, 1);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.IsArtifactEquipped(FishermanCharm).ShouldBeTrue();
    }

    [Fact]
    public void 가진_것보다_많이_올리면_NotEnoughItem이고_보관함이_그대로다()
    {
        var (user, b) = NewUser(charms: 1);

        Register(user, 2);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.GetArtifactCount(FishermanCharm).ShouldBe(1);
    }

    [Fact]
    public void 기준가_아래로는_올릴_수_없다()
    {
        var (user, b) = NewUser(charms: 1);

        Register(user, 1, unitPrice: 399);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionPriceOutOfBand);
        user.GetArtifactCount(FishermanCharm).ShouldBe(1);
    }

    // ── 거래소 ──

    [Fact]
    public void 거래소_구매_요청에_아티팩트_종류가_실린다()
    {
        var (user, _) = NewUser(charms: 0);
        _client.ReserveQuantity = _ => Task.FromResult(new Proto.ReserveReply { Result = Proto.ReserveResult.NotEnough });

        user.TryBuyMarket(FishermanCharm, 2, 500, Now, EAuctionKind.Artifact);

        _client.RequestsOf<Proto.ReserveQuantityRequest>().Single().Kind.ShouldBe((int)EAuctionKind.Artifact);
    }

    [Fact]
    public void 거래소에서_장비_종류로는_살_수_없다()
    {
        var (user, b) = NewUser(charms: 0);

        user.TryBuyMarket(1001, 1, 500, Now, EAuctionKind.Equip);

        Last<S_MarketBuyResponse>(b).Result.ShouldBe(EResultCode.AuctionInvalidRequest);
        _client.RequestsOf<Proto.ReserveQuantityRequest>().ShouldBeEmpty();
    }

    [Fact]
    public void 아티팩트_우편을_받으면_인벤토리_아티팩트_탭에_들어간다()
    {
        var (user, b) = NewUser(charms: 0);
        var attachment = new MailAttachment(0, new(), new(), new(), ArtifactItems: new List<(int, int)> { (FishermanCharm, 2) });
        user.OnMailsArrived(new List<UserMailRow> { MailDb.ToRow(40, AuctionMail.PurchasedTemplateTid, attachment, Now) });

        user.TryClaimMail(40, Now);

        Last<S_MailClaimResponse>(b).Result.ShouldBe(EResultCode.Ok);
        user.GetArtifactCount(FishermanCharm).ShouldBe(2);
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(0);   // 장착 전이라 효과 없음
    }

    [Fact]
    public void 아티팩트_탭이_가득_차면_우편을_받지_못한다()
    {
        var (user, b) = NewUser(charms: 0);
        var full = Enumerable.Range(0, User.StorageCapacity)
            .Select(slot => new UserArtifactStackRow { slot = slot, artifact_tid = 2, count = User.ArtifactMaxStack }).ToList();
        user.LoadArtifacts(Array.Empty<UserArtifactRow>(), full);
        var attachment = new MailAttachment(0, new(), new(), new(), ArtifactItems: new List<(int, int)> { (FishermanCharm, 1) });
        user.OnMailsArrived(new List<UserMailRow> { MailDb.ToRow(41, AuctionMail.PurchasedTemplateTid, attachment, Now) });

        user.TryClaimMail(41, Now);

        Last<S_MailClaimResponse>(b).Result.ShouldBe(EResultCode.StorageFull);
        user.GetArtifactCount(FishermanCharm).ShouldBe(0);
    }

    // ── 원장 ──

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private static AuctionItemSnapshot Snapshot(int count)
        => new() { Kind = EAuctionKind.Artifact, Tid = FishermanCharm, Rarity = 1, Count = count };

    private static List<ItemChangeInfo> StackChange(int left)
        => new() { new ItemChangeInfo { ItemId = FishermanCharm, Count = left, Slot = 0, Kind = left == 0 ? EItemChangeKind.Remove : EItemChangeKind.Update } };

    private Task<long> RegisterInDb(int owned = 3, int count = 2)
    {
        _db.Execute($"INSERT INTO t_user_artifact_stack (user_id, slot, artifact_tid, count) VALUES ({Seller}, 0, {FishermanCharm}, {owned})");
        return AuctionDb.RegisterAsync(Conn, Seller, Snapshot(count), 400, 8, new List<ItemChangeInfo>(), gold: 992, Now.AddHours(48), Now, StackChange(owned - count));
    }

    [Fact]
    public async Task 등록하면_아티팩트_칸_수량이_같은_트랜잭션에서_준다()
    {
        var tradeId = await RegisterInDb(owned: 3, count: 2);

        Scalar($"SELECT count FROM t_user_artifact_stack WHERE user_id = {Seller} AND slot = 0").ShouldBe(1);
        Scalar($"SELECT kind FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe((long)EAuctionKind.Artifact);
    }

    [Fact]
    public async Task 마지막_칸을_올리면_칸_행이_지워진다()
    {
        await RegisterInDb(owned: 2, count: 2);

        Scalar($"SELECT COUNT(*) FROM t_user_artifact_stack WHERE user_id = {Seller}").ShouldBe(0);
    }

    [Fact]
    public async Task 칸_변경이_없으면_등록_전체가_되돌아간다()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => AuctionDb.RegisterAsync(Conn, Seller, Snapshot(1), 400, 4, new List<ItemChangeInfo>(), 996, Now.AddHours(48), Now, new List<ItemChangeInfo>()));

        Scalar("SELECT COUNT(*) FROM t_auction_trade").ShouldBe(0);
        Scalar($"SELECT gold FROM t_user_currency WHERE user_id = {Seller}").ShouldBe(0);
    }

    [Fact]
    public async Task 거래소에서_팔리면_구매자_우편에_아티팩트가_실린다()
    {
        var tradeId = await RegisterInDb(owned: 3, count: 2);

        var result = await AuctionDb.SettleMarketAsync(Conn, Buyer, 900, FishermanCharm, new[] { new MarketAllocation(tradeId, 2, 400) }, 200, Now, EAuctionKind.Artifact);

        result.Settled.ShouldBeTrue();
        var attachment = MailAttachment.FromRow(result.BuyerMail!);
        attachment.Artifacts.ShouldBe(new[] { (FishermanCharm, 2) });
        attachment.Items.ShouldBeEmpty();
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE user_id = {Buyer} AND artifacts = '[[{FishermanCharm},2]]'").ShouldBe(1);
    }

    [Fact]
    public async Task 아티팩트_매물을_자원으로_정산하면_어긋나서_실패한다()
    {
        var tradeId = await RegisterInDb();

        var result = await AuctionDb.SettleMarketAsync(Conn, Buyer, 900, FishermanCharm, new[] { new MarketAllocation(tradeId, 2, 400) }, 200, Now);

        result.Settled.ShouldBeFalse();
        Scalar($"SELECT count FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe(2);
    }

    [Fact]
    public async Task 취소하면_판매자_우편으로_아티팩트가_돌아간다()
    {
        var tradeId = await RegisterInDb(owned: 3, count: 2);

        var result = await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Cancelled, Now);

        result!.SellerId.ShouldBe(Seller);
        MailAttachment.FromRow(result.Mail).Artifacts.ShouldBe(new[] { (FishermanCharm, 2) });
    }
}
