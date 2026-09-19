using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 특성 트리 검증 — 노드 하나 = <c>UnlockTable</c> 행 하나. 조건은 그 해금 행(계정 레벨·선행·골드) + 특성 포인트다.
/// <b>포인트는 조건을 전부 통과한 뒤에만 빠진다</b>(해금 2.1과 같은 순서). 거절 경로에서 포인트가 움직이면 트리가 샌다.
/// </summary>
public class UserTraitTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    // 테스트 전용 트리 — 엑셀 값에 기대지 않는다.
    //   2202 낚시 Lv2    : 포인트 1 · 계정 Lv5
    //   2203 낚시 Lv3    : 포인트 1 · 계정 Lv5 · 선행 2202
    //   3201 낚시 속도 1 : 포인트 1 · +10%
    //   3202 낚시 속도 2 : 포인트 2 · +10% · 선행 3201
    private const int FishingLv2   = 2202;
    private const int FishingLv3   = 2203;
    private const int FishingSpeed1 = 3201;
    private const int FishingSpeed2 = 3202;

    public UserTraitTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWith(int level, int traitPoint)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Unlocks.Load(
            new[]
            {
                new UnlockTableRow { UnlockTID = FishingLv2,    Name = "낚시 Lv2", AccountLevel = 5 },
                new UnlockTableRow { UnlockTID = FishingLv3,    Name = "낚시 Lv3", AccountLevel = 5, RequiredUnlockTIDs = new[] { FishingLv2 } },
                new UnlockTableRow { UnlockTID = FishingSpeed1, Name = "낚시 속도 10%" },
                new UnlockTableRow { UnlockTID = FishingSpeed2, Name = "낚시 속도 20%", RequiredUnlockTIDs = new[] { FishingSpeed1 } },
            },
            new[]
            {
                new WorkSlotTableRow { WorkSlotTID = 0, UnlockTID = 0 },
            });
        b.Traits.Load(new[]
        {
            new UserTraitTableRow { UserTraitTID = FishingLv2,    Name = "낚시 Lv2", TraitPoint = 1, Industry = IndustryType.Fishing },
            new UserTraitTableRow { UserTraitTID = FishingLv3,    Name = "낚시 Lv3", TraitPoint = 1, Industry = IndustryType.Fishing },
            new UserTraitTableRow
            {
                UserTraitTID = FishingSpeed1, Name = "낚시 속도 10%", TraitPoint = 1,
                EffectType = UserTraitEffect.SpeedAdd, Industry = IndustryType.Fishing, EffectValue = 100,
            },
            new UserTraitTableRow
            {
                UserTraitTID = FishingSpeed2, Name = "낚시 속도 20%", TraitPoint = 2,
                EffectType = UserTraitEffect.SpeedAdd, Industry = IndustryType.Fishing, EffectValue = 100,
            },
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

    // ─────────────────────── 성공 경로 ───────────────────────

    [Fact]
    public void 조건을_채우면_노드를_찍고_해금이_열리고_포인트가_빠진다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 2);

        user.TryLearnTrait(FishingLv2, Base);

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.IsUnlocked(FishingLv2).ShouldBeTrue();
        user.TraitPoint.ShouldBe(1);
        b.DB.PostedOf<SaveUnlockRepository>().ShouldHaveSingleItem().UnlockTid.ShouldBe(FishingLv2);
        b.DB.PostedOf<SaveAccountRepository>().ShouldNotBeEmpty();
        b.Channel.SentOf<S_AccountLevelResponse>().Last().TraitPoint.ShouldBe(1);
    }

    [Fact]
    public void 산업_레벨_노드를_찍으면_그_레벨로_배치할_수_있다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 1);
        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow { IndustryLevelTID = 201, IndustryType = IndustryType.Fishing, Level = 1, Name = "개울", RequiredScore = 30_000 },
            new IndustryLevelTableRow { IndustryLevelTID = 202, IndustryType = IndustryType.Fishing, Level = 2, Name = "저수지", RequiredScore = 30_000, UnlockTID = FishingLv2 },
        });
        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 2).ShouldBeFalse();

        user.TryLearnTrait(FishingLv2, Base);

        user.IsIndustryLevelUnlocked(IndustryType.Fishing, 2).ShouldBeTrue();
    }

    [Fact]
    public void 속도_특성은_그_산업_슬롯_속도에_가산된다()
    {
        // 적성 1 기본 속도 1000 × (1 + 10% + 10%) = 1200
        var (user, b) = UserWith(level: 1, traitPoint: 3);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        var before = slot.CurrentWorkSpeed;

        user.TryLearnTrait(FishingSpeed1, Base);
        user.TryLearnTrait(FishingSpeed2, Base);

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

        user.TryLearnTrait(FishingSpeed1, Base);

        slot.CurrentWorkSpeed.ShouldBe(before);
    }

    [Fact]
    public void 찍은_속도_특성은_로그인_때_해금_기록으로_되살아난다()
    {
        // 찍었다는 기록은 t_user_unlock 하나뿐이다 — 별도 특성 테이블이 없다.
        var (user, _) = UserWith(level: 1, traitPoint: 0);
        user.LoadUnlocks(new[] { new UserUnlockRow { unlock_tid = FishingSpeed1 } });

        user.GetTraitSpeedAdd(IndustryType.Fishing).ShouldBe(100);
        user.GetTraitSpeedAdd(IndustryType.Mining).ShouldBe(0);
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
    public void 포인트가_모자라면_NotEnoughTraitPoint다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 0);

        user.TryLearnTrait(FishingLv2, Base);

        Response(b).Result.ShouldBe(EResultCode.NotEnoughTraitPoint);
        user.IsUnlocked(FishingLv2).ShouldBeFalse();
    }

    [Fact]
    public void 계정_레벨이_모자라면_UnlockLocked고_포인트를_쓰지_않는다()
    {
        var (user, b) = UserWith(level: 4, traitPoint: 2);

        user.TryLearnTrait(FishingLv2, Base);

        Response(b).Result.ShouldBe(EResultCode.UnlockLocked);
        user.TraitPoint.ShouldBe(2);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 선행_노드가_없으면_UnlockLocked다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 2);

        user.TryLearnTrait(FishingLv3, Base);

        Response(b).Result.ShouldBe(EResultCode.UnlockLocked);
        user.TraitPoint.ShouldBe(2);
    }

    [Fact]
    public void 이미_찍은_노드는_AlreadyUnlocked다()
    {
        var (user, b) = UserWith(level: 5, traitPoint: 2);
        user.LoadUnlocks(new[] { new UserUnlockRow { unlock_tid = FishingLv2 } });

        user.TryLearnTrait(FishingLv2, Base);

        Response(b).Result.ShouldBe(EResultCode.AlreadyUnlocked);
        user.TraitPoint.ShouldBe(2);
    }

    [Fact]
    public void 특성_노드의_해금은_일반_해금_요청으로_열_수_없다()
    {
        // 포인트를 건너뛰는 뒷문 — C_UnlockRequest는 비용 컬럼(골드)만 보므로 막지 않으면 공짜로 열린다.
        var (user, b) = UserWith(level: 5, traitPoint: 2);

        user.TryUnlock(FishingLv2, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.TraitOnlyUnlock);
        user.IsUnlocked(FishingLv2).ShouldBeFalse();
    }
}
