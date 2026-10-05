using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 특성 레벨 치트(<c>SetTraitLevel</c> · T-115) — 계정 레벨 조건·포인트 없이 특성 레벨을 정한다.
/// 레벨형 특성(T-108) 이후 <c>Unlock</c> 치트로는 특성이 오르지 않아, 이것이 없으면 산업 Lv5 확인에 계정 Lv50이 필요하다.
/// </summary>
public class UserCheatTraitTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    // 테스트 전용 특성 — 엑셀 값에 기대지 않는다.
    //   102 낚시 개척: 기본 Lv1 · 최대 Lv3 · Lv2 계정 5 · Lv3 계정 15
    //   202 낚시 속도: 기본 Lv0 · 최대 Lv2 · 레벨당 +10%
    //   302 낚시 산출량: 기본 Lv0 · 최대 Lv2 · 레벨당 +100%
    private const int FishingPioneer = 102;
    private const int FishingSpeed   = 202;
    private const int FishingYield   = 302;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    public UserCheatTraitTest() => GameTableFixture.EnsureLoaded();

    private static UserTraitTableRow Trait(int tid, UserTraitEffect effect, int value, int baseLevel, int maxLevel)
        => new()
        {
            UserTraitTID = tid, Name = $"특성 {tid}", TraitPoint = 1, EffectType = effect,
            Industry = IndustryType.Fishing, EffectValue = value, BaseLevel = baseLevel, MaxLevel = maxLevel,
        };

    private static UserTraitLevelTableRow Level(int tid, int level, int accountLevel = 0)
        => new() { UserTraitLevelTID = tid * 100 + level, UserTraitTID = tid, Level = level, AccountLevel = accountLevel };

    /// <summary>계정 Lv1 · 특성 포인트 0 — 정상 경로로는 아무 특성도 못 올린다.</summary>
    private static (User User, TestUserBuilder B) Admin()
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Traits.Load(
            new[]
            {
                Trait(FishingPioneer, UserTraitEffect.IndustryUnlock, 0, baseLevel: 1, maxLevel: 3),
                Trait(FishingSpeed, UserTraitEffect.SpeedAdd, 100, baseLevel: 0, maxLevel: 2),
                Trait(FishingYield, UserTraitEffect.YieldAdd, 1000, baseLevel: 0, maxLevel: 2),
            },
            new[]
            {
                Level(FishingPioneer, 2, accountLevel: 5), Level(FishingPioneer, 3, accountLevel: 15),
                Level(FishingSpeed, 1), Level(FishingSpeed, 2),
                Level(FishingYield, 1), Level(FishingYield, 2),
            });

        var user = b.Build();
        user.AdminLevel = 1;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        user.LoadAccount(new AccountRow { level = 1, exp = 0, trait_point = 0 });

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static C_CheatRequest Req(long traitTid, long level)
        => new() { Command = ECheatCommand.SetTraitLevel, Arg1 = traitTid, Arg2 = level };

    private static S_CheatResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem();

    [Fact]
    public void 계정_레벨과_포인트가_모자라도_특성_레벨을_정한다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(FishingPioneer, 3), Base);   // Lv3는 계정 15가 조건이다

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.GetTraitLevel(FishingPioneer).ShouldBe(3);
        user.TraitPoint.ShouldBe(0);
    }

    [Fact]
    public void 최대_레벨을_넘으면_최대로_자른다()
    {
        var (user, _) = Admin();

        user.ExecuteCheat(Req(FishingPioneer, 99), Base);

        user.GetTraitLevel(FishingPioneer).ShouldBe(3);
    }

    [Fact]
    public void 기본_레벨까지_내리면_올린_산업_레벨이_다시_잠긴다()
    {
        var (user, _) = Admin();
        user.ExecuteCheat(Req(FishingPioneer, 3), Base);

        user.ExecuteCheat(Req(FishingPioneer, 0), Base);   // 기본 Lv1 아래는 기본으로

        user.GetTraitLevel(FishingPioneer).ShouldBe(1);
        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 2).ShouldBeFalse();
    }

    [Fact]
    public void TID가_0이면_전_특성을_각자의_최대_레벨까지_정한다()
    {
        var (user, _) = Admin();

        user.ExecuteCheat(Req(0, 99), Base);

        user.GetTraitLevel(FishingPioneer).ShouldBe(3);
        user.GetTraitLevel(FishingSpeed).ShouldBe(2);
    }

    [Theory]
    [InlineData(9999, 1)]           // 없는 특성
    [InlineData(-1, 1)]             // 음수 TID
    [InlineData(FishingSpeed, -1)]  // 음수 레벨
    public void 잘못된_인자는_InvalidCheatArgs고_아무것도_바꾸지_않는다(long traitTid, long level)
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(traitTid, level), Base);

        Response(b).Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 속도_특성을_바꾸면_슬롯_속도를_다시_매긴다()
    {
        // 기본 속도 × (1 + 10% × 2레벨)
        var (user, b) = Admin();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.ExecuteCheat(Req(FishingSpeed, 2), Base);

        slot.CurrentWorkSpeed.ShouldBe(before * 12 / 10);
    }

    [Fact]
    public void 산출량_특성을_바꾸기_전까지_쌓인_판정은_옛_산출량으로_정산된다()
    {
        // 5분 = 10판정 × 산출량 100% = 10개. 바꾼 뒤에 정산하면 +100%가 소급돼 20개가 된다.
        var (user, _) = Admin();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });

        user.ExecuteCheat(Req(FishingYield, 1), Base.AddMinutes(5));

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(10);
    }

    [Fact]
    public void 바꾼_뒤_특성_목록을_다시_보낸다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(FishingPioneer, 3), Base);

        var info = b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldHaveSingleItem();
        info.UserTraitTID.ShouldBe(FishingPioneer);
        info.Level.ShouldBe(3);
    }

    [Fact]
    public void 기본_레벨로_내린_특성은_다시_보낸_목록에서_빠진다()
    {
        // 목록에 없는 특성은 기본 레벨이다 — 클라는 목록을 통째로 갈아 끼운다.
        var (user, b) = Admin();
        user.ExecuteCheat(Req(FishingPioneer, 3), Base);
        b.Channel.Sent.Clear();

        user.ExecuteCheat(Req(FishingPioneer, 1), Base);

        b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldBeEmpty();
    }

    [Fact]
    public void 내린_레벨도_저장한다()
    {
        // 저장하지 않으면 다음 로그인에 올렸던 레벨로 되살아난다.
        var (user, b) = Admin();
        user.ExecuteCheat(Req(FishingPioneer, 3), Base);
        b.DB.Posted.Clear();

        user.ExecuteCheat(Req(FishingPioneer, 1), Base);

        b.DB.PostedOf<SaveUserTraitRepository>().ShouldHaveSingleItem().Level.ShouldBe(1);
    }

    [Fact]
    public void 기본_레벨로_저장된_특성은_로그인_목록에_싣지_않는다()
    {
        // 치트로 내리면 기본 레벨 행이 DB에 남는다. 다음 로그인 목록도 내리기 직후와 같아야 한다.
        var (user, b) = Admin();

        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = FishingPioneer, level = 1 } });
        user.SendTraitList();

        b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldBeEmpty();
    }
}
