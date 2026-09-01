using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="GachaService"/> 검증 — <b>비용 차감과 보상 지급의 순서·경계</b>를 본다.
///
/// <para>
/// 추첨 자체는 <see cref="WeightedPickerTest"/>·<see cref="GachaPoolCatalogTest"/>가 담당한다.
/// 여기서는 후보를 1종으로 고정해 <b>무엇이 뽑히는지가 아니라 뽑힌 뒤 무엇이 일어나는지</b>만 본다.
/// </para>
///
/// <para>
/// 비용 값(단차·10연차)은 <c>GachaInfoTable</c>에서 읽는다. 밸런스 수치를 테스트에 박으면
/// 엑셀을 조정할 때마다 빨개진다 — 여기서 지켜야 할 것은 값이 아니라 <b>어느 값을 쓰는가</b>다.
/// </para>
/// </summary>
public class GachaServiceTest
{
    private const int ItemPoolId      = 1;   // GachaInfoTable에 메타가 있는 아이템 풀
    private const int CharacterPoolId = 2;   // GachaInfoTable에 메타가 있는 캐릭터 풀
    private const int NoMetaPoolId    = 999; // 추첨 풀만 있고 메타가 없는 풀

    private const int RewardItemTid      = 100001;
    private const int RewardCharacterTid = 1002;

    public GachaServiceTest() => GameTableFixture.EnsureLoaded();

    /// <summary>풀마다 후보를 1종만 두어 추첨 결과를 확정시킨다.</summary>
    private static GachaService BuildService()
    {
        var catalog = new GachaPoolCatalog();
        catalog.Load(new[]
        {
            new GachaEntry(ItemPoolId,      GachaRewardType.Item,      RewardItemTid,      1, 100),
            new GachaEntry(CharacterPoolId, GachaRewardType.Character, RewardCharacterTid, 1, 100),
            new GachaEntry(NoMetaPoolId,    GachaRewardType.Item,      RewardItemTid,      1, 100),
        });

        return new GachaService(catalog);
    }

    private static GachaInfoTableRow Info(int gachaId)
    {
        GameTable.GachaInfoTable.TryGet(gachaId, out var info).ShouldBeTrue();
        return info;
    }

    /// <summary>비용을 낼 수 있는 유저. 지급 경로가 아직 없어 테스트에서 직접 넣는다.</summary>
    private static (User User, TestUserBuilder B) RichUser(long gold = 1_000_000)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainGold(gold);
        return (user, b);
    }

    private static S_GachaDrawResponse LastDrawResponse(TestUserBuilder b)
        => b.Channel.SentOf<S_GachaDrawResponse>().Last();

    [Fact]
    public void 아이템_풀은_뽑은_아이템을_인벤토리에_반영한다()
    {
        var (user, b) = RichUser();

        BuildService().Draw(user, ItemPoolId, 10);

        var res = LastDrawResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Rewards!.Count.ShouldBe(10);
        res.Rewards.ShouldAllBe(r => r.RewardType == EGachaRewardType.Item && r.ItemId == RewardItemTid);

        // 같은 아이템 10개는 인벤토리 갱신 1건으로 합쳐진다.
        var change = res.ItemChangeInfos!.ShouldHaveSingleItem();
        change.ItemId.ShouldBe(RewardItemTid);
        change.Count.ShouldBe(10);

        // 아이템 풀은 캐릭터를 지급하지 않는다.
        b.DB.PostedOf<GrantCharacterRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 캐릭터_풀은_뽑은_수만큼_지급을_요청한다()
    {
        var (user, b) = RichUser();

        BuildService().Draw(user, CharacterPoolId, 10);

        var res = LastDrawResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Rewards!.Count.ShouldBe(10);
        res.Rewards.ShouldAllBe(r => r.RewardType == EGachaRewardType.Character
                                     && r.CharacterTid == RewardCharacterTid
                                     && r.ItemId == 0);

        // 10장을 DB 왕복 한 번으로 지급한다 — 장당 왕복하면 목록 갱신이 열 번 쪼개진다.
        var grant = b.DB.PostedOf<GrantCharacterRepository>().ShouldHaveSingleItem();
        grant.CharacterTids.Count.ShouldBe(10);

        // 캐릭터 풀은 인벤토리를 건드리지 않는다.
        res.ItemChangeInfos!.ShouldBeEmpty();
    }

    [Fact]
    public void 개체_PK는_지급이_끝난_뒤에_목록으로_내려간다()
    {
        var (user, b) = RichUser();

        // 뽑기 응답 시점에는 아직 보유 캐릭터가 늘지 않는다 — PK를 DB가 발급하기 때문이다.
        BuildService().Draw(user, CharacterPoolId, 1);
        user.Characters.Count.ShouldBe(0);

        user.OnGachaCharactersGranted(new[] { (Id: 500L, Tid: RewardCharacterTid) });

        user.Characters.Count.ShouldBe(1);
        var sent = b.Channel.SentOf<S_CharacterListResponse>().ShouldHaveSingleItem();
        sent.Characters!.ShouldHaveSingleItem().CharacterTid.ShouldBe(RewardCharacterTid);
    }

    [Fact]
    public void 골드가_모자라면_거절하고_아무것도_지급하지_않는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();   // 골드 0

        BuildService().Draw(user, CharacterPoolId, 10);

        LastDrawResponse(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(0);
        b.DB.PostedOf<GrantCharacterRepository>().ShouldBeEmpty();
        b.DB.PostedOf<SaveCurrencyRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 단차는_CostSingle을_10연차는_CostMulti를_차감한다()
    {
        var info = Info(CharacterPoolId);

        var (single, singleB) = RichUser();
        var before = single.Gold;
        BuildService().Draw(single, CharacterPoolId, 1);
        single.Gold.ShouldBe(before - info.CostSingle);
        LastDrawResponse(singleB).Result.ShouldBe(EResultCode.Ok);

        // 10연차가 단차 × 10으로 계산되지 않고 자기 컬럼을 쓰는지 본다.
        var (multi, _) = RichUser();
        BuildService().Draw(multi, CharacterPoolId, 10);
        multi.Gold.ShouldBe(before - info.CostMulti);
    }

    [Fact]
    public void 메타가_없는_풀은_거절한다()
    {
        var (user, b) = RichUser();
        var before = user.Gold;

        // 추첨 풀은 있지만 비용을 적어 둔 행이 없다 — 통과시키면 그 풀만 공짜가 된다.
        BuildService().Draw(user, NoMetaPoolId, 1);

        LastDrawResponse(b).Result.ShouldBe(EResultCode.InvalidGachaId);
        user.Gold.ShouldBe(before);
    }

    [Fact]
    public void 허용되지_않는_횟수는_차감_전에_거절한다()
    {
        var (user, b) = RichUser();
        var before = user.Gold;

        BuildService().Draw(user, CharacterPoolId, 5);

        LastDrawResponse(b).Result.ShouldBe(EResultCode.InvalidDrawCount);
        user.Gold.ShouldBe(before);
    }
}
