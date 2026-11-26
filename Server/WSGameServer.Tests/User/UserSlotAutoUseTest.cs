using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>ON/OFF 설정·공유 재고 소비·버프 유지를 검증해 중복 소비와 OFF 뒤 추가 소비를 막는다.</summary>
public class UserSlotAutoUseTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;
    private const int SpeedElix = 100044;
    private const int YieldElix = 100045;
    private const int Potion = 100041;
    private const int Compass = 100036;
    private const int SilverCompass = 100011;
    private const int GoldCompass = 100037;
    private const int TreasureMap = 100010;
    private const long CharacterId = 500;
    private const long SecondCharacterId = 501;
    private const int WorkingSlot = 0;
    private const int IdleSlot = 1;

    public UserSlotAutoUseTest() => GameTableFixture.EnsureLoaded();

    private static readonly ItemUseTableRow[] Uses =
    {
        new() { ItemTID = Potion, UseEffect = ItemUseEffect.CharacterExp, EffectValue = 5 },
        new() { ItemTID = SpeedElix, UseEffect = ItemUseEffect.SlotWorkSpeed, EffectValue = 300, EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = YieldElix, UseEffect = ItemUseEffect.SlotYield, EffectValue = 1000, EffectCount = 3, SlotCount = 1 },
        new() { ItemTID = Compass, UseEffect = ItemUseEffect.SlotBoxChance, EffectValue = 500, EffectCount = 20, SlotCount = 1 },
        new() { ItemTID = SilverCompass, UseEffect = ItemUseEffect.SlotBoxChance, EffectValue = 1000, EffectCount = 20, SlotCount = 2 },
        new() { ItemTID = GoldCompass, UseEffect = ItemUseEffect.SlotBoxRelicChance, EffectValue = 1000, EffectCount = 20, SlotCount = 3 },
        new() { ItemTID = TreasureMap, UseEffect = ItemUseEffect.InstantJudge, EffectValue = 1, SlotCount = 1 },
    };

    private static (User User, TestUserBuilder B) NewUser(
        int rewardTid = TestUserBuilder.FishItemTid, int elixirEffectCount = 3)
    {
        var b = new TestUserBuilder();
        b.Drops.Register(IndustryType.Excavation, 1, DropTable.From(
            "test", new[] { (ItemTID: rewardTid, Weight: 100) }, r => r.ItemTID, r => r.Weight));
        b.ItemUses.Load(Uses.Select(use => new ItemUseTableRow
        {
            ItemTID = use.ItemTID,
            UseEffect = use.UseEffect,
            EffectValue = use.EffectValue,
            EffectCount = use.UseEffect is ItemUseEffect.SlotWorkSpeed or ItemUseEffect.SlotYield
                ? elixirEffectCount : use.EffectCount,
            SlotCount = use.SlotCount,
        }));
        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = 1001, level = 1 },
            new CharacterRow { character_id = SecondCharacterId, character_tid = 1001, level = 1 },
        });
        user.LoadInventory(Uses.Select(use => use.ItemTID)
            .Select((tid, i) => new InventoryRow { item_id = tid, count = 10, slot = i }).ToList());
        user.WorkStation.Load(new[]
        {
            new WorkStationSlot(WorkingSlot, IndustryType.Excavation, CharacterId, Base),
            new WorkStationSlot(IdleSlot, IndustryType.None, 0, Base),
        });
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static WorkStationSlot Slot(User user, int index)
    {
        user.WorkStation.TryGet(index, out var slot).ShouldBeTrue();
        return slot;
    }

    private static S_SlotAutoUseResponse Toggle(User user, TestUserBuilder b, int slot, int tid, bool on = true)
    {
        user.SetSlotAutoUse(slot, tid, on, Base);
        return b.Channel.SentOf<S_SlotAutoUseResponse>().Last();
    }

    [Fact]
    public void 배치를_해제하면_영약_효과만_사라진다()
    {
        var (user, b) = NewUser(elixirEffectCount: 1);
        var slot = Slot(user, WorkingSlot);
        Toggle(user, b, WorkingSlot, SpeedElix);
        slot.AddBuff(Compass, 3);

        user.AssignWorkStation(WorkingSlot, IndustryType.None, 0, Base);

        slot.Buffs.ShouldNotContainKey(SpeedElix);
        slot.Buffs[Compass].ShouldBe(3);
        slot.AutoUse.ShouldContain(SpeedElix);
    }

    [Fact]
    public void 산출_영약은_판정한_만큼만_쓰고_다음_것을_예약하지_않는다()
    {
        var (user, b) = NewUser(elixirEffectCount: 1);
        Toggle(user, b, WorkingSlot, YieldElix);

        user.GetItemCount(YieldElix).ShouldBe(10);
        Slot(user, WorkingSlot).AdvanceJudges(2).ShouldBeTrue();
        user.SettleWorkStation(Base);
        user.GetItemCount(YieldElix).ShouldBe(8);
        Slot(user, WorkingSlot).Buffs.ShouldBeEmpty();
    }

    [Fact]
    public void 쉬는_슬롯의_자동_사용을_켜도_보유_영약을_옮기지_않는다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, IdleSlot, YieldElix).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(YieldElix).ShouldBe(10);
        Slot(user, IdleSlot).ToInfo().AutoUse!.ShouldHaveSingleItem().On.ShouldBeTrue();
    }

    [Fact]
    public void 돌고_있는_슬롯은_켜는_즉시_하나만_써서_버프를_건다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        user.GetItemCount(YieldElix).ShouldBe(9);
        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(3);
    }

    [Fact]
    public void 같은_ON을_다시_보내도_추가로_소비하지_않는다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        Toggle(user, b, WorkingSlot, YieldElix);
        user.GetItemCount(YieldElix).ShouldBe(9);
    }

    [Fact]
    public void 보유가_없어도_ON이고_재고가_생기면_자동으로_쓴다()
    {
        var (user, b) = NewUser();
        user.LoadInventory(Array.Empty<InventoryRow>());
        Toggle(user, b, WorkingSlot, YieldElix).Result.ShouldBe(EResultCode.Ok);
        Slot(user, WorkingSlot).AutoUse.ShouldContain(YieldElix);
        b.Channel.Sent.Clear();
        user.GainItem(YieldElix, 1);
        Slot(user, WorkingSlot).AdvanceJudges(1);
        user.SettleWorkStation(Base);
        user.GetItemCount(YieldElix).ShouldBe(0);
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(2);
        b.Channel.SentOf<S_SlotAutoUseResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 신속의_영약은_바로_속도에_붙는다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, SpeedElix);
        Slot(user, WorkingSlot).SpeedBreakdown.BuffAddPermille.ShouldBe(300);
    }

    [Fact]
    public void 직접_쓴_버프가_남아_있으면_자동_소비를_기다린다()
    {
        var (user, b) = NewUser();
        user.TryUseConsumable(YieldElix, 1, 0, new[] { WorkingSlot }, Base);
        Toggle(user, b, WorkingSlot, YieldElix);
        user.GetItemCount(YieldElix).ShouldBe(9);
        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);
        user.GetItemCount(YieldElix).ShouldBe(8);
        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(3);
    }

    [Fact]
    public void OFF는_현재_버프를_남기고_이후_자동_소비만_멈춘다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        Toggle(user, b, WorkingSlot, YieldElix, false);
        Slot(user, WorkingSlot).AutoUse.ShouldBeEmpty();
        Slot(user, WorkingSlot).AdvanceJudges(5);
        user.SettleWorkStation(Base);
        // 앞 3회는 2개, 뒤 2회는 1개 → 8개.
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(8);
        user.GetItemCount(YieldElix).ShouldBe(9);
    }

    [Theory]
    [InlineData(Compass)]
    [InlineData(SilverCompass)]
    [InlineData(GoldCompass)]
    public void 나침반은_20회가_끝나면_한_개를_이어_쓴다(int tid)
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, tid).Result.ShouldBe(EResultCode.Ok);

        var slot = Slot(user, WorkingSlot);
        slot.AdvanceJudges(21);
        user.SettleWorkStation(Base);

        // 20회짜리 두 개에서 21회 차감 → 남은 19회.
        user.GetItemCount(tid).ShouldBe(8);
        slot.Buffs[tid].ShouldBe(19);
    }

    [Fact]
    public void 여러_나침반을_켜면_같은_판정에서_각각_차감한다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, SilverCompass);
        Toggle(user, b, WorkingSlot, GoldCompass);

        var slot = Slot(user, WorkingSlot);
        slot.AdvanceJudges(1);
        user.SettleWorkStation(Base);

        slot.Buffs.ShouldBe(new Dictionary<int, int> { [SilverCompass] = 19, [GoldCompass] = 19 });
    }

    [Theory]
    [InlineData(Potion)]
    [InlineData(TreasureMap)]
    [InlineData(999999)]
    public void 슬롯_버프가_아니면_자동_사용을_설정하지_못한다(int tid)
    {
        var (user, b) = NewUser();
        Toggle(user, b, IdleSlot, tid).Result.ShouldBe(EResultCode.NotAutoUsable);
        Slot(user, IdleSlot).AutoUse.ShouldBeEmpty();
    }

    [Fact]
    public void 없는_슬롯에는_자동_사용을_설정하지_못한다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, 99, YieldElix).Result.ShouldBe(EResultCode.InvalidUseTarget);
        user.GetItemCount(YieldElix).ShouldBe(10);
    }

    [Fact]
    public void 한_정산에_여러_버프가_끝나도_보유분에서_이어_쓴다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        Slot(user, WorkingSlot).AdvanceJudges(7);
        user.SettleWorkStation(Base);
        // 3회짜리 영약 3개로 7회 모두 두 배, 잔여 2회.
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(14);
        user.GetItemCount(YieldElix).ShouldBe(7);
        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(2);
    }

    [Fact]
    public void 인벤토리를_먼저_쓰고_소진되면_창고에서_쓴다()
    {
        var (user, b) = NewUser();
        user.LoadInventory(new[]
        {
            new InventoryRow { item_id = YieldElix, count = 1, slot = 0 },
            new InventoryRow { item_id = YieldElix, count = 2, slot = 0, container = (int)EContainer.Warehouse },
        });
        var first = Toggle(user, b, WorkingSlot, YieldElix).ItemChangeInfos!.ShouldHaveSingleItem();
        first.Container.ShouldBe(EContainer.Inventory);
        first.Count.ShouldBe(0);
        user.GetItemCount(YieldElix).ShouldBe(2);
        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);
        var next = b.Channel.SentOf<S_UpdateItemResponse>().Last().ItemChangeInfos!.ShouldHaveSingleItem();
        next.Container.ShouldBe(EContainer.Warehouse);
        next.Count.ShouldBe(1);
    }

    [Fact]
    public void 여러_슬롯이_같은_재고를_두_번_쓰지_않는다()
    {
        var (user, b) = NewUser();
        user.LoadInventory(new[] { new InventoryRow { item_id = YieldElix, count = 1, slot = 0 } });
        user.AssignWorkStation(IdleSlot, IndustryType.Excavation, SecondCharacterId, Base);
        Toggle(user, b, WorkingSlot, YieldElix);
        Toggle(user, b, IdleSlot, YieldElix);
        Slot(user, WorkingSlot).AdvanceJudges(4);
        Slot(user, IdleSlot).AdvanceJudges(4);
        user.SettleWorkStation(Base);
        // 첫 슬롯 3×2+1=7, 두 번째 슬롯은 4 → 11개.
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(11);
        user.GetItemCount(YieldElix).ShouldBe(0);
        Slot(user, IdleSlot).AutoUse.ShouldContain(YieldElix);
    }

    [Fact]
    public void 쉬던_슬롯에_배치하면_ON인_영약을_보유분에서_쓴다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, IdleSlot, YieldElix);
        user.AssignWorkStation(IdleSlot, IndustryType.Excavation, SecondCharacterId, Base);
        Slot(user, IdleSlot).Buffs[YieldElix].ShouldBe(3);
        user.GetItemCount(YieldElix).ShouldBe(9);
    }

    [Fact]
    public void 켜기_전_구간에_버프를_소급하지_않는다()
    {
        var (user, _) = NewUser();
        user.SetSlotAutoUse(WorkingSlot, YieldElix, true, Base.AddSeconds(90));
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(3);
    }

    [Fact]
    public void ON_설정과_버프와_보유_차감을_같이_저장한다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        var saved = b.DB.PostedOf<SaveSlotAutoUseRepository>().ShouldHaveSingleItem();
        saved.Changes.ShouldBe(new[] { new SlotAutoUseChange(WorkingSlot, YieldElix, true) });
        saved.BuffChanges.ShouldBe(new[] { new SlotBuffChange(WorkingSlot, YieldElix, 3) });
        saved.ItemChanges.ShouldHaveSingleItem().Count.ShouldBe(9);
    }

    [Fact]
    public void 직접_사용_중_자동_사용도_발생하면_최종_보유량을_응답한다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, SpeedElix);
        Slot(user, WorkingSlot).AdvanceJudges(3);

        user.TryUseConsumable(SpeedElix, 1, 0, new[] { WorkingSlot }, Base);

        user.GetItemCount(SpeedElix).ShouldBe(7);
        b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem().ItemChangeInfos!
            .ShouldHaveSingleItem().Count.ShouldBe(7);
    }

    [Fact]
    public void 자동_소비는_버프와_보유_차감을_함께_저장하고_통지한다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        b.DB.Posted.Clear();
        b.Channel.Sent.Clear();
        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);
        var saved = b.DB.PostedOf<SaveSlotAutoUseRepository>().ShouldHaveSingleItem();
        saved.ItemChanges.ShouldHaveSingleItem().Count.ShouldBe(8);
        saved.BuffChanges.ShouldBe(new[] { new SlotBuffChange(WorkingSlot, YieldElix, 3) });
        b.Channel.SentOf<S_UpdateItemResponse>().ShouldHaveSingleItem().ItemChangeInfos!.ShouldHaveSingleItem().Count.ShouldBe(8);
    }

    [Fact]
    public void 종료_정산에서도_소비를_저장하지만_패킷은_보내지_않는다()
    {
        var (user, b) = NewUser();
        Toggle(user, b, WorkingSlot, YieldElix);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base, notify: false);
        user.GetItemCount(YieldElix).ShouldBe(8);
        b.DB.PostedOf<SaveSlotAutoUseRepository>().ShouldHaveSingleItem().ItemChanges.ShouldHaveSingleItem().Count.ShouldBe(8);
        b.Channel.Sent.ShouldBeEmpty();
    }

    [Fact]
    public void 자동으로_쓴_영약을_같은_정산의_보상으로_얻어도_최종값을_저장한다()
    {
        var (user, b) = NewUser(YieldElix);
        Toggle(user, b, WorkingSlot, YieldElix);
        b.DB.Posted.Clear();
        Slot(user, WorkingSlot).AdvanceJudges(3);
        user.SettleWorkStation(Base);

        // 시작 10 - ON 때 1 - 이어 쓰기 1 + 판정 3회의 두 배 보상 6 = 14.
        user.GetItemCount(YieldElix).ShouldBe(14);
        b.DB.PostedOf<SaveSlotAutoUseRepository>().ShouldHaveSingleItem().ItemChanges
            .ShouldHaveSingleItem().Count.ShouldBe(14);
    }

    [Fact]
    public void 재접속하면_ON을_복원하고_오프라인_시간은_판정하지_않는다()
    {
        var b = new TestUserBuilder().WithExcavationDrops();
        b.ItemUses.Load(Uses);
        var user = b.Build();
        var data = new PlayerLoginData(
            new() { new InventoryRow { item_id = YieldElix, count = 10, slot = 0 } }, null,
            new() { new CharacterRow { character_id = CharacterId, character_tid = 1001, level = 1 } },
            new() { new WorkStationSlotRow { slot_index = WorkingSlot, industry = (int)IndustryType.Excavation, industry_level = 1, character_id = CharacterId } },
            new(), new(), new(), SlotAutoUseRows: new() { new SlotAutoUseRow { slot_index = WorkingSlot, item_tid = YieldElix } });
        user.OnLoginDataLoaded(data, Base.AddDays(1));
        Slot(user, WorkingSlot).AutoUse.ShouldContain(YieldElix);
        Slot(user, WorkingSlot).Buffs[YieldElix].ShouldBe(3);
        user.GetItemCount(YieldElix).ShouldBe(9);
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(0);
        b.Channel.SentOf<S_WorkStationSlotsResponse>().ShouldHaveSingleItem().Slots!
            .Single(s => s.SlotIndex == WorkingSlot).AutoUse!.ShouldHaveSingleItem().On.ShouldBeTrue();
    }
}
