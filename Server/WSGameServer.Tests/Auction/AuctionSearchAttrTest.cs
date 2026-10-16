using GameData;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 경매 검색 확장(T-155) — 메인이 등록 때 수치 속성(작업속도 · 인챈트 합계 · 레벨 · ★ · 적성)을 계산해 싣고,
/// 검색 요청의 정렬·범위·속성 필터를 경매장으로 옮긴다. 경매장은 GameData를 모르므로 여기서 틀리면 거를 값 자체가 없다.
/// </summary>
public class AuctionSearchAttrTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int  SwordTid = 1001;
    private const long Sword    = 55;
    private const long CharA    = 500;   // 램볼(1001) — 전 산업 적성 1
    private const long CharB    = 501;

    private readonly FakeAuctionClient _client = new();

    public AuctionSearchAttrTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) NewUser()
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", GlobalRarity = GlobalRarity.Rare, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.Mining, SpeedAddPermille = 100, BasePrice = 70 },
        });
        b.Enchants.LoadAll();

        var user = b.Build();
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharA, character_tid = 1001, level = 7, exp = 0 },
            new CharacterRow { character_id = CharB, character_tid = 1001, level = 1, exp = 0 },
        });
        // 1301 = 희귀 전 산업 속도 +20‰ · 1307 = 희귀 경험치 +30‰
        user.LoadEquips(
            new[] { new UserEquipRow { equip_id = Sword, equip_tid = SwordTid, slot_position = 0, enchant_grade = (int)GlobalRarity.Rare, enchant_1 = 1301, enchant_2 = 1307 } },
            Array.Empty<CharacterEquipRow>());

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static Dictionary<EAuctionAttr, long> Attrs(TestUserBuilder b)
        => b.DB.PostedOf<RegisterAuctionRepository>().Single().Item.Attrs.ToDictionary(kv => (EAuctionAttr)kv.Key, kv => kv.Value);

    // ── 등록 스냅샷 ──

    [Fact]
    public void 장비_스냅샷에_산업_작업속도_인챈트_합계가_실린다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Equip, 0, 0, Sword, 70, Now);

        // 작업속도 = 기본 100 + 인챈트 속도 20 = 120
        Attrs(b).ShouldBe(new Dictionary<EAuctionAttr, long>
        {
            [EAuctionAttr.EquipIndustry]       = (long)IndustryType.Mining,
            [EAuctionAttr.EquipSpeed]          = 120,
            [EAuctionAttr.EnchantSpeed]        = 20,
            [EAuctionAttr.EnchantCharacterExp] = 30,
        }, ignoreOrder: true);
    }

    [Fact]
    public void 캐릭터_스냅샷에_레벨_별_산업별_적성이_실린다()
    {
        var (user, b) = NewUser();

        user.TryRegisterAuction(EAuctionKind.Character, 0, 0, 0, 50, Now, CharA);

        var attrs = Attrs(b);
        (attrs[EAuctionAttr.CharacterLevel], attrs[EAuctionAttr.CharacterStar]).ShouldBe((7L, 0L));
        attrs[EAuctionAttr.AptitudeFarming].ShouldBe(1);
        attrs[EAuctionAttr.AptitudeHunting].ShouldBe(1);
    }

    [Fact]
    public void 경매장_등록_요청에_속성이_그대로_간다()
    {
        var (user, b) = NewUser();
        _client.Register = _ => Task.FromResult(new Proto.RegisterReply());
        user.TryRegisterAuction(EAuctionKind.Character, 0, 0, 0, 50, Now, CharA);

        var item = b.DB.PostedOf<RegisterAuctionRepository>().Single().Item;
        var snapshot = AuctionRelay.ToSnapshot(new AuctionRegisterMessage(1, user.Uid, item, 50, 0));

        snapshot.Attrs[(int)EAuctionAttr.CharacterLevel].ShouldBe(7);
    }

    // ── 검색 요청 ──

    [Fact]
    public void 정렬_단가_하한_속성_필터가_경매장으로_가고_건수와_커서가_돌아온다()
    {
        var (user, b) = NewUser();
        _client.Search = _ => Task.FromResult(new Proto.SearchReply { TotalCount = 42, NextCursorSortValue = -30, NextCursorListingId = 9, HasMore = true });

        user.TrySearchAuction(new C_AuctionSearchRequest
        {
            Kind            = EAuctionKind.Character,
            Sort            = EAuctionSort.AttrDesc,
            SortAttr        = EAuctionAttr.CharacterLevel,
            MinUnitPrice    = 100,
            Attrs           = new List<AuctionAttrRangeInfo> { new() { Attr = EAuctionAttr.CharacterStar, Min = 2 } },
            CursorSortValue = -40,
            CursorListingId = 3,
        }, Now);

        var sent = _client.RequestsOf<Proto.SearchRequest>().Single();
        (sent.Sort, sent.SortAttr, sent.MinUnitPrice, sent.CursorUnitPrice, sent.CursorListingId)
            .ShouldBe(((int)EAuctionSort.AttrDesc, (int)EAuctionAttr.CharacterLevel, 100L, -40L, 3L));
        sent.Attrs.Single().ShouldSatisfyAllConditions(a => a.Key.ShouldBe((int)EAuctionAttr.CharacterStar), a => a.Min.ShouldBe(2));

        var res = b.Channel.SentOf<S_AuctionSearchResponse>().Last();
        (res.TotalCount, res.NextCursorSortValue, res.NextCursorListingId).ShouldBe((42, -30L, 9L));
    }

    [Fact]
    public void 단가_낮은순은_옛_클라의_CursorUnitPrice도_받는다()
    {
        var (user, _) = NewUser();
        _client.Search = _ => Task.FromResult(new Proto.SearchReply());

        user.TrySearchAuction(new C_AuctionSearchRequest { Kind = EAuctionKind.Item, CursorUnitPrice = 70, CursorListingId = 9 }, Now);

        _client.RequestsOf<Proto.SearchRequest>().Single().CursorUnitPrice.ShouldBe(70);
    }

    [Fact]
    public void 속성_필터가_너무_많으면_거절한다()
    {
        var (user, b) = NewUser();

        var attrs = Enumerable.Range(0, User.MaxSearchAttrs + 1).Select(_ => new AuctionAttrRangeInfo { Attr = EAuctionAttr.CharacterLevel }).ToList();
        user.TrySearchAuction(new C_AuctionSearchRequest { Attrs = attrs }, Now);

        b.Channel.SentOf<S_AuctionSearchResponse>().Last().Result.ShouldBe(EResultCode.AuctionInvalidRequest);
        _client.RequestsOf<Proto.SearchRequest>().ShouldBeEmpty();
    }
}
