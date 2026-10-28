using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 유물 감정 · 아티팩트(T-150 · T-181) 검증 — 유물은 <c>OpenCostGold</c>만큼 골드를 내고 여는 상자다.
/// 아티팩트는 인벤토리 아티팩트 탭에 쌓이고, <b>도감에 장착하고 켜 둔 번호만 · 번호당 한 번</b> 효과가 붙는다.
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

    private static UserArtifactRow Equipped(int artifactTid) => new() { artifact_tid = artifactTid };

    private static S_ItemUseResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem();

    // ─────────────────────── 감정 비용 ───────────────────────

    [Fact]
    public void 유물을_감정하면_감정_비용이_빠진다()
    {
        var (user, b) = UserWithRelics(3, 1000);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, CommonRelicTid, 1, TestUserBuilder.Base);

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.Gold.ShouldBe(800);   // 1000 − 200
        user.GetItemCount(CommonRelicTid).ShouldBe(2);
        user.GetItemCount(ShardTid).ShouldBe(1);
    }

    [Fact]
    public void 골드가_모자라면_NotEnoughCurrency이고_유물과_골드가_그대로다()
    {
        var (user, b) = UserWithRelics(1, 199);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, CommonRelicTid, 1, TestUserBuilder.Base);

        Response(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(199);
        user.GetItemCount(CommonRelicTid).ShouldBe(1);
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
    public void 유물은_한_번에_하나만_감정하고_여러_개면_아무것도_바뀌지_않는다()
    {
        var (user, b) = UserWithRelics(2, 1000);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Item, ShardTid, 1, 100)).OpenBox(user, CommonRelicTid, 2, TestUserBuilder.Base);

        Response(b).Result.ShouldBe(EResultCode.InvalidUseCount);
        user.Gold.ShouldBe(1000);
        user.GetItemCount(CommonRelicTid).ShouldBe(2);
        user.GetItemCount(ShardTid).ShouldBe(0);
    }

    [Fact]
    public void 감정_비용_할인을_가진_만큼_비용이_준다()
    {
        var (user, b) = UserWithRelics(1, 1000);
        user.LoadArtifacts(new[] { Equipped(OldCoin) }, Array.Empty<UserArtifactStackRow>());

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

    // ─────────────────────── 아티팩트 ───────────────────────

    [Fact]
    public void 아티팩트가_나오면_인벤토리_아티팩트_탭에_쌓이고_효과는_아직_없다()
    {
        var (user, b) = UserWithRelics(1, 1000);

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Artifact, FishermanCharm, 2, 100)).OpenBox(user, CommonRelicTid, 1, TestUserBuilder.Base);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Rewards!.ShouldAllBe(r => r.RewardType == EGachaRewardType.Artifact && r.ArtifactTid == FishermanCharm && r.Rarity == EGlobalRarity.Common);
        user.GetArtifactCount(FishermanCharm).ShouldBe(2);
        user.GetItemCount(CommonRelicTid).ShouldBe(0);
        res.ItemChangeInfos!.ShouldAllBe(c => c.ItemId == CommonRelicTid);   // 자원 탭에는 유물 차감만

        var saved = b.DB.PostedOf<SaveArtifactStacksRepository>().ShouldHaveSingleItem().Changes.ShouldHaveSingleItem();
        (saved.ItemId, saved.Count, saved.Slot).ShouldBe((FishermanCharm, 2, 0));
        b.Channel.SentOf<S_ArtifactInventoryResponse>().Last().Stacks.ShouldHaveSingleItem().Count.ShouldBe(2);
        user.IsArtifactEquipped(FishermanCharm).ShouldBeFalse();
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(0);
    }

    [Fact]
    public void 아티팩트_탭이_가득_차면_감정_결과가_통째로_우편으로_간다()
    {
        var (user, b) = UserWithRelics(1, 1000);
        var full = Enumerable.Range(0, User.StorageCapacity)
            .Select(slot => new UserArtifactStackRow { slot = slot, artifact_tid = CrackedComb, count = User.ArtifactMaxStack });
        user.LoadArtifacts(Array.Empty<UserArtifactRow>(), full.ToList());

        ServiceWith(new GachaEntry(CommonRelicPool, GachaRewardType.Artifact, FishermanCharm, 1, 100)).OpenBox(user, CommonRelicTid, 1, TestUserBuilder.Base);

        var res = Response(b);
        (res.Result, res.StoredInMail).ShouldBe((EResultCode.Ok, true));
        user.GetArtifactCount(FishermanCharm).ShouldBe(0);
        b.DB.PostedOf<SaveArtifactStacksRepository>().ShouldBeEmpty();
        user.Gold.ShouldBe(800);   // 감정 비용은 낸다 — 보상만 우편에 맡긴다
    }

    [Fact]
    public void 도감에_장착하고_켜_둔_번호만_효과가_붙는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.LoadArtifacts(new[] { Equipped(FishermanCharm), Equipped(CrackedComb) },
            new[] { new UserArtifactStackRow { slot = 0, artifact_tid = FishermanCharm, count = 3 }, new UserArtifactStackRow { slot = 1, artifact_tid = OldCoin, count = 1 } });

        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(3);   // 전 산업 1 + 농사 2 — 인벤토리 3개는 더하지 않는다
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Mining).ShouldBe(1);    // 전 산업 1만
        user.GetArtifactEffect(UserTraitEffect.AppraiseCostDiscount, IndustryType.None).ShouldBe(0);   // 인벤토리에만 있는 낡은 동전
    }

    [Fact]
    public void 표에_없는_번호는_적재하지_않는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.LoadArtifacts(new[] { Equipped(99_999) }, new[] { new UserArtifactStackRow { slot = 0, artifact_tid = 99_999, count = 1 } });

        user.GetArtifactCount(99_999).ShouldBe(0);
        user.IsArtifactEquipped(99_999).ShouldBeFalse();
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
