using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 유물 감정 · 아티팩트 보관함(T-150) 검증 — 유물은 <c>OpenCostGold</c>만큼 골드를 내고 여는 상자다.
/// 아티팩트는 인벤토리가 아니라 번호별 보관함에 쌓이고, <b>수량 1 이상인 번호만 · 번호당 한 번</b> 효과가 붙는다.
/// </summary>
public class RelicAppraiseTest
{
    private const int CommonRelicTid  = 100061;   // 흙 묻은 유물 — 감정 200골드 · 풀 25
    private const int CommonRelicPool = 25;
    private const int ShardTid        = 10001;    // 깨진 토기 조각
    private const int FishermanCharm  = 1;        // 어부의 낡은 부적 — 전 산업 작업속도 +1‰
    private const int CrackedComb     = 2;        // 깨진 토기 빗 — 농사 작업속도 +2‰
    private const int OldCoin         = 5;        // 낡은 동전 — 감정 비용 할인 5‰

    public RelicAppraiseTest() => GameTableFixture.EnsureLoaded();

    private static GachaService ServiceWith(params GachaEntry[] entries)
    {
        var catalog = new GachaPoolCatalog();
        catalog.Load(entries);
        return new GachaService(catalog);
    }

    private static (User User, TestUserBuilder B) UserWithRelics(int relics, long gold)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainItem(CommonRelicTid, relics);
        user.GainGold(gold);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_ItemUseResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem();

    // ─────────────────────── 감정 비용 ───────────────────────

    [Fact]
    public void 유물을_감정하면_개수만큼_감정_비용이_빠진다()
    {
        var (user, b) = UserWithRelics(3, 1000);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, CommonRelicTid, 3, TestUserBuilder.Base);

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.Gold.ShouldBe(400);   // 1000 − 200 × 3
        user.GetItemCount(CommonRelicTid).ShouldBe(0);
        user.GetItemCount(ShardTid).ShouldBe(3);
    }

    [Fact]
    public void 골드가_모자라면_NotEnoughCurrency이고_유물과_골드가_그대로다()
    {
        var (user, b) = UserWithRelics(3, 599);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, CommonRelicTid, 3, TestUserBuilder.Base);

        Response(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(599);
        user.GetItemCount(CommonRelicTid).ShouldBe(3);
        user.GetItemCount(ShardTid).ShouldBe(0);
    }

    [Fact]
    public void 상자는_감정_비용이_없다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainItem(100019, 2);   // 나무 상자 Lv1 — OpenCostGold 0

        ServiceWith(new GachaEntry(9, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, 100019, 2, TestUserBuilder.Base);

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.Gold.ShouldBe(0);
    }

    [Fact]
    public void 감정_비용_할인을_가진_만큼_비용이_준다()
    {
        var (user, b) = UserWithRelics(1, 1000);
        user.GainArtifacts(new Dictionary<int, int> { [OldCoin] = 1 }, TestUserBuilder.Base);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, CommonRelicTid, 1, TestUserBuilder.Base);

        user.Gold.ShouldBe(1000 - 199);   // 200 × (1000 − 5) / 1000
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(300, 700)]
    [InlineData(500, 500)]
    [InlineData(800, 500)]   // 50%에서 자른다 — 감정이 공짜가 되지 않는다
    public void 감정_비용_할인은_50퍼센트에서_잘린다(int discountPermille, long expected)
    {
        User.DiscountAppraiseCost(1000, discountPermille).ShouldBe(expected);
    }

    // ─────────────────────── 아티팩트 보관함 ───────────────────────

    [Fact]
    public void 아티팩트가_나오면_인벤토리가_아니라_보관함에_쌓인다()
    {
        var (user, b) = UserWithRelics(2, 1000);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Artifact, FishermanCharm, 1, 100)).OpenBox(user, CommonRelicTid, 2, TestUserBuilder.Base);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Rewards!.ShouldAllBe(r => r.RewardType == EGachaRewardType.Artifact && r.ArtifactTid == FishermanCharm && r.Rarity == EGlobalRarity.Common);
        user.GetArtifactCount(FishermanCharm).ShouldBe(2);
        user.GetItemCount(CommonRelicTid).ShouldBe(0);
        res.ItemChangeInfos!.ShouldAllBe(c => c.ItemId == CommonRelicTid);   // 인벤토리에는 유물 차감만

        b.DB.PostedOf<SaveUserArtifactRepository>().ShouldHaveSingleItem().Changes.ShouldBe(new[] { (FishermanCharm, 2) });
        b.Channel.SentOf<S_ArtifactListResponse>().Last().Artifacts
            .ShouldHaveSingleItem().ShouldSatisfyAllConditions(a => a.ArtifactTid.ShouldBe(FishermanCharm), a => a.Count.ShouldBe(2));
    }

    [Fact]
    public void 같은_아티팩트를_여러_개_가져도_효과는_한_번만_붙는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.GainArtifacts(new Dictionary<int, int> { [FishermanCharm] = 3, [CrackedComb] = 1 }, TestUserBuilder.Base);

        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(3);   // 전 산업 1 + 농사 2
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Mining).ShouldBe(1);    // 전 산업 1만
        user.GetArtifactEffect(UserTraitEffect.YieldAdd, IndustryType.Farming).ShouldBe(0);
    }

    [Fact]
    public void 수량이_0인_번호는_도감에_남지만_효과가_꺼진다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadArtifacts(new[] { new UserArtifactRow { artifact_tid = FishermanCharm, count = 0 } });

        user.SendArtifactList();

        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(0);
        b.Channel.SentOf<S_ArtifactListResponse>().Last().Artifacts.ShouldHaveSingleItem().Count.ShouldBe(0);
    }

    [Fact]
    public void 표에_없는_번호는_적재하지_않는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.LoadArtifacts(new[] { new UserArtifactRow { artifact_tid = 99_999, count = 1 } });

        user.GetArtifactCount(99_999).ShouldBe(0);
    }

    // ─────────────────────── 유물 발견 확률 ───────────────────────

    [Fact]
    public void 유물_발견_확률_가산은_행의_확률에_곱해진다()
    {
        var catalog = new CommonRewardCatalog();
        catalog.Load(Array.Empty<CommonRewardTableRow>(), new[]
        {
            new CommonRewardOverrideTableRow { CommonRewardOverrideTID = 1, IndustryType = IndustryType.Excavation, IndustryLevel = 1, ItemTID = CommonRelicTid, Count = 1, ChancePerMillion = 500_000 },
        });

        // 50% × (1000 + 1000)‰ = 100% — 판정마다 나온다
        catalog.Roll(IndustryType.Excavation, 1, 20, new Random(1), chanceAddPermille: 1000)[CommonRelicTid].ShouldBe(20);
    }
}
