using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 메인 거래 원장 SQL을 실제 SQLite에 대고 검증한다. 등록·정산·반환은 각각 한 트랜잭션이어야 하고,
/// 거래 전이는 한 번만 일어나야 한다 — 틀리면 아이템이 증발·복제되거나 대금이 두 번 나간다.
/// </summary>
public class AuctionDbTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const long Seller = 7;
    private const long Buyer  = 8;

    private readonly SqliteFixture _db = new();

    public AuctionDbTest()
    {
        _db.CreatePlayerTables();
        _db.CreateMailTables();
        _db.CreateAuctionTables();
    }

    public void Dispose() => _db.Dispose();

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private string? Text(string sql) => _db.Query(sql) as string;

    private static AuctionItemSnapshot Wood(int count)
        => new() { Kind = EAuctionKind.Item, Tid = 40001, Category = 4, Rarity = 1, Count = count };

    private static AuctionItemSnapshot Sword(long equipId)
        => new() { Kind = EAuctionKind.Equip, Tid = 1001, Category = 1, Rarity = 3, Count = 1, EquipId = equipId, EnchantGrade = 4, Options = new() { 701, 702 } };

    private Task<long> RegisterWood(int count = 10, long unitPrice = 30, long fee = 3)
    {
        var changes = new List<ItemChangeInfo> { new() { ItemId = 40001, Count = 5 } };
        return AuctionDb.RegisterAsync(Conn, Seller, Wood(count), unitPrice, fee, changes, gold: 997, dia: 0, Now.AddHours(48), Now);
    }

    private Task<long> RegisterSword(long equipId = 55)
    {
        _db.Execute($"INSERT INTO t_user_equip (equip_id, user_id, equip_tid, enchant_grade, enchant_1, enchant_2) VALUES ({equipId}, {Seller}, 1001, 4, 701, 702)");
        return AuctionDb.RegisterAsync(Conn, Seller, Sword(equipId), 500, 5, new List<ItemChangeInfo>(), gold: 995, dia: 0, Now.AddHours(48), Now);
    }

    // ── 등록 ──

    [Fact]
    public async Task 등록하면_판매중_거래와_등록_메시지가_남는다()
    {
        var tradeId = await RegisterWood();

        Scalar($"SELECT state FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe(AuctionDb.Listed);
        Scalar($"SELECT COUNT(*) FROM t_auction_outbox WHERE trade_id = {tradeId} AND kind = 1 AND sent_at IS NULL").ShouldBe(1);
    }

    [Fact]
    public async Task 등록하면_인벤토리와_골드를_확정값으로_쓴다()
    {
        await RegisterWood();

        Scalar("SELECT count FROM t_user_inventory WHERE user_id = 7 AND item_id = 40001").ShouldBe(5);
        Scalar("SELECT gold FROM t_user_currency WHERE user_id = 7").ShouldBe(997);
    }

    [Fact]
    public async Task 등록_메시지에_매물_ID와_만료_시각이_실린다()
    {
        var tradeId = await RegisterWood();

        var row     = (await AuctionDb.LoadUnsentAsync(Conn, 10)).Single();
        var message = System.Text.Json.JsonSerializer.Deserialize<AuctionRegisterMessage>(row.payload)!;

        message.ListingId.ShouldBe(tradeId);
        message.ExpiresAtUnixMs.ShouldBe(new DateTimeOffset(Now.AddHours(48)).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task 장비를_등록하면_그_거래로_잠긴다()
    {
        var tradeId = await RegisterSword();

        Scalar("SELECT auction_trade_id FROM t_user_equip WHERE equip_id = 55").ShouldBe(tradeId);
    }

    [Fact]
    public async Task 이미_잠긴_장비면_아무것도_남지_않는다()
    {
        await RegisterSword();

        await Should.ThrowAsync<InvalidOperationException>(
            () => AuctionDb.RegisterAsync(Conn, Seller, Sword(55), 500, 5, new List<ItemChangeInfo>(), 990, 0, Now.AddHours(48), Now));

        Scalar("SELECT COUNT(*) FROM t_auction_trade").ShouldBe(1);
        Scalar("SELECT gold FROM t_user_currency WHERE user_id = 7").ShouldBe(995);
    }

    [Fact]
    public async Task 판매중_건수는_Listed만_센다()
    {
        await RegisterWood();
        var returned = await RegisterWood();
        await AuctionDb.ReturnAsync(Conn, returned, AuctionReturnReason.Cancelled, Now);

        (await AuctionDb.CountActiveAsync(Conn, Seller)).ShouldBe(1);
    }

    // ── 정산 ──

    [Fact]
    public async Task 정산하면_구매자_우편에_물건이_간다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);

        var result = await AuctionDb.SettleAsync(Conn, tradeId, Buyer, purchaseId: 900, totalPrice: 300, buyerGold: 700, buyerDia: 0, Now);

        result.Settled.ShouldBeTrue();
        Text("SELECT items FROM t_user_mail WHERE user_id = 8").ShouldBe("[[40001,10]]");
    }

    [Fact]
    public async Task 정산하면_판매자_우편에_수수료를_뺀_대금이_간다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, 700, 0, Now);

        // 300 − 300 × 5% = 285
        Scalar($"SELECT gold FROM t_user_mail WHERE user_id = 7 AND template_tid = {AuctionMail.SoldTemplateTid}").ShouldBe(285);
    }

    [Fact]
    public async Task 정산하면_구매자_골드를_확정값으로_쓴다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, buyerGold: 700, 0, Now);

        Scalar("SELECT gold FROM t_user_currency WHERE user_id = 8").ShouldBe(700);
    }

    [Fact]
    public async Task 정산하면_확정_성공_메시지가_남는다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, 700, 0, Now);

        Text($"SELECT payload FROM t_auction_outbox WHERE trade_id = {tradeId} AND kind = 2")
            .ShouldBe("{\"PurchaseId\":900,\"Success\":true}");
    }

    [Fact]
    public async Task 장비를_정산하면_소유가_구매자로_넘어가고_잠금은_남는다()
    {
        var tradeId = await RegisterSword(55);

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 500, 500, 0, Now);

        Scalar("SELECT user_id FROM t_user_equip WHERE equip_id = 55").ShouldBe(Buyer);
        Scalar("SELECT auction_trade_id FROM t_user_equip WHERE equip_id = 55").ShouldBe(tradeId);
    }

    [Fact]
    public async Task 장비_구매_우편에_인챈트가_그대로_실린다()
    {
        var tradeId = await RegisterSword(55);

        var result = await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 500, 500, 0, Now);

        MailAttachment.FromRow(result.BuyerMail!).Equips.ShouldBe(new[] { new MailEquip(55, 1001, 4, new List<int> { 701, 702 }) },
            ignoreOrder: false, comparer: new MailEquipComparer());
    }

    [Fact]
    public async Task 두_번_정산해도_한_번만_팔린다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);
        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, 700, 0, Now);

        var second = await AuctionDb.SettleAsync(Conn, tradeId, 9, 901, 300, 700, 0, Now);

        second.Settled.ShouldBeFalse();
        Scalar("SELECT COUNT(*) FROM t_user_mail WHERE template_tid = 3").ShouldBe(1);
    }

    [Fact]
    public async Task 정산에_실패하면_확정_실패_메시지만_남는다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);
        await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now);

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, 700, 0, Now);

        Text($"SELECT payload FROM t_auction_outbox WHERE trade_id = {tradeId} AND kind = 2")
            .ShouldBe("{\"PurchaseId\":900,\"Success\":false}");
    }

    [Fact]
    public async Task 정산에_실패하면_뺀_대금을_돌려준_잔액을_같이_쓴다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);
        await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now);

        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, buyerGold: 700, 0, Now);

        // 메모리는 1000 − 300 = 700을 들고 있다. 실패면 DB에는 700 + 300 = 1000이 남아야 한다.
        Scalar("SELECT gold FROM t_user_currency WHERE user_id = 8").ShouldBe(1000);
        Scalar("SELECT COUNT(*) FROM t_user_mail").ShouldBe(1);
    }

    [Fact]
    public async Task 총액이_다르면_정산하지_않는다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);

        (await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, totalPrice: 299, 700, 0, Now)).Settled.ShouldBeFalse();
    }

    [Fact]
    public async Task 판매자_자신에게는_정산하지_않는다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);

        (await AuctionDb.SettleAsync(Conn, tradeId, Seller, 900, 300, 700, 0, Now)).Settled.ShouldBeFalse();
    }

    // ── 반환 ──

    [Fact]
    public async Task 만료_반환은_물건과_등록비를_돌려준다()
    {
        var tradeId = await RegisterWood(count: 10, fee: 3);

        var result = await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now);

        var attachment = MailAttachment.FromRow(result!.Mail);
        (attachment.Gold, attachment.Items.Single()).ShouldBe((3L, (40001, 10)));
        result.Mail.template_tid.ShouldBe(AuctionMail.ExpiredTemplateTid);
    }

    [Fact]
    public async Task 취소_반환은_등록비를_돌려주지_않는다()
    {
        var tradeId = await RegisterWood(count: 10, fee: 3);

        var result = await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Cancelled, Now);

        MailAttachment.FromRow(result!.Mail).Gold.ShouldBe(0);
    }

    [Fact]
    public async Task 장비_반환은_판매자_소유로_잠긴_채_우편에_실린다()
    {
        var tradeId = await RegisterSword(55);

        var result = await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Cancelled, Now);

        MailAttachment.FromRow(result!.Mail).Equips.Single().EquipId.ShouldBe(55);
        Scalar("SELECT user_id FROM t_user_equip WHERE equip_id = 55").ShouldBe(Seller);
        Scalar("SELECT auction_trade_id FROM t_user_equip WHERE equip_id = 55").ShouldBe(tradeId);
    }

    [Fact]
    public async Task 두_번_반환해도_우편은_한_통이다()
    {
        var tradeId = await RegisterWood();
        await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Cancelled, Now);

        (await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now)).ShouldBeNull();
        Scalar("SELECT COUNT(*) FROM t_user_mail").ShouldBe(1);
    }

    [Fact]
    public async Task 팔린_거래는_반환하지_않는다()
    {
        var tradeId = await RegisterWood(count: 10, unitPrice: 30);
        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 300, 700, 0, Now);

        (await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Expired, Now)).ShouldBeNull();
    }

    // ── 받기 ──

    [Fact]
    public async Task 잠긴_장비를_받으면_잠금이_풀리고_칸이_정해진다()
    {
        var tradeId = await RegisterSword(55);
        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 500, 500, 0, Now);

        (await AuctionDb.UnlockEquipAsync(Conn, 55, Buyer, slotPosition: 3)).ShouldBeTrue();

        Scalar("SELECT auction_trade_id FROM t_user_equip WHERE equip_id = 55").ShouldBe(0);
        Scalar("SELECT slot_position FROM t_user_equip WHERE equip_id = 55").ShouldBe(3);
    }

    [Fact]
    public async Task 남의_잠긴_장비는_받을_수_없다()
    {
        var tradeId = await RegisterSword(55);
        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 500, 500, 0, Now);

        (await AuctionDb.UnlockEquipAsync(Conn, 55, Seller, 3)).ShouldBeFalse();
    }

    [Fact]
    public async Task 이미_받은_장비는_다시_풀지_않는다()
    {
        var tradeId = await RegisterSword(55);
        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 500, 500, 0, Now);
        await AuctionDb.UnlockEquipAsync(Conn, 55, Buyer, 3);

        (await AuctionDb.UnlockEquipAsync(Conn, 55, Buyer, 4)).ShouldBeFalse();
    }

    // ── 릴레이·대사 ──

    [Fact]
    public async Task 보낸_메시지는_미전송_목록에서_빠진다()
    {
        await RegisterWood();
        var row = (await AuctionDb.LoadUnsentAsync(Conn, 10)).Single();

        await AuctionDb.MarkSentAsync(Conn, row.outbox_id, Now);

        (await AuctionDb.LoadUnsentAsync(Conn, 10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task 대사_대상은_등록_메시지를_보낸_오래된_판매중_거래뿐이다()
    {
        var sent    = await RegisterWood();
        var unsent  = await RegisterWood();
        var settled = await RegisterWood(count: 10, unitPrice: 30);
        foreach (var row in await AuctionDb.LoadUnsentAsync(Conn, 10))
        {
            if (row.trade_id != unsent)
            {
                await AuctionDb.MarkSentAsync(Conn, row.outbox_id, Now);
            }
        }
        await AuctionDb.SettleAsync(Conn, settled, Buyer, 900, 300, 700, 0, Now);

        var stale = await AuctionDb.FindStaleListedAsync(Conn, createdBefore: Now, max: 10);

        stale.ShouldBe(new[] { sent });
    }

    [Fact]
    public async Task 대사_기준_시각보다_새_거래는_빠진다()
    {
        var tradeId = await RegisterWood();
        var row = (await AuctionDb.LoadUnsentAsync(Conn, 10)).Single();
        await AuctionDb.MarkSentAsync(Conn, row.outbox_id, Now);

        (await AuctionDb.FindStaleListedAsync(Conn, createdBefore: Now.AddSeconds(-1), max: 10)).ShouldBeEmpty();
    }

    private sealed class MailEquipComparer : IEqualityComparer<MailEquip>
    {
        public bool Equals(MailEquip? x, MailEquip? y)
            => x is not null && y is not null && x.EquipId == y.EquipId && x.EquipTid == y.EquipTid
               && x.EnchantGrade == y.EnchantGrade && x.Options.SequenceEqual(y.Options);

        public int GetHashCode(MailEquip obj) => obj.EquipId.GetHashCode();
    }
}
