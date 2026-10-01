using GameData;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 캐릭터 경매 — 등록 검증과 메모리에서 빼기, 원장의 잠금·소유 이전·반환, 우편 수령, 검색 결과의 개체 상세.
/// 장비 경매와 같은 길을 캐릭터가 탄다. 잠금이 빠지면 로그인이 잠긴 캐릭터를 올려 두 사람이 같은 개체를 갖는다.
/// </summary>
public class CharacterAuctionTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const long CharA  = 500;   // 램볼(1001) — Common, BasePrice 50
    private const long CharB  = 501;
    private const long Seller = 7;
    private const long Buyer  = 8;

    private readonly FakeAuctionClient _client = new();
    private readonly SqliteFixture     _db     = new();

    public CharacterAuctionTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreatePlayerTables();
        _db.CreateMailTables();
        _db.CreateAuctionTables();
    }

    public void Dispose() => _db.Dispose();

    private (User User, TestUserBuilder B) NewUser(params CharacterRow[] characters)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };

        var user = b.Build(Seller);
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.LoadCharacters(characters.Length > 0 ? characters : new[]
        {
            new CharacterRow { character_id = CharA, character_tid = 1001, level = 7, exp = 30, fishing_bonus = 1 },
            new CharacterRow { character_id = CharB, character_tid = 1001, level = 1, exp = 0 },
        });

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    private static void Register(User user, long characterId, long unitPrice = 50)
        => user.TryRegisterAuction(EAuctionKind.Character, 0, 0, 0, unitPrice, Now, characterId);

    // ── 등록 ──

    [Fact]
    public void 등록하면_캐릭터가_메모리에서_빠지고_레벨까지_스냅샷에_실린다()
    {
        var (user, b) = NewUser();

        Register(user, CharA);

        user.TryGetCharacter(CharA, out _).ShouldBeFalse();
        var item = b.DB.PostedOf<RegisterAuctionRepository>().Single().Item;
        (item.Kind, item.Tid, item.Count, item.Rarity).ShouldBe((EAuctionKind.Character, 1001, 1, (int)GlobalRarity.Common));
        item.Character.ShouldBe(new MailCharacter(CharA, 1001, 7, 30, new AptitudeBonus(Fishing: 1)));
    }

    [Fact]
    public void 기준가_아래로는_올릴_수_없다()
    {
        // 램볼 BasePrice 50 — 하한이 곧 즉시 판매가다.
        var (user, b) = NewUser();

        Register(user, CharA, unitPrice: 49);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionPriceOutOfBand);
        user.TryGetCharacter(CharA, out _).ShouldBeTrue();
    }

    [Fact]
    public void 슬롯에_배치된_캐릭터는_올릴_수_없다()
    {
        var (user, b) = NewUser();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, Now) });

        Register(user, CharA);

        (Last<S_AuctionRegisterResponse>(b).Result, Last<S_AuctionRegisterResponse>(b).CharacterId).ShouldBe((EResultCode.AuctionCharacterBusy, CharA));
    }

    [Fact]
    public void 마지막_캐릭터는_올릴_수_없다()
    {
        var (user, b) = NewUser(new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 });

        Register(user, CharA);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.AuctionLastCharacter);
    }

    [Fact]
    public void 미보유_캐릭터는_거부된다()
    {
        var (user, b) = NewUser();

        Register(user, 999);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.CharacterNotOwned);
    }

    // ── 원장 ──

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private static AuctionItemSnapshot Snapshot(long characterId)
        => new() { Kind = EAuctionKind.Character, Tid = 1001, Rarity = 1, Count = 1, Character = new MailCharacter(characterId, 1001, 7, 30, new AptitudeBonus(Fishing: 1)) };

    private Task<long> RegisterInDb(long characterId = CharA)
    {
        _db.Execute($"INSERT INTO t_character (character_id, user_id, character_tid, level, exp) VALUES ({characterId}, {Seller}, 1001, 7, 30)");
        return AuctionDb.RegisterAsync(Conn, Seller, Snapshot(characterId), 50, 1, new List<ItemChangeInfo>(), gold: 999, Now.AddHours(48), Now);
    }

    [Fact]
    public async Task 등록하면_캐릭터_행이_그_거래로_잠긴다()
    {
        var tradeId = await RegisterInDb();

        Scalar($"SELECT auction_trade_id FROM t_character WHERE character_id = {CharA}").ShouldBe(tradeId);
        Scalar($"SELECT character_id FROM t_auction_trade WHERE trade_id = {tradeId}").ShouldBe(CharA);
    }

    [Fact]
    public async Task 이미_잠긴_캐릭터를_다시_올리면_등록_전체가_되돌아간다()
    {
        await RegisterInDb();

        await Should.ThrowAsync<InvalidOperationException>(
            () => AuctionDb.RegisterAsync(Conn, Seller, Snapshot(CharA), 50, 1, new List<ItemChangeInfo>(), 998, Now.AddHours(48), Now));

        Scalar("SELECT COUNT(*) FROM t_auction_trade").ShouldBe(1);
    }

    [Fact]
    public async Task 정산하면_캐릭터가_잠긴_채_구매자_소유가_되고_우편에_실린다()
    {
        var tradeId = await RegisterInDb();

        var result = await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 50, 950, Now);

        result.Settled.ShouldBeTrue();
        Scalar($"SELECT user_id FROM t_character WHERE character_id = {CharA}").ShouldBe(Buyer);
        Scalar($"SELECT auction_trade_id FROM t_character WHERE character_id = {CharA}").ShouldBe(tradeId);
        MailAttachment.FromRow(result.BuyerMail!).Characters.Single().ShouldBe(new MailCharacter(CharA, 1001, 7, 30, new AptitudeBonus(Fishing: 1)));
    }

    [Fact]
    public async Task 취소하면_판매자_우편으로_캐릭터가_돌아간다()
    {
        var tradeId = await RegisterInDb();

        var result = await AuctionDb.ReturnAsync(Conn, tradeId, AuctionReturnReason.Cancelled, Now);

        result!.SellerId.ShouldBe(Seller);
        MailAttachment.FromRow(result.Mail).Characters.Single().CharacterId.ShouldBe(CharA);
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE user_id = {Seller} AND character_ids LIKE '%{CharA}%'").ShouldBe(1);
    }

    [Fact]
    public async Task 잠금은_소유자만_풀_수_있고_한_번만_풀린다()
    {
        var tradeId = await RegisterInDb();
        await AuctionDb.SettleAsync(Conn, tradeId, Buyer, 900, 50, 950, Now);

        (await AuctionDb.UnlockCharacterAsync(Conn, CharA, Seller, 0)).ShouldBeFalse();
        (await AuctionDb.UnlockCharacterAsync(Conn, CharA, Buyer, 0)).ShouldBeTrue();
        (await AuctionDb.UnlockCharacterAsync(Conn, CharA, Buyer, 0)).ShouldBeFalse();
    }

    // ── 우편 수령 ──

    [Fact]
    public void 잠긴_캐릭터_우편을_받으면_잠금_해제를_요청한다()
    {
        var (user, b) = NewUser();
        var attachment = new MailAttachment(0, new(), new(), new(), null, new List<MailCharacter> { new(77, 1001, 7, 30, default) });
        user.OnMailsArrived(new List<UserMailRow> { MailDb.ToRow(40, AuctionMail.PurchasedTemplateTid, attachment, Now) });

        user.TryClaimMail(40, Now);

        b.DB.PostedOf<UnlockMailCharacterRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 잠금이_풀리면_레벨과_적성까지_그대로_들어온다()
    {
        var (user, b) = NewUser();

        user.OnMailCharacterUnlocked(new MailCharacter(77, 1001, 7, 30, new AptitudeBonus(Fishing: 1)), slot: 0, unlocked: true);

        user.TryGetCharacter(77, out var character).ShouldBeTrue();
        (character.Level, character.Exp, character.Bonus.Fishing).ShouldBe((7, 30, 1));
        Last<S_CharacterSyncResponse>(b).Character!.CharacterId.ShouldBe(77);
    }

    [Fact]
    public void 잠금_해제가_안_됐으면_넣지_않는다()
    {
        var (user, _) = NewUser();

        user.OnMailCharacterUnlocked(new MailCharacter(77, 1001, 7, 30, default), slot: 0, unlocked: false);

        user.TryGetCharacter(77, out _).ShouldBeFalse();
    }

    // ── 검색 ──

    [Fact]
    public void 캐릭터_매물은_상세가_캐릭터_정보로_실린다()
    {
        var (user, b) = NewUser();
        var view = new Proto.ListingView
        {
            ListingId = 3, Kind = (int)EAuctionKind.Character, Tid = 1001, Count = 1, UnitPrice = 60, TotalPrice = 60,
            State = Proto.ListingState.Listed, Detail = System.Text.Json.JsonSerializer.Serialize(new MailCharacter(77, 1001, 7, 30, default)),
        };
        var reply = new Proto.SearchReply();
        reply.Listings.Add(view);
        _client.Search = _ => Task.FromResult(reply);

        user.TrySearchAuction(new C_AuctionSearchRequest { Kind = EAuctionKind.Character }, Now);

        var character = Last<S_AuctionSearchResponse>(b).Listings!.Single().Character!;
        (character.CharacterTid, character.Level, character.Exp).ShouldBe((1001, 7, 30));
        character.Aptitudes.Count.ShouldBe(5);
    }

    [Fact]
    public void 깨진_상세는_캐릭터_없이_싣는다()
    {
        var (user, b) = NewUser();
        var reply = new Proto.SearchReply();
        reply.Listings.Add(new Proto.ListingView { ListingId = 3, Kind = (int)EAuctionKind.Character, Tid = 1001, Count = 1, Detail = "{깨짐" });
        _client.Search = _ => Task.FromResult(reply);

        user.TrySearchAuction(new C_AuctionSearchRequest(), Now);

        var listing = Last<S_AuctionSearchResponse>(b).Listings!.Single();
        (listing.ListingId, listing.Character).ShouldBe((3L, (CharacterInfo?)null));
    }
}
