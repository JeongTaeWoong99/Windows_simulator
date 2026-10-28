using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 특성 레벨 치트(<c>SetTraitLevel</c> · T-115) — 선행·건물 포인트 없이 특성 레벨을 정한다.
/// 레벨형 특성(T-108) 이후 <c>Unlock</c> 치트로는 특성이 오르지 않아, 이것이 없으면 산업 Lv5 확인에 계정 Lv50이 필요하다.
/// </summary>
public class UserCheatTraitTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    // 테스트 전용 특성 — 엑셀 값에 기대지 않는다.
    //   102 발굴 개척: 기본 Lv1 · 최대 Lv3
    //   202 발굴 속도: 기본 Lv0 · 최대 Lv2 · 레벨당 +10%
    //   302 발굴 산출량: 기본 Lv0 · 최대 Lv2 · 레벨당 +100%
    private const int ExcavationPioneer = 102;
    private const int ExcavationSpeed   = 202;
    private const int ExcavationYield   = 302;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    public UserCheatTraitTest() => GameTableFixture.EnsureLoaded();

    private static UserTraitTableRow Trait(int tid, UserTraitEffect effect, int value, int baseLevel, int maxLevel)
        => new()
        {
            UserTraitTID = tid, Name = $"특성 {tid}", TraitPoint = 1, EffectType = effect,
            Industry = IndustryType.Excavation, EffectValue = value, BaseLevel = baseLevel, MaxLevel = maxLevel,
            BuildingTID = TestBuildings.Workshop,
        };

    /// <summary>작업장 Lv0(포인트 0) — 정상 경로로는 아무 특성도 못 올린다.</summary>
    private static (User User, TestUserBuilder B) Admin()
    {
        var b = new TestUserBuilder().WithExcavationDrops();
        b.Traits.Load(
            new[]
            {
                Trait(ExcavationPioneer, UserTraitEffect.IndustryUnlock, 0, baseLevel: 1, maxLevel: 3),
                Trait(ExcavationSpeed, UserTraitEffect.SpeedAdd, 100, baseLevel: 0, maxLevel: 2),
                Trait(ExcavationYield, UserTraitEffect.YieldAdd, 1000, baseLevel: 0, maxLevel: 2),
            },
            TestBuildings.TwoFree().Buildings, TestBuildings.TwoFree().Levels);

        var user = b.Build();
        user.AdminLevel = 1;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static C_CheatRequest Req(long traitTid, long level)
        => new() { Command = ECheatCommand.SetTraitLevel, Arg1 = traitTid, Arg2 = level };

    private static S_CheatResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem();

    [Fact]
    public void 건물_포인트가_없어도_특성_레벨을_정한다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ExcavationPioneer, 3), Base);

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.GetTraitLevel(ExcavationPioneer).ShouldBe(3);
    }

    [Fact]
    public void 최대_레벨을_넘으면_최대로_자른다()
    {
        var (user, _) = Admin();

        user.ExecuteCheat(Req(ExcavationPioneer, 99), Base);

        user.GetTraitLevel(ExcavationPioneer).ShouldBe(3);
    }

    [Fact]
    public void 기본_레벨까지_내리면_올린_산업_레벨이_다시_잠긴다()
    {
        var (user, _) = Admin();
        user.ExecuteCheat(Req(ExcavationPioneer, 3), Base);

        user.ExecuteCheat(Req(ExcavationPioneer, 0), Base);   // 기본 Lv1 아래는 기본으로

        user.GetTraitLevel(ExcavationPioneer).ShouldBe(1);
        user.IsIndustryLevelUnlocked(IndustryType.Excavation, 2).ShouldBeFalse();
    }

    [Fact]
    public void TID가_0이면_전_특성을_각자의_최대_레벨까지_정한다()
    {
        var (user, _) = Admin();

        user.ExecuteCheat(Req(0, 99), Base);

        user.GetTraitLevel(ExcavationPioneer).ShouldBe(3);
        user.GetTraitLevel(ExcavationSpeed).ShouldBe(2);
    }

    [Theory]
    [InlineData(9999, 1)]           // 없는 특성
    [InlineData(-1, 1)]             // 음수 TID
    [InlineData(ExcavationSpeed, -1)]  // 음수 레벨
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
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Excavation, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.ExecuteCheat(Req(ExcavationSpeed, 2), Base);

        slot.CurrentWorkSpeed.ShouldBe(before * 12 / 10);
    }

    [Fact]
    public void 산출량_특성을_바꾸기_전까지_쌓인_판정은_옛_산출량으로_정산된다()
    {
        // 5분 = 10판정 × 산출량 100% = 10개. 바꾼 뒤에 정산하면 +100%가 소급돼 20개가 된다.
        var (user, _) = Admin();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Excavation, CharacterId, Base) });

        user.ExecuteCheat(Req(ExcavationYield, 1), Base.AddMinutes(5));

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(10);
    }

    [Fact]
    public void 바꾼_뒤_특성_목록을_다시_보낸다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ExcavationPioneer, 3), Base);

        var info = b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldHaveSingleItem();
        info.UserTraitTID.ShouldBe(ExcavationPioneer);
        info.Level.ShouldBe(3);
    }

    [Fact]
    public void 기본_레벨로_내린_특성은_다시_보낸_목록에서_빠진다()
    {
        // 목록에 없는 특성은 기본 레벨이다 — 클라는 목록을 통째로 갈아 끼운다.
        var (user, b) = Admin();
        user.ExecuteCheat(Req(ExcavationPioneer, 3), Base);
        b.Channel.Sent.Clear();

        user.ExecuteCheat(Req(ExcavationPioneer, 1), Base);

        b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldBeEmpty();
    }

    [Fact]
    public void 내린_레벨도_저장한다()
    {
        // 저장하지 않으면 다음 로그인에 올렸던 레벨로 되살아난다.
        var (user, b) = Admin();
        user.ExecuteCheat(Req(ExcavationPioneer, 3), Base);
        b.DB.Posted.Clear();

        user.ExecuteCheat(Req(ExcavationPioneer, 1), Base);

        b.DB.PostedOf<SaveUserTraitRepository>().ShouldHaveSingleItem().Level.ShouldBe(1);
    }

    [Fact]
    public void 기본_레벨로_저장된_특성은_로그인_목록에_싣지_않는다()
    {
        // 치트로 내리면 기본 레벨 행이 DB에 남는다. 다음 로그인 목록도 내리기 직후와 같아야 한다.
        var (user, b) = Admin();

        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = ExcavationPioneer, level = 1 } });
        user.SendTraitList();

        b.Channel.SentOf<S_UserTraitListResponse>().ShouldHaveSingleItem().Traits.ShouldBeEmpty();
    }
}
