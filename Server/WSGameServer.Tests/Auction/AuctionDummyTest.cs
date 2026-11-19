using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 더미 매물(치트 T-168 · T-204) — 만드는 모양과 원장 SQL. 판매자가 실제 유저면 팔릴 때 대금이 그 유저에게 가고,
/// 지울 때 실제 유저의 거래·우편이 같이 지워지면 원장이 깨진다. 장비·캐릭터에 개체 행이 없으면 사도 받을 수 없다.
/// </summary>
public class AuctionDummyTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const long RealSeller = 7;

    private static readonly ItemTableRow Carp = new() { ItemTID = 10001, Name = "붕어", ItemType = ItemType.Excavation, GlobalRarity = GlobalRarity.Common, BasePrice = 10 };
    private static readonly ItemTableRow Wood = new() { ItemTID = 40001, Name = "잡목", ItemType = ItemType.Logging, GlobalRarity = GlobalRarity.Uncommon, BasePrice = 0 };

    private static readonly EquipTableRow Hoe = new()
    {
        EquipTID = 501, Name = "괭이", GlobalRarity = GlobalRarity.Rare, EquipKind = EquipKind.Weapon, Industry = IndustryType.Farming, SpeedAddPermille = 100, BasePrice = 50,
    };

    private static readonly CharacterTableRow Mole = new() { CharacterTID = 1001, Name = "두더지", GlobalRarity = GlobalRarity.Epic, Farming = 3, Mining = 7, BasePrice = 80 };

    private static readonly ArtifactTableRow Idol = new() { ArtifactTID = 301, Name = "우상", GlobalRarity = GlobalRarity.Legendary, BasePrice = 40 };

    private readonly SqliteFixture _db = new();

    public AuctionDummyTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    private static DummyPool Items(params ItemTableRow[] rows) => new() { Items = rows };

    // 희귀 장비는 칸 2개 · 등급마다 속도 +30 옵션 하나뿐인 풀 — 어느 등급이 뽑혀도 칸마다 +30이다.
    private static EnchantCatalog Enchants()
    {
        var catalog = new EnchantCatalog();
        var options = Enumerable.Range((int)GlobalRarity.Common, (int)GlobalRarity.Mythic).Select(g => new EnchantOptionTableRow
        {
            EnchantOptionTID = 900 + g, Grade = (GlobalRarity)g, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 30, Weight = 1,
        });
        catalog.Load(options, new[] { new EnchantGradeTableRow { Grade = GlobalRarity.Rare, SlotCount = 2 } }, Array.Empty<EnchantItemTableRow>());
        return catalog;
    }

    // ★1 = 누적 2 · ★2 = 누적 5
    private static CharacterStarCatalog Stars()
    {
        var catalog = new CharacterStarCatalog();
        catalog.Load(new[] { new CharacterStarTableRow { CharacterStarTID = 1, RequiredCount = 2 }, new CharacterStarTableRow { CharacterStarTID = 2, RequiredCount = 3 } });
        return catalog;
    }

    private static DummyPool All() => new()
    {
        Items = new[] { Carp }, Equips = new[] { Hoe }, Characters = new[] { Mole }, Artifacts = new[] { Idol },
        Enchants = Enchants(), Stars = Stars(), MaxLevel = 30,
    };

    private static List<DummyListing> BuildOf(EAuctionKind kind, int count)
        => AuctionDummy.Build(All(), count, Now, new Random(1)).Where(l => l.Item.Kind == kind).ToList();

    // ─────────────────────── 만들기 — 자원 ───────────────────────

    [Fact]
    public void 요청한_개수만큼_만들고_판매자는_더미다()
    {
        var listings = AuctionDummy.Build(Items(Carp), 30, Now);

        listings.Count.ShouldBe(30);
        listings.ShouldAllBe(l => l.Item.SellerName == AuctionDummy.SellerName && l.Item.Kind == EAuctionKind.Item);
    }

    [Fact]
    public void 종류를_여럿_주면_돌아가며_쓴다()
    {
        var listings = AuctionDummy.Build(Items(Carp, Wood), 4, Now);

        listings.Select(l => l.Item.Tid).ShouldBe(new[] { 10001, 40001, 10001, 40001 });
    }

    [Fact]
    public void 단가는_즉시_판매가_아래로_내려가지_않고_흩어진다()
    {
        var listings = AuctionDummy.Build(Items(Carp), 50, Now);

        listings.ShouldAllBe(l => l.UnitPrice >= 10);
        listings.Select(l => l.UnitPrice).Distinct().Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void 즉시_판매가가_0인_자원도_단가는_1_이상이다()
    {
        AuctionDummy.Build(Items(Wood), 20, Now).ShouldAllBe(l => l.UnitPrice >= 1);
    }

    [Fact]
    public void 만료는_지금보다_뒤이고_등록_기간_안에서_흩어진다()
    {
        var listings = AuctionDummy.Build(Items(Carp), 50, Now);

        listings.ShouldAllBe(l => l.ExpiresAt > Now && l.ExpiresAt <= Now + AuctionRules.ListingDuration);
        listings.Select(l => l.ExpiresAt).Distinct().Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void 수량은_한_번에_올리는_상한을_넘지_않는다()
    {
        AuctionDummy.Build(Items(Carp), 50, Now).ShouldAllBe(l => l.Item.Count >= 1 && l.Item.Count <= AuctionRules.MaxListingCount);
    }

    // ─────────────────────── 만들기 — 종류 섞기 ───────────────────────

    [Fact]
    public void 네_종류를_모두_주면_종류가_돌아가며_섞인다()
    {
        var listings = AuctionDummy.Build(All(), 8, Now, new Random(1));

        listings.Select(l => l.Item.Kind).ShouldBe(new[]
        {
            EAuctionKind.Item, EAuctionKind.Equip, EAuctionKind.Character, EAuctionKind.Artifact,
            EAuctionKind.Item, EAuctionKind.Equip, EAuctionKind.Character, EAuctionKind.Artifact,
        });
    }

    [Fact]
    public void 빈_종류는_건너뛴다()
    {
        var listings = AuctionDummy.Build(new DummyPool { Items = new[] { Carp }, Artifacts = new[] { Idol } }, 4, Now);

        listings.Select(l => l.Item.Kind).ShouldBe(new[] { EAuctionKind.Item, EAuctionKind.Artifact, EAuctionKind.Item, EAuctionKind.Artifact });
    }

    [Fact]
    public void 장비는_한_개씩_올리고_분류는_장비_종류다()
    {
        BuildOf(EAuctionKind.Equip, 20).ShouldAllBe(l =>
            l.Item.Count == 1 && l.Item.Tid == 501 && l.Item.Category == (int)EquipKind.Weapon && l.Item.Rarity == (int)GlobalRarity.Rare);
    }

    [Fact]
    public void 장비는_등급의_칸_수만큼_인챈트_옵션이_찬다()
    {
        var equips = BuildOf(EAuctionKind.Equip, 40);

        equips.ShouldAllBe(l => l.Item.Options.Count == 2 && l.Item.EnchantGrade >= (int)GlobalRarity.Common && l.Item.EnchantGrade <= (int)GlobalRarity.Mythic);
        equips.Select(l => l.Item.EnchantGrade).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void 장비_검색_속성은_기본_속도에_인챈트_속도를_더한다()
    {
        var attrs = BuildOf(EAuctionKind.Equip, 4)[0].Item.Attrs;

        // 기본 100 + 칸 2개 × 30 = 160
        attrs[(int)EAuctionAttr.EquipSpeed].ShouldBe(160);
        attrs[(int)EAuctionAttr.EnchantSpeed].ShouldBe(60);
        attrs[(int)EAuctionAttr.EquipIndustry].ShouldBe((long)IndustryType.Farming);
    }

    [Fact]
    public void 칸이_없는_등급의_장비는_인챈트_없이_올린다()
    {
        var pool = All() with { Enchants = new EnchantCatalog() };

        AuctionDummy.Build(pool, 8, Now).Where(l => l.Item.Kind == EAuctionKind.Equip)
            .ShouldAllBe(l => l.Item.EnchantGrade == 0 && l.Item.Options.Count == 0);
    }

    [Fact]
    public void 캐릭터는_한_장씩_올리고_레벨과_응축이_범위_안에서_흩어진다()
    {
        var characters = BuildOf(EAuctionKind.Character, 80);

        characters.ShouldAllBe(l => l.Item.Count == 1 && l.Item.Character != null && l.Item.Character.CharacterTid == 1001
                                    && l.Item.Character.Level >= 1 && l.Item.Character.Level <= 30
                                    && l.Item.Character.CondenseCount >= 0 && l.Item.Character.CondenseCount <= 5);
        characters.Select(l => l.Item.Character!.Level).Distinct().Count().ShouldBeGreaterThan(3);
        characters.Select(l => l.Item.Character!.CondenseCount).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void 캐릭터_검색_속성은_레벨과_별과_테이블_적성이다()
    {
        foreach (var l in BuildOf(EAuctionKind.Character, 40))
        {
            var c     = l.Item.Character!;
            var attrs = l.Item.Attrs;
            var star  = c.CondenseCount >= 5 ? 2 : c.CondenseCount >= 2 ? 1 : 0;

            attrs[(int)EAuctionAttr.CharacterLevel].ShouldBe(c.Level);
            attrs[(int)EAuctionAttr.CharacterStar].ShouldBe(star);
            attrs[(int)EAuctionAttr.AptitudeFarming].ShouldBe(3);
            attrs[(int)EAuctionAttr.AptitudeMining].ShouldBe(7);
        }
    }

    [Fact]
    public void 아티팩트는_분류_없이_수량으로_올린다()
    {
        var artifacts = BuildOf(EAuctionKind.Artifact, 40);

        artifacts.ShouldAllBe(l => l.Item.Tid == 301 && l.Item.Category == 0 && l.Item.Rarity == (int)GlobalRarity.Legendary
                                   && l.Item.Count >= 1 && l.Item.Count <= AuctionRules.MaxListingCount);
        artifacts.ShouldAllBe(l => l.UnitPrice >= 40);
    }

    // ─────────────────────── 원장 ───────────────────────

    [Fact]
    public async Task 넣으면_더미_판매자의_거래와_등록_메시지가_개수만큼_남는다()
    {
        var count = await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(Items(Carp), 5, Now), Now);

        count.ShouldBe(5);
        Scalar($"SELECT COUNT(*) FROM t_auction_trade WHERE seller_id = {AuctionDummy.SellerId} AND state = 1 AND listing_fee = 0").ShouldBe(5);
        Scalar("SELECT COUNT(*) FROM t_auction_outbox WHERE kind = 1 AND sent_at IS NULL").ShouldBe(5);
    }

    [Fact]
    public async Task 장비_매물은_더미_판매자의_잠긴_장비_행을_가리킨다()
    {
        await AuctionDb.SeedDummyAsync(Conn, BuildOf(EAuctionKind.Equip, 8), Now);

        // 거래 2건 — 장비 행도 2개, 각 행이 자기 거래에 잠겨 있고 거래의 equip_id·스냅샷이 그 행을 가리킨다.
        Scalar($@"SELECT COUNT(*) FROM t_user_equip e JOIN t_auction_trade t ON t.equip_id = e.equip_id AND e.auction_trade_id = t.trade_id
                  WHERE e.user_id = {AuctionDummy.SellerId} AND e.equip_tid = 501 AND json_extract(t.snapshot, '$.EquipId') = e.equip_id
                    AND e.enchant_grade = json_extract(t.snapshot, '$.EnchantGrade') AND e.enchant_2 <> 0").ShouldBe(2);
    }

    [Fact]
    public async Task 캐릭터_매물은_더미_판매자의_잠긴_캐릭터_행을_가리킨다()
    {
        await AuctionDb.SeedDummyAsync(Conn, BuildOf(EAuctionKind.Character, 8), Now);

        Scalar($@"SELECT COUNT(*) FROM t_character c JOIN t_auction_trade t ON t.character_id = c.character_id AND c.auction_trade_id = t.trade_id
                  WHERE c.user_id = {AuctionDummy.SellerId} AND c.character_tid = 1001
                    AND json_extract(t.snapshot, '$.Character.CharacterId') = c.character_id
                    AND c.level = json_extract(t.snapshot, '$.Character.Level')
                    AND c.condense_count = json_extract(t.snapshot, '$.Character.CondenseCount')").ShouldBe(2);
    }

    [Fact]
    public async Task 지우면_더미의_거래와_우편과_기록만_사라진다()
    {
        await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(Items(Carp), 3, Now), Now);
        var real = await AuctionDb.RegisterAsync(Conn, RealSeller, new AuctionItemSnapshot { Kind = EAuctionKind.Item, Tid = 10001, Count = 1 },
            30, 3, new List<ItemChangeInfo>(), gold: 100, Now.AddHours(48), Now);
        _db.Execute($"INSERT INTO t_user_mail (user_id, template_tid) VALUES ({AuctionDummy.SellerId}, 1), ({RealSeller}, 1)");
        _db.Execute($"INSERT INTO t_auction_history (user_id, type, kind, tid, count, unit_price, amount, fee, trade_id, created_at) VALUES ({AuctionDummy.SellerId}, 1, 1, 10001, 1, 10, 10, 0, 1, '2026-01-01 00:00:00')");

        var deleted = await AuctionDb.PurgeDummyAsync(Conn);

        deleted.ShouldBe(3);
        Scalar($"SELECT COUNT(*) FROM t_auction_trade WHERE seller_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE user_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_auction_history WHERE user_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar("SELECT COUNT(*) FROM t_auction_outbox").ShouldBe(1);
        Scalar($"SELECT COUNT(*) FROM t_auction_trade WHERE trade_id = {real}").ShouldBe(1);
        Scalar($"SELECT COUNT(*) FROM t_user_mail WHERE user_id = {RealSeller}").ShouldBe(1);
    }

    [Fact]
    public async Task 지우면_더미의_장비와_캐릭터_행도_사라지고_실제_유저_것은_남는다()
    {
        await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(All(), 8, Now, new Random(1)), Now);
        _db.Execute($"INSERT INTO t_user_equip (user_id, equip_tid) VALUES ({RealSeller}, 501)");
        _db.Execute($"INSERT INTO t_character (user_id, character_tid) VALUES ({RealSeller}, 1001)");

        await AuctionDb.PurgeDummyAsync(Conn);

        Scalar($"SELECT COUNT(*) FROM t_user_equip WHERE user_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_character WHERE user_id = {AuctionDummy.SellerId}").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_user_equip WHERE user_id = {RealSeller}").ShouldBe(1);
        Scalar($"SELECT COUNT(*) FROM t_character WHERE user_id = {RealSeller}").ShouldBe(1);
    }

    [Fact]
    public async Task 지워도_구매_확정_메시지는_남긴다()
    {
        // 확정이 경매장에 안 가면 구매자가 잡은 예약이 타임아웃까지 매물을 붙든다.
        await AuctionDb.SeedDummyAsync(Conn, AuctionDummy.Build(Items(Carp), 1, Now), Now);
        var tradeId = Scalar("SELECT trade_id FROM t_auction_trade");
        _db.Execute($"INSERT INTO t_auction_outbox (trade_id, kind, payload, purchase_id) VALUES ({tradeId}, 2, '{{}}', 99)");

        await AuctionDb.PurgeDummyAsync(Conn);

        Scalar("SELECT COUNT(*) FROM t_auction_outbox WHERE kind = 2").ShouldBe(1);
    }
}
