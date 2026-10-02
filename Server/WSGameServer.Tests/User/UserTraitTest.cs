using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 레벨형 특성 검증(T-108) — 특성 하나가 기본 레벨에서 최대 레벨까지 1씩 오른다. 조건은 다음 레벨의 계정 레벨 + 특성 포인트다.
/// <b>포인트는 조건을 전부 통과한 뒤에만 빠진다.</b> 거절 경로에서 포인트가 움직이면 트리가 샌다.
/// </summary>
public class UserTraitTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    // 테스트 전용 특성 — 엑셀 값에 기대지 않는다.
    //   102 낚시 개척  : 기본 Lv1 · 최대 Lv3 · Lv2 계정 5 · Lv3 계정 15
    //   202 낚시 속도  : 기본 Lv0 · 최대 Lv2 · 레벨당 +10% · 포인트 1
    //   300 공통 산출량: 기본 Lv0 · 최대 Lv2 · 레벨당 +20%
    //   302 낚시 산출량: 기본 Lv0 · 최대 Lv2 · 레벨당 +30% · 포인트 2
    private const int FishingPioneer = 102;
    private const int FishingSpeed   = 202;
    private const int CommonYield    = 300;
    private const int FishingYield   = 302;

    public UserTraitTest() => GameTableFixture.EnsureLoaded();

    private static UserTraitTableRow Trait(int tid, UserTraitEffect effect, IndustryType industry, int value, int baseLevel, int maxLevel, int point = 1)
        => new()
        {
            UserTraitTID = tid, Name = $"특성 {tid}", TraitPoint = point, EffectType = effect,
            Industry = industry, EffectValue = value, BaseLevel = baseLevel, MaxLevel = maxLevel,
        };

    private static UserTraitLevelTableRow Level(int tid, int level, int accountLevel = 0)
        => new() { UserTraitLevelTID = tid * 100 + level, UserTraitTID = tid, Level = level, AccountLevel = accountLevel };

    private static (User User, TestUserBuilder B) UserWith(int level, int traitPoint)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Traits.Load(
            new[]
            {
                Trait(FishingPioneer, UserTraitEffect.IndustryUnlock, IndustryType.Fishing, 0, baseLevel: 1, maxLevel: 3),
                Trait(FishingSpeed, UserTraitEffect.SpeedAdd, IndustryType.Fishing, 100, baseLevel: 0, maxLevel: 2),
                Trait(CommonYield, UserTraitEffect.YieldAdd, IndustryType.None, 200, baseLevel: 0, maxLevel: 2),
                Trait(FishingYield, UserTraitEffect.YieldAdd, IndustryType.Fishing, 300, baseLevel: 0, maxLevel: 2, point: 2),
            },
            new[]
            {
                Level(FishingPioneer, 2, accountLevel: 5), Level(FishingPioneer, 3, accountLevel: 15),
                Level(FishingSpeed, 1), Level(FishingSpeed, 2),
                Level(CommonYield, 1), Level(CommonYield, 2),
                Level(FishingYield, 1), Level(FishingYield, 2),
            });
        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow { IndustryLevelTID = 201, IndustryType = IndustryType.Fishing, Level = 1, Name = "개울", RequiredScore = 30_000 },
            new IndustryLevelTableRow { IndustryLevelTID = 202, IndustryType = IndustryType.Fishing, Level = 2, Name = "저수지", RequiredScore = 30_000 },
            new IndustryLevelTableRow { IndustryLevelTID = 203, IndustryType = IndustryType.Fishing, Level = 3, Name = "강", RequiredScore = 30_000 },
        });

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        user.LoadAccount(new AccountRow { level = level, exp = 0, trait_point = traitPoint });

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_UserTraitLearnResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_UserTraitLearnResponse>().ShouldHaveSingleItem();

    // ─────────────────────── 레벨업 ───────────────────────

    [Fact]
    public void 조건을_채우면_1레벨_오르고_포인트가_빠지고_저장된다()
    {
        var (user, b) = UserWith(level: 1, traitPoint: 2);

        user.TryLearnTrait(FishingSpeed, Base);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Level.ShouldBe(1);
        user.GetTraitLevel(FishingSpeed).ShouldBe(1);
        user.TraitPoint.ShouldBe(1);
        var saved = b.DB.PostedOf<SaveUserTraitRepository>().ShouldHaveSingleItem();
        saved.UserTraitTid.ShouldBe(FishingSpeed);
        saved.Level.ShouldBe(1);
        b.Channel.SentOf<S_AccountLevelResponse>().Last().TraitPoint.ShouldBe(1);
    }

    [Fact]
    public void 올린_적_없는_특성은_기본_레벨이다()
    {
        var (user, _) = UserWith(level: 1, traitPoint: 0);

        user.GetTraitLevel(FishingPioneer).ShouldBe(1);
        user.GetTraitLevel(FishingSpeed).ShouldBe(0);
    }

    [Fact]
    public void 로그인_때_저장된_레벨로_되살아나고_목록을_보낸다()
    {
        var (user, b) = UserWith(level: 1, traitPoint: 0);

        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = FishingSpeed, level = 2 } });
        user.SendTraitList();

        user.GetTraitSpeedAdd(IndustryType.Fishing).ShouldBe(200);
        var info = b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldHaveSingleItem();
        info.UserTraitTID.ShouldBe(FishingSpeed);
        info.Level.ShouldBe(2);
    }

    // ─────────────────────── 개척 = 산업 레벨 ───────────────────────

    [Fact]
    public void 개척_기본_레벨에서는_산업_Lv1만_열려_있다()
    {
        var (user, _) = UserWith(level: 1, traitPoint: 0);

        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 1).ShouldBeTrue();
        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 2).ShouldBeFalse();
    }

    [Fact]
    public void 개척을_올리면_특성_레벨까지의_산업_레벨이_열린다()
    {
        var (user, _) = UserWith(level: 5, traitPoint: 1);

        user.TryLearnTrait(FishingPioneer, Base);

        user.GetTraitLevel(FishingPioneer).ShouldBe(2);
        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 2).ShouldBeTrue();
        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 3).ShouldBeFalse();
    }

    // ─────────────────────── 속도 ───────────────────────

    [Fact]
    public void 속도_특성은_레벨만큼_그_산업_슬롯_속도에_가산된다()
    {
        // 기본 속도 × (1 + 10% × 2레벨)
        var (user, b) = UserWith(level: 1, traitPoint: 2);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.TryLearnTrait(FishingSpeed, Base);
        user.TryLearnTrait(FishingSpeed, Base);

        slot.CurrentWorkSpeed.ShouldBe(before * 12 / 10);
        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldNotBeEmpty();
    }

    [Fact]
    public void 속도_특성은_다른_산업에는_붙지_않는다()
    {
        var (user, _) = UserWith(level: 1, traitPoint: 1);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Mining, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.TryLearnTrait(FishingSpeed, Base);

        slot.CurrentWorkSpeed.ShouldBe(before);
    }

    // ─────────────────────── 산출량 ───────────────────────

    [Fact]
    public void 산출량은_공통과_그_산업을_더한다()
    {
        // 공통 20% × 1 + 낚시 30% × 1 = 50%. 채굴은 공통만.
        var (user, _) = UserWith(level: 1, traitPoint: 3);

        user.TryLearnTrait(CommonYield, Base);
        user.TryLearnTrait(FishingYield, Base);

        user.GetTraitYieldAdd(IndustryType.Fishing).ShouldBe(500);
        user.GetTraitYieldAdd(IndustryType.Mining).ShouldBe(200);
    }

    [Fact]
    public void 산출량이_100퍼센트면_판정마다_자원이_2개씩_나온다()
    {
        // 공통 20%×2 + 낚시 30%×2 = 100% → 소수부가 없어 난수와 무관하다. 5분 = 10판정 → 20개.
        var (user, _) = UserWith(level: 1, traitPoint: 6);
        user.TryLearnTrait(CommonYield, Base);
        user.TryLearnTrait(CommonYield, Base);
        user.TryLearnTrait(FishingYield, Base);
        user.TryLearnTrait(FishingYield, Base);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(20);
    }

    [Theory]
    [InlineData(0, 10)]      // 100% — 그대로
    [InlineData(1000, 20)]   // 200% — 정수부 2
    [InlineData(1500, 30)]   // 250% — 정수부 2 + 소수부 50% (항상 0을 내는 난수라 10판정 전부 1개 더)
    public void ApplyYield는_정수부만큼_주고_소수부는_확률로_1개_더_준다(int yieldAdd, int expected)
    {
        User.ApplyYield(10, yieldAdd, new AlwaysZeroRandom()).ShouldBe(expected);
    }

    [Fact]
    public void ApplyYield의_소수부는_난수가_확률보다_크면_붙지_않는다()
    {
        User.ApplyYield(10, 200, new AlwaysMaxRandom()).ShouldBe(10);
    }

    // ─────────────────────── 거절 경로 — 포인트가 움직이면 안 된다 ───────────────────────

    [Fact]
    public void 없는_특성은_InvalidUserTraitTID다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 2);

        user.TryLearnTrait(9999, Base);

        Response(b).Result.ShouldBe(EResultCode.InvalidUserTraitTID);
        user.TraitPoint.ShouldBe(2);
    }

    [Fact]
    public void 포인트가_모자라면_NotEnoughTraitPoint고_레벨은_그대로다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 1);

        user.TryLearnTrait(FishingYield, Base);   // 비용 2

        Response(b).Result.ShouldBe(EResultCode.NotEnoughTraitPoint);
        user.GetTraitLevel(FishingYield).ShouldBe(0);
        user.TraitPoint.ShouldBe(1);
    }

    [Fact]
    public void 계정_레벨이_모자라면_UnlockLocked고_포인트를_쓰지_않는다()
    {
        var (user, b) = UserWith(level: 4, traitPoint: 2);

        user.TryLearnTrait(FishingPioneer, Base);   // Lv2는 계정 5

        Response(b).Result.ShouldBe(EResultCode.UnlockLocked);
        user.TraitPoint.ShouldBe(2);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 최대_레벨이면_TraitMaxLevel이다()
    {
        var (user, b) = UserWith(level: 1, traitPoint: 3);
        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = FishingSpeed, level = 2 } });

        user.TryLearnTrait(FishingSpeed, Base);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.TraitMaxLevel);
        res.Level.ShouldBe(2);
        user.TraitPoint.ShouldBe(3);
    }

    private sealed class AlwaysZeroRandom : Random
    {
        public override int Next(int maxValue) => 0;
    }

    private sealed class AlwaysMaxRandom : Random
    {
        public override int Next(int maxValue) => maxValue - 1;
    }
}
