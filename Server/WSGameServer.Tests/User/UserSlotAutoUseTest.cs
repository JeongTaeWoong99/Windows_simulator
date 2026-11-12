using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 작업슬롯 자동 사용 칸(이슈 #113) — 칸에 넣어 둔 영약을 같은 영약의 버프가 끝날 때 1개씩 꺼내 쓴다.
/// 틀리면 넣은 영약이 사라지거나 두 번 쓰이고, 쉬는 슬롯에서 영약이 줄거나, 꺼낸 영약이 자리가 없어 버려진다.
/// </summary>
public class UserSlotAutoUseTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  SpeedElix   = 100044;   // 신속의 영약 — 속도 +300‰ · 3회
    private const int  YieldElix   = 100045;   // 풍요의 영약 — 산출 +1000‰ · 3회 · MaxStack 9999(실제 표)
    private const int  Potion      = 100041;   // 성장의 비약 — 칸에 못 넣는다
    private const int  Compass     = 100036;   // 낡은 나침반 — 칸에 못 넣는다
    private const long CharacterId = 500;
    private const long SecondCharacterId = 501;
    private const int  WorkingSlot = 0;
    private const int  IdleSlot    = 1;

    public UserSlotAutoUseTest() => GameTableFixture.EnsureLoaded();

    private static readonly ItemUseTableRow[] Uses =
    {
        new() { ItemTID = Potion,    UseEffect = ItemUseEffect.CharacterExp,  EffectValue = 5 },
        new() { ItemTID = SpeedElix, UseEffect = ItemUseEffect.SlotWorkSpeed, EffectValue = 300,  EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = YieldElix, UseEffect = ItemUseEffect.SlotYield,     EffectValue = 1000, EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = Compass,   UseEffect = ItemUseEffect.SlotBoxChance, EffectValue = 1000, EffectCount = 3, SlotCount = 1 },
    };

    /// <summary>발굴 슬롯 0(캐릭터 배치) · 빈 슬롯 1. 영약·비약·나침반을 10개씩 가진다. extraRows는 그 뒤 칸에 더 싣는다.</summary>
    private static (User User, TestUserBuilder B) NewUser(params InventoryRow[] extraRows)
    {
        var b = new TestUserBuilder().WithExcavationDrops();
        b.ItemUses.Load(Uses);

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId,       character_tid = 1001, level = 1 },
            new CharacterRow { character_id = SecondCharacterId, character_tid = 1001, level = 1 },
        });
        user.LoadInventory(new[] { SpeedElix, YieldElix, Potion, Compass }
            .Select((tid, i) => new InventoryRow { item_id = tid, count = 10, slot = i })
            .Concat(extraRows)
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

    private static S_SlotAutoUseResponse Put(User user, TestUserBuilder b, int slot, int itemTid, int count)
    {
        user.PutSlotAutoUse(slot, itemTid, count, Base);
        return b.Channel.SentOf<S_SlotAutoUseResponse>().Last();
    }

    private static S_SlotAutoUseResponse Take(User user, TestUserBuilder b, int slot, int itemTid, int count)
    {
        user.TakeSlotAutoUse(slot, itemTid, count);
        return b.Channel.SentOf<S_SlotAutoUseResponse>().Last();
    }

    private static WorkStationSlot Slot(User user, int index)
    {
        user.WorkStation.TryGet(index, out var slot).ShouldBeTrue();
        return slot;
    }

    // ───────────────────────── 넣기 ─────────────────────────

    [Fact]
    public void 넣으면_보유에서_빠지고_칸에_쌓인다()
    {
        var (user, b) = NewUser();

        Put(user, b, IdleSlot, YieldElix, 4).Result.ShouldBe(EResultCode.Ok);

        user.GetItemCount(YieldElix).ShouldBe(6);
    }

    [Fact]
    public void 쉬는_슬롯에서는_칸의_영약을_쓰지_않는다()
    {
        var (user, b) = NewUser();

        Put(user, b, IdleSlot, YieldElix, 4);

        Slot(user, IdleSlot).AutoUse[YieldElix].ShouldBe(4);
    }

    [Fact]
    public void 돌고_있는_슬롯에_넣으면_버프가_없을_때_바로_하나를_쓴다()
    {
        var (user, b) = NewUser();

        Put(user, b, WorkingSlot, YieldElix, 4);

        // 4개 중 1개를 써서 3회 — 칸에는 3개
        Slot(user, WorkingSlot).AutoUse[YieldElix].ShouldBe(3);
        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(3);
    }

    [Fact]
    public void 신속의_영약을_넣으면_속도_가산이_바로_붙는다()
    {
        var (user, b) = NewUser();

        Put(user, b, WorkingSlot, SpeedElix, 1);

        Slot(user, WorkingSlot).SpeedBreakdown.BuffAddPermille.ShouldBe(300);
    }

    [Fact]
    public void 직접_쓴_횟수가_남아_있으면_칸의_영약은_기다린다()
    {
        var (user, b) = NewUser();
        user.TryUseConsumable(YieldElix, 1, 0, new[] { WorkingSlot }, Base);

        Put(user, b, WorkingSlot, YieldElix, 1);

        Slot(user, WorkingSlot).AutoUse[YieldElix].ShouldBe(1);
    }

    [Fact]
    public void 넣으면_칸_버프_인벤토리를_함께_저장한다()
    {
        var (user, b) = NewUser();

        Put(user, b, WorkingSlot, YieldElix, 4);

        var saved = b.DB.PostedOf<SaveSlotAutoUseRepository>().Single();
        saved.Changes.ShouldBe(new[] { new SlotAutoUseChange(WorkingSlot, YieldElix, 3) });
        saved.BuffChanges.ShouldBe(new[] { new SlotBuffChange(WorkingSlot, YieldElix, 3) });
        saved.ItemChanges.Single().Count.ShouldBe(6);
    }

    [Fact]
    public void 신속_풍요의_영약이_아니면_넣을_수_없다()
    {
        var (user, b) = NewUser();

        Put(user, b, IdleSlot, Compass, 1).Result.ShouldBe(EResultCode.NotAutoUsable);
        Put(user, b, IdleSlot, Potion, 1).Result.ShouldBe(EResultCode.NotAutoUsable);

        user.GetItemCount(Compass).ShouldBe(10);
    }

    [Fact]
    public void 가진_것보다_많이_넣으면_거절하고_아무것도_빠지지_않는다()
    {
        var (user, b) = NewUser();

        Put(user, b, IdleSlot, YieldElix, 11).Result.ShouldBe(EResultCode.NotEnoughItem);

        user.GetItemCount(YieldElix).ShouldBe(10);
    }

    [Fact]
    public void 칸은_MaxStack까지만_들어간다()
    {
        // 9989 + 10 = 9999개 보유. 9999개를 넣으면 한도가 차서 1개도 더 못 넣는다.
        var (user, b) = NewUser(new InventoryRow { item_id = YieldElix, count = 9989, slot = 10 });
        Put(user, b, IdleSlot, YieldElix, 9998).Result.ShouldBe(EResultCode.Ok);
        user.GainItem(YieldElix, 5);

        Put(user, b, IdleSlot, YieldElix, 2).Result.ShouldBe(EResultCode.AutoUseSlotFull);
    }

    [Fact]
    public void 없는_슬롯에는_넣을_수_없다()
    {
        var (user, b) = NewUser();

        Put(user, b, 99, YieldElix, 1).Result.ShouldBe(EResultCode.InvalidUseTarget);
    }

    [Fact]
    public void 개수가_0이면_거절한다()
    {
        var (user, b) = NewUser();

        Put(user, b, IdleSlot, YieldElix, 0).Result.ShouldBe(EResultCode.InvalidUseCount);
    }

    // ───────────────────────── 자동 사용 ─────────────────────────

    [Fact]
    public void 버프가_다_되면_칸에서_다음_영약을_꺼내_이어간다()
    {
        var (user, b) = NewUser();
        Put(user, b, WorkingSlot, YieldElix, 2);

        // 판정 5번 모두 +1000‰(2개씩) → 10. 이어 쓰지 않으면 3 × 2 + 2 = 8
        Slot(user, WorkingSlot).AdvanceJudges(5);
        user.SettleWorkStation(Base);

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(10);
    }

    [Fact]
    public void 칸을_다_쓰면_효과_없이_계속_돈다()
    {
        var (user, b) = NewUser();
        Put(user, b, WorkingSlot, YieldElix, 1);

        // 앞 3번만 +1000‰ → 3 × 2 + 2 = 8
        Slot(user, WorkingSlot).AdvanceJudges(5);
        user.SettleWorkStation(Base);

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(8);
    }

    [Fact]
    public void 직접_쓴_횟수가_끝나면_칸의_영약을_이어서_쓴다()
    {
        var (user, b) = NewUser();
        user.TryUseConsumable(YieldElix, 1, 0, new[] { WorkingSlot }, Base);
        Put(user, b, WorkingSlot, YieldElix, 1);

        // 직접 쓴 3회가 끝나는 판정 직후 칸의 1개를 꺼내 다시 3회
        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);

        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(3);
    }

    [Fact]
    public void 정산이_칸에서_꺼내면_칸과_버프를_함께_저장한다()
    {
        var (user, b) = NewUser();
        Put(user, b, WorkingSlot, YieldElix, 2);
        b.DB.Posted.Clear();

        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);

        var saved = b.DB.PostedOf<SaveSlotAutoUseRepository>().Single();
        saved.Changes.ShouldBe(new[] { new SlotAutoUseChange(WorkingSlot, YieldElix, 0) });
        saved.BuffChanges.ShouldBe(new[] { new SlotBuffChange(WorkingSlot, YieldElix, 3) });
    }

    [Fact]
    public void 신속의_영약이_칸에서_이어지면_속도_가산이_유지된다()
    {
        var (user, b) = NewUser();
        Put(user, b, WorkingSlot, SpeedElix, 2);

        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);

        Slot(user, WorkingSlot).SpeedBreakdown.BuffAddPermille.ShouldBe(300);
    }

    [Fact]
    public void 쉬던_슬롯에_캐릭터를_배치하면_칸에서_바로_꺼낸다()
    {
        var (user, b) = NewUser();
        Put(user, b, IdleSlot, YieldElix, 2);

        user.AssignWorkStation(IdleSlot, IndustryType.Excavation, SecondCharacterId, Base);

        Slot(user, IdleSlot).Buffs[YieldElix].ShouldBe(3);
    }

    // ───────────────────────── 꺼내기 ─────────────────────────

    [Fact]
    public void 꺼내면_보유로_돌아온다()
    {
        var (user, b) = NewUser();
        Put(user, b, IdleSlot, YieldElix, 4);

        Take(user, b, IdleSlot, YieldElix, 3).Result.ShouldBe(EResultCode.Ok);

        user.GetItemCount(YieldElix).ShouldBe(9);
    }

    [Fact]
    public void 칸에_넣은_것보다_많이_꺼낼_수_없다()
    {
        var (user, b) = NewUser();
        Put(user, b, IdleSlot, YieldElix, 2);

        Take(user, b, IdleSlot, YieldElix, 3).Result.ShouldBe(EResultCode.NotEnoughItem);

        Slot(user, IdleSlot).AutoUse[YieldElix].ShouldBe(2);
    }

    [Fact]
    public void 꺼낼_자리가_없으면_거절하고_칸에_남긴다()
    {
        // 영약 칸 + 다른 자원 49칸 = 50칸. 영약을 전부 넣어 빈 칸을 다른 자원으로 채우면 꺼낼 자리가 없다.
        var fillers = Enumerable.Range(0, User.StorageCapacity - 4)
            .Select(i => new InventoryRow { item_id = 900_000 + i, count = 1, slot = 4 + i })
            .ToArray();
        var (user, b) = NewUser(fillers);
        Put(user, b, IdleSlot, YieldElix, 10);
        user.GainItem(999_999, 1);

        Take(user, b, IdleSlot, YieldElix, 1).Result.ShouldBe(EResultCode.StorageFull);

        Slot(user, IdleSlot).AutoUse[YieldElix].ShouldBe(10);
    }
}
