using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 건물 트리 노드(특성 · T-183) — 노드는 기본 레벨에서 최대 레벨까지 1씩 오른다. 조건은 앞 노드 최대 레벨 + 그 건물의 남은 포인트다.
/// 포인트는 건물 레벨이고 따로 저장하지 않는다 — 찍은 레벨만큼 줄어든다. 거절 경로에서 레벨이 움직이면 트리가 샌다.
/// </summary>
public class UserTraitTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    // 테스트 전용 노드 — 엑셀 값에 기대지 않는다. 전부 작업장(2) 트리다.
    //   102 발굴 개척  : 기본 Lv1 · 최대 Lv3
    //   202 발굴 속도  : 기본 Lv0 · 최대 Lv2 · 레벨당 +10% · 포인트 1
    //   300 공통 산출량: 기본 Lv0 · 최대 Lv2 · 레벨당 +20%
    //   302 발굴 산출량: 기본 Lv0 · 최대 Lv2 · 레벨당 +30% · 포인트 2
    //   303 채굴 산출량: 기본 Lv0 · 최대 Lv1 · 선행 202(발굴 속도) — 선행 검사용
    private const int ExcavationPioneer = 102;
    private const int ExcavationSpeed   = 202;
    private const int CommonYield       = 300;
    private const int ExcavationYield   = 302;
    private const int GatedYield        = 303;

    public UserTraitTest() => GameTableFixture.EnsureLoaded();

    private static UserTraitTableRow Trait(int tid, UserTraitEffect effect, IndustryType industry, int value, int baseLevel, int maxLevel,
                                           int point = 1, params int[] required)
        => new()
        {
            UserTraitTID = tid, Name = $"특성 {tid}", TraitPoint = point, EffectType = effect,
            Industry = industry, EffectValue = value, BaseLevel = baseLevel, MaxLevel = maxLevel,
            BuildingTID = TestBuildings.Workshop, RequiredTraitTIDs = required,
        };

    /// <summary>작업장 레벨 = 그 트리의 포인트. 관리소 레벨은 다른 건물 포인트가 섞이지 않는지 볼 때 쓴다.</summary>
    private static (User User, TestUserBuilder B) UserWith(int workshopLevel, int officeLevel = 0)
    {
        var b = new TestUserBuilder().WithExcavationDrops();
        var (buildings, levels) = TestBuildings.TwoFree();
        b.Traits.Load(
            new[]
            {
                Trait(ExcavationPioneer, UserTraitEffect.IndustryUnlock, IndustryType.Excavation, 0, baseLevel: 1, maxLevel: 3),
                Trait(ExcavationSpeed, UserTraitEffect.SpeedAdd, IndustryType.Excavation, 100, baseLevel: 0, maxLevel: 2),
                Trait(CommonYield, UserTraitEffect.YieldAdd, IndustryType.None, 200, baseLevel: 0, maxLevel: 2),
                Trait(ExcavationYield, UserTraitEffect.YieldAdd, IndustryType.Excavation, 300, baseLevel: 0, maxLevel: 2, point: 2),
                Trait(GatedYield, UserTraitEffect.YieldAdd, IndustryType.Mining, 100, baseLevel: 0, maxLevel: 1, required: ExcavationSpeed),
            },
            buildings, levels);
        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow { IndustryLevelTID = 201, IndustryType = IndustryType.Excavation, Level = 1, Name = "개울", RequiredScore = 30_000 },
            new IndustryLevelTableRow { IndustryLevelTID = 202, IndustryType = IndustryType.Excavation, Level = 2, Name = "저수지", RequiredScore = 30_000 },
            new IndustryLevelTableRow { IndustryLevelTID = 203, IndustryType = IndustryType.Excavation, Level = 3, Name = "강", RequiredScore = 30_000 },
        });

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        user.LoadBuildings(new[]
        {
            new UserBuildingRow { building_tid = TestBuildings.Workshop, level = workshopLevel },
            new UserBuildingRow { building_tid = TestBuildings.Office,   level = officeLevel },
        });

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_UserTraitLearnResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_UserTraitLearnResponse>().ShouldHaveSingleItem();

    // ─────────────────────── 레벨업 · 포인트 ───────────────────────

    [Fact]
    public void 조건을_채우면_1레벨_오르고_포인트가_빠지고_저장된다()
    {
        var (user, b) = UserWith(workshopLevel: 2);

        user.TryLearnTrait(ExcavationSpeed, Base);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Level.ShouldBe(1);
        user.GetTraitLevel(ExcavationSpeed).ShouldBe(1);
        user.GetBuildingPoints(TestBuildings.Workshop).ShouldBe(1);   // Lv2 − 1
        var saved = b.DB.PostedOf<SaveUserTraitRepository>().ShouldHaveSingleItem();
        saved.UserTraitTid.ShouldBe(ExcavationSpeed);
        saved.Level.ShouldBe(1);
    }

    [Fact]
    public void 남은_포인트는_건물_레벨에서_찍은_레벨_곱하기_비용을_뺀_값이다()
    {
        // Lv6 − 속도 2×1 − 발굴 산출량 1×2 − 개척(기본 Lv1→2) 1×1 = 1
        var (user, _) = UserWith(workshopLevel: 6);

        user.LoadTraits(new[]
        {
            new UserTraitRow { user_trait_tid = ExcavationSpeed,   level = 2 },
            new UserTraitRow { user_trait_tid = ExcavationYield,   level = 1 },
            new UserTraitRow { user_trait_tid = ExcavationPioneer, level = 2 },
        });

        user.GetBuildingPoints(TestBuildings.Workshop).ShouldBe(1);
    }

    [Fact]
    public void 올린_적_없는_특성은_기본_레벨이다()
    {
        var (user, _) = UserWith(workshopLevel: 0);

        user.GetTraitLevel(ExcavationPioneer).ShouldBe(1);
        user.GetTraitLevel(ExcavationSpeed).ShouldBe(0);
    }

    [Fact]
    public void 로그인_때_저장된_레벨로_되살아나고_목록을_보낸다()
    {
        var (user, b) = UserWith(workshopLevel: 0);

        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = ExcavationSpeed, level = 2 } });
        user.SendTraitList();

        user.GetTraitSpeedAdd(IndustryType.Excavation).ShouldBe(200);
        var info = b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldHaveSingleItem();
        info.UserTraitTID.ShouldBe(ExcavationSpeed);
        info.Level.ShouldBe(2);
    }

    // ─────────────────────── 개척 = 산업 레벨 ───────────────────────

    [Fact]
    public void 개척_기본_레벨에서는_산업_Lv1만_열려_있다()
    {
        var (user, _) = UserWith(workshopLevel: 0);

        user.IsIndustryLevelUnlocked(IndustryType.Excavation, 1).ShouldBeTrue();
        user.IsIndustryLevelUnlocked(IndustryType.Excavation, 2).ShouldBeFalse();
    }

    [Fact]
    public void 개척을_올리면_특성_레벨까지의_산업_레벨이_열린다()
    {
        var (user, _) = UserWith(workshopLevel: 1);

        user.TryLearnTrait(ExcavationPioneer, Base);

        user.GetTraitLevel(ExcavationPioneer).ShouldBe(2);
        user.IsIndustryLevelUnlocked(IndustryType.Excavation, 2).ShouldBeTrue();
        user.IsIndustryLevelUnlocked(IndustryType.Excavation, 3).ShouldBeFalse();
    }

    // ─────────────────────── 속도 ───────────────────────

    [Fact]
    public void 속도_특성은_레벨만큼_그_산업_슬롯_속도에_가산된다()
    {
        // 기본 속도 × (1 + 10% × 2레벨)
        var (user, b) = UserWith(workshopLevel: 2);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Excavation, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.TryLearnTrait(ExcavationSpeed, Base);
        user.TryLearnTrait(ExcavationSpeed, Base);

        slot.CurrentWorkSpeed.ShouldBe(before * 12 / 10);
        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldNotBeEmpty();
    }

    [Fact]
    public void 속도_특성은_다른_산업에는_붙지_않는다()
    {
        var (user, _) = UserWith(workshopLevel: 1);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Mining, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.TryLearnTrait(ExcavationSpeed, Base);

        slot.CurrentWorkSpeed.ShouldBe(before);
    }

    // ─────────────────────── 산출량 ───────────────────────

    [Fact]
    public void 산출량은_공통과_그_산업을_더한다()
    {
        // 공통 20% × 1 + 발굴 30% × 1 = 50%. 채굴은 공통만.
        var (user, _) = UserWith(workshopLevel: 3);

        user.TryLearnTrait(CommonYield, Base);
        user.TryLearnTrait(ExcavationYield, Base);

        user.GetTraitYieldAdd(IndustryType.Excavation).ShouldBe(500);
        user.GetTraitYieldAdd(IndustryType.Mining).ShouldBe(200);
    }

    [Fact]
    public void 산출량이_100퍼센트면_판정마다_자원이_2개씩_나온다()
    {
        // 공통 20%×2 + 발굴 30%×2 = 100% → 소수부가 없어 난수와 무관하다. 5분 = 10판정 → 20개.
        var (user, _) = UserWith(workshopLevel: 6);
        user.TryLearnTrait(CommonYield, Base);
        user.TryLearnTrait(CommonYield, Base);
        user.TryLearnTrait(ExcavationYield, Base);
        user.TryLearnTrait(ExcavationYield, Base);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Excavation, CharacterId, Base) });

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

    // ─────────────────────── 거절 경로 — 레벨이 움직이면 안 된다 ───────────────────────

    [Fact]
    public void 없는_특성은_InvalidUserTraitTID다()
    {
        var (user, b) = UserWith(workshopLevel: 2);

        user.TryLearnTrait(9999, Base);

        Response(b).Result.ShouldBe(EResultCode.InvalidUserTraitTID);
        user.GetBuildingPoints(TestBuildings.Workshop).ShouldBe(2);
    }

    [Fact]
    public void 포인트가_모자라면_NotEnoughTraitPoint고_레벨은_그대로다()
    {
        var (user, b) = UserWith(workshopLevel: 1);

        user.TryLearnTrait(ExcavationYield, Base);   // 비용 2

        Response(b).Result.ShouldBe(EResultCode.NotEnoughTraitPoint);
        user.GetTraitLevel(ExcavationYield).ShouldBe(0);
        user.GetBuildingPoints(TestBuildings.Workshop).ShouldBe(1);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 다른_건물의_레벨은_이_트리의_포인트가_아니다()
    {
        var (user, b) = UserWith(workshopLevel: 0, officeLevel: 5);

        user.TryLearnTrait(ExcavationSpeed, Base);

        Response(b).Result.ShouldBe(EResultCode.NotEnoughTraitPoint);
    }

    [Fact]
    public void 앞_노드를_최대까지_찍지_않으면_TraitPrerequisite다()
    {
        // 발굴 속도 Lv1/2 — 포인트는 남아도 다음 노드는 닫혀 있다.
        var (user, b) = UserWith(workshopLevel: 5);
        user.TryLearnTrait(ExcavationSpeed, Base);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();

        user.TryLearnTrait(GatedYield, Base);

        Response(b).Result.ShouldBe(EResultCode.TraitPrerequisite);
        user.GetTraitLevel(GatedYield).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 앞_노드를_최대까지_찍으면_다음_노드가_열린다()
    {
        var (user, b) = UserWith(workshopLevel: 5);
        user.TryLearnTrait(ExcavationSpeed, Base);
        user.TryLearnTrait(ExcavationSpeed, Base);
        b.Channel.Sent.Clear();

        user.TryLearnTrait(GatedYield, Base);

        Response(b).Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 노드는_계정_레벨로_잠그지_않는다()
    {
        // 계정 Lv1이어도 작업장 포인트만 있으면 개척 Lv3까지 찍는다.
        var (user, b) = UserWith(workshopLevel: 2);

        user.TryLearnTrait(ExcavationPioneer, Base);
        user.TryLearnTrait(ExcavationPioneer, Base);

        user.GetTraitLevel(ExcavationPioneer).ShouldBe(3);
        b.Channel.SentOf<S_UserTraitLearnResponse>().ShouldAllBe(r => r.Result == EResultCode.Ok);
    }

    [Fact]
    public void 최대_레벨이면_TraitMaxLevel이다()
    {
        var (user, b) = UserWith(workshopLevel: 3);
        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = ExcavationSpeed, level = 2 } });

        user.TryLearnTrait(ExcavationSpeed, Base);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.TraitMaxLevel);
        res.Level.ShouldBe(2);
        user.GetBuildingPoints(TestBuildings.Workshop).ShouldBe(1);   // Lv3 − 2
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
