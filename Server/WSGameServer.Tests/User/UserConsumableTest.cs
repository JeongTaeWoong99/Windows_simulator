using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 소모품 사용(T-195 · 아이템 기획 4.4 「사용 규칙」) — 비약·영약·묘약·지도·나침반.
/// 효과 값은 표(<c>ItemUseTable</c>)가 정하므로 여기서는 테스트 값을 넣어 고정한다.
/// 틀리면 아이템만 사라지고 효과가 없거나, 실패한 사용이 아이템을 먹거나, 버프가 끝나지 않는다.
/// </summary>
public class UserConsumableTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  Potion     = 100041;   // 성장의 비약 — 경험치 5
    private const int  SpeedElix  = 100044;   // 신속의 영약 — 속도 +300‰ · 3회
    private const int  YieldElix  = 100045;   // 풍요의 영약 — 산출 +1000‰ · 3회
    private const int  Forget     = 100046;   // 망각의 묘약
    private const int  Map        = 100034;   // 은빛 보물 지도 — 판정 2번 · 슬롯 2개
    private const int  Compass    = 100036;   // 낡은 나침반 — 상자 +1000‰ · 3회 · 슬롯 1개
    private const int  GoldCompass = 100037;  // 황금 나침반 — 상자·유물 +1000‰ · 3회 · 슬롯 1개
    private const int  BoxTid     = 100019;   // 공통 보상으로 나오는 상자
    private const long CharacterId = 500;
    private const int  WorkingSlot = 0;
    private const int  IdleSlot    = 1;

    public UserConsumableTest() => GameTableFixture.EnsureLoaded();

    private static readonly ItemUseTableRow[] Uses =
    {
        new() { ItemTID = Potion,      UseEffect = ItemUseEffect.CharacterExp,       EffectValue = 5 },
        new() { ItemTID = SpeedElix,   UseEffect = ItemUseEffect.SlotWorkSpeed,      EffectValue = 300,  EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = YieldElix,   UseEffect = ItemUseEffect.SlotYield,          EffectValue = 1000, EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = Forget,      UseEffect = ItemUseEffect.TraitReset },
        new() { ItemTID = Map,         UseEffect = ItemUseEffect.InstantJudge,       EffectValue = 2, SlotCount = 2 },
        new() { ItemTID = Compass,     UseEffect = ItemUseEffect.SlotBoxChance,      EffectValue = 1000, EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = GoldCompass, UseEffect = ItemUseEffect.SlotBoxRelicChance, EffectValue = 1000, EffectCount = 3, SlotCount = 1 },
    };

    /// <summary>발굴 슬롯 0(캐릭터 배치) · 빈 슬롯 1. 캐릭터 곡선 Lv2 10 · Lv3 12(만렙). 소모품을 10개씩 가진다.</summary>
    private static (User User, TestUserBuilder B) NewUser(int level = 1, int exp = 0, int boxChancePerMillion = 0)
    {
        var b = new TestUserBuilder().WithExcavationDrops();
        b.ItemUses.Load(Uses);
        b.Growth.Load(new[]
        {
            new CharacterLevelTableRow { CharacterLevelTID = 1, RequiredExp = 0 },
            new CharacterLevelTableRow { CharacterLevelTID = 2, RequiredExp = 10 },
            new CharacterLevelTableRow { CharacterLevelTID = 3, RequiredExp = 12 },
        });
        if (boxChancePerMillion > 0)
        {
            b.CommonRewards.Load(
                new[] { new CommonRewardTableRow { CommonRewardTID = 1, IndustryLevel = 1, ItemTID = BoxTid, Count = 1, ChancePerMillion = boxChancePerMillion } },
                Array.Empty<CommonRewardOverrideTableRow>());
        }

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharacterId, character_tid = 1001, level = level, exp = exp } });
        user.LoadInventory(new[] { Potion, SpeedElix, YieldElix, Forget, Map, Compass, GoldCompass }
            .Select((tid, i) => new InventoryRow { item_id = tid, count = 10, slot = i })
            .ToList());
        user.WorkStation.Load(new[]
        {
            new WorkStationSlot(WorkingSlot, IndustryType.Excavation, CharacterId, Base),
            new WorkStationSlot(IdleSlot, IndustryType.None, 0, Base),
        });

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_ItemUseResponse Use(User user, TestUserBuilder b, int itemTid, int count = 1, long character = 0, params int[] slots)
    {
        user.TryUseConsumable(itemTid, count, character, slots, Base).ShouldBeTrue();
        return b.Channel.SentOf<S_ItemUseResponse>().Last();
    }

    private static WorkStationSlot Slot(User user, int index)
    {
        user.WorkStation.TryGet(index, out var slot).ShouldBeTrue();
        return slot;
    }

    // ───────────────────────── 공통 ─────────────────────────

    [Fact]
    public void 표에_없는_아이템은_소모품이_아니라_상자_경로로_넘긴다()
    {
        var (user, _) = NewUser();

        user.TryUseConsumable(BoxTid, 1, 0, null, Base).ShouldBeFalse();
    }

    [Fact]
    public void 가진_것보다_많이_쓰면_거절하고_아무것도_빠지지_않는다()
    {
        var (user, b) = NewUser();

        Use(user, b, Potion, 11, CharacterId).Result.ShouldBe(EResultCode.NotEnoughItem);

        user.GetItemCount(Potion).ShouldBe(10);
    }

    // ───────────────────────── 성장의 비약 ─────────────────────────

    [Fact]
    public void 비약은_개수만큼_캐릭터_경험치를_준다()
    {
        var (user, b) = NewUser();

        // 5 × 3 = 15 → Lv2(10) 남은 5
        Use(user, b, Potion, 3, CharacterId).Result.ShouldBe(EResultCode.Ok);

        user.TryGetCharacter(CharacterId, out var character);
        character.Level.ShouldBe(2);
        character.Exp.ShouldBe(5);
    }

    [Fact]
    public void 비약은_계정_경험치를_올리지_않는다()
    {
        var (user, b) = NewUser();

        Use(user, b, Potion, 3, CharacterId);

        user.AccountExp.ShouldBe(0);
    }

    [Fact]
    public void 만렙을_넘기는_개수면_필요한_만큼만_빠진다()
    {
        var (user, b) = NewUser();

        // 만렙까지 10 + 12 = 22 → 5개(25)면 닿는다. 10개를 요청해도 5개만 빠진다.
        Use(user, b, Potion, 10, CharacterId).UsedCount.ShouldBe(5);

        user.GetItemCount(Potion).ShouldBe(5);
    }

    [Fact]
    public void 만렙_캐릭터에는_비약을_쓸_수_없다()
    {
        var (user, b) = NewUser(level: 3);

        Use(user, b, Potion, 1, CharacterId).Result.ShouldBe(EResultCode.CharacterMaxLevel);

        user.GetItemCount(Potion).ShouldBe(10);
    }

    [Fact]
    public void 없는_캐릭터에는_비약을_쓸_수_없다()
    {
        var (user, b) = NewUser();

        Use(user, b, Potion, 1, 999).Result.ShouldBe(EResultCode.InvalidUseTarget);
    }

    // ───────────────────────── 영약 · 나침반 (슬롯 버프) ─────────────────────────

    [Fact]
    public void 영약은_슬롯에_개수만큼의_횟수로_걸린다()
    {
        var (user, b) = NewUser();

        // 3회 × 2개 = 6
        Use(user, b, SpeedElix, 2, slots: WorkingSlot).Result.ShouldBe(EResultCode.Ok);

        Slot(user, WorkingSlot).Buffs[SpeedElix].ShouldBe(6);
    }

    [Fact]
    public void 같은_영약을_다시_쓰면_남은_횟수에_합산된다()
    {
        var (user, b) = NewUser();

        Use(user, b, SpeedElix, 1, slots: WorkingSlot);
        Use(user, b, SpeedElix, 1, slots: WorkingSlot);

        Slot(user, WorkingSlot).Buffs[SpeedElix].ShouldBe(6);
    }

    [Fact]
    public void 캐릭터가_없는_슬롯에도_영약을_건다()
    {
        var (user, b) = NewUser();

        Use(user, b, YieldElix, 1, slots: IdleSlot).Result.ShouldBe(EResultCode.Ok);

        Slot(user, IdleSlot).Buffs[YieldElix].ShouldBe(3);
    }

    [Fact]
    public void 신속의_영약은_슬롯_속도_내역에_가산으로_붙는다()
    {
        var (user, b) = NewUser();

        Use(user, b, SpeedElix, 1, slots: WorkingSlot);

        Slot(user, WorkingSlot).SpeedBreakdown.BuffAddPermille.ShouldBe(300);
    }

    [Fact]
    public void 버프를_걸면_슬롯을_밀어_주고_남은_횟수를_저장한다()
    {
        var (user, b) = NewUser();

        Use(user, b, YieldElix, 1, slots: WorkingSlot);

        var buff = b.Channel.SentOf<S_WorkStationSlotSyncResponse>().Last().Slot!.Buffs!.ShouldHaveSingleItem();
        buff.ItemTID.ShouldBe(YieldElix);
        buff.Remaining.ShouldBe(3);
        b.DB.PostedOf<SaveSlotBuffRepository>().ShouldHaveSingleItem().Changes
            .ShouldBe(new[] { new SlotBuffChange(WorkingSlot, YieldElix, 3) });
    }

    [Fact]
    public void 버프는_판정마다_하나씩_줄고_다_쓰면_사라진다()
    {
        var (user, b) = NewUser();
        Use(user, b, YieldElix, 1, slots: WorkingSlot);
        b.DB.Posted.Clear();

        Slot(user, WorkingSlot).AdvanceJudges(2);
        user.SettleWorkStation(Base);
        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(1);

        Slot(user, WorkingSlot).AdvanceJudges(5);
        user.SettleWorkStation(Base);
        Slot(user, WorkingSlot).Buffs.ShouldNotContainKey(YieldElix);
        b.DB.PostedOf<SaveSlotBuffRepository>().Last().Changes
            .ShouldBe(new[] { new SlotBuffChange(WorkingSlot, YieldElix, 0) });
    }

    [Fact]
    public void 신속의_영약이_끝나면_속도_가산이_빠진다()
    {
        var (user, b) = NewUser();
        Use(user, b, SpeedElix, 1, slots: WorkingSlot);

        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);

        Slot(user, WorkingSlot).SpeedBreakdown.BuffAddPermille.ShouldBe(0);
    }

    [Fact]
    public void 풍요의_영약은_버프가_남은_판정의_자원만_늘린다()
    {
        var (user, b) = NewUser();
        Use(user, b, YieldElix, 1, slots: WorkingSlot);

        // 판정 5번: 앞 3번은 +1000‰(2개씩) · 뒤 2번은 1개씩 → 3 × 2 + 2 = 8
        Slot(user, WorkingSlot).AdvanceJudges(5);
        user.SettleWorkStation(Base);

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(8);
    }

    [Fact]
    public void 나침반이_걸린_판정은_상자_확률이_가산된다()
    {
        // 상자 50% + 나침반 +1000‰ = 100% → 버프가 남은 판정 3번은 반드시 상자가 나온다
        var (user, b) = NewUser(boxChancePerMillion: 500_000);
        Use(user, b, GoldCompass, 1, slots: WorkingSlot);

        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);

        user.GetItemCount(BoxTid).ShouldBe(3);
    }

    [Fact]
    public void 슬롯_수를_넘겨_고르면_거절한다()
    {
        var (user, b) = NewUser();

        Use(user, b, Compass, 1, slots: new[] { WorkingSlot, IdleSlot }).Result.ShouldBe(EResultCode.InvalidUseTarget);

        user.GetItemCount(Compass).ShouldBe(10);
    }

    [Fact]
    public void 슬롯을_고르지_않으면_거절한다()
    {
        var (user, b) = NewUser();

        Use(user, b, Compass, 1).Result.ShouldBe(EResultCode.InvalidUseTarget);
    }

    [Fact]
    public void 없는_슬롯은_거절한다()
    {
        var (user, b) = NewUser();

        Use(user, b, Compass, 1, slots: 7).Result.ShouldBe(EResultCode.InvalidUseTarget);
    }

    // ───────────────────────── 보물 지도 ─────────────────────────

    [Fact]
    public void 지도는_고른_슬롯의_판정을_즉시_끝낸다()
    {
        var (user, b) = NewUser();

        // 판정 2번 × 3개 = 6번
        Use(user, b, Map, 3, slots: WorkingSlot).Result.ShouldBe(EResultCode.Ok);

        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount.ShouldBe(6);
    }

    [Fact]
    public void 돌지_않는_슬롯에는_지도를_쓸_수_없다()
    {
        var (user, b) = NewUser();

        Use(user, b, Map, 1, slots: new[] { WorkingSlot, IdleSlot }).Result.ShouldBe(EResultCode.UseTargetIdle);

        user.GetItemCount(Map).ShouldBe(10);
        b.Channel.SentOf<S_GatherResultResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 같은_슬롯을_두_번_고르면_거절한다()
    {
        var (user, b) = NewUser();

        Use(user, b, Map, 1, slots: new[] { WorkingSlot, WorkingSlot }).Result.ShouldBe(EResultCode.InvalidUseTarget);
    }

    // ───────────────────────── 망각의 묘약 ─────────────────────────

    [Fact]
    public void 묘약은_올린_노드를_전부_기본_레벨로_되돌린다()
    {
        var (user, b) = NewUser();
        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = 101, level = 3 } });

        Use(user, b, Forget).Result.ShouldBe(EResultCode.Ok);

        // 101(농사 개척)의 기본 레벨은 1
        user.GetTraitLevel(101).ShouldBe(1);
    }

    [Fact]
    public void 올린_노드가_없으면_묘약을_쓸_수_없다()
    {
        var (user, b) = NewUser();

        Use(user, b, Forget).Result.ShouldBe(EResultCode.NothingToReset);

        user.GetItemCount(Forget).ShouldBe(10);
    }

    [Fact]
    public void 묘약은_한_번에_하나만_쓴다()
    {
        var (user, b) = NewUser();
        user.LoadTraits(new[] { new UserTraitRow { user_trait_tid = 101, level = 3 } });

        Use(user, b, Forget, 2).Result.ShouldBe(EResultCode.InvalidUseCount);
    }
}
