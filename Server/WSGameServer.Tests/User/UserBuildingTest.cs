using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 섬 건물(T-166) — 해금 요청이 자원을 받고, 열린 건물 노드가 그 산업의 속도·산출량 가산에 붙는다.
/// 자원이 새는 지점이라 <b>거절 경로에서 보유량이 움직이면 안 되고</b>, 노드가 다른 산업에 붙으면 건물 하나가 전 산업을 올린다.
/// </summary>
public class UserBuildingTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const long CharacterId = 500;

    private const int Shard   = 10001;   // 깨진 토기 조각 — 발굴 일반
    private const int Coin    = 10002;   // 녹슨 동전 — 발굴 고급
    private const int Wheat   = 20001;   // 밀 — 농사 일반

    // 테스트 전용 표 — 엑셀 값에 기대지 않는다.
    //   9201 발굴 캠프 Lv1   조각 50
    //   9211 속도 +3%        조각 100        (선행 9201)
    //   9212 산출량 +5%      조각 10 + 동전 10 (선행 9211)
    //   9101 농장 Lv1        밀 50
    //   9111 농장 속도 +4%   밀 100          (선행 9101)
    //   9300 골드 500 + 조각 10 — 지불이 둘인 행
    private const int CampLv1   = 9201;
    private const int CampSpeed = 9211;
    private const int CampYield = 9212;
    private const int FarmLv1   = 9101;
    private const int FarmSpeed = 9111;
    private const int GoldAndItem = 9300;

    public UserBuildingTest() => GameTableFixture.EnsureLoaded();

    private static UnlockTableRow Row(int tid, int[] items, int[] counts, int gold = 0, params int[] required) => new()
    {
        UnlockTID = tid, Name = $"해금 {tid}", Gold = gold, RequiredUnlockTIDs = required, ItemTIDs = items, ItemCounts = counts,
    };

    private static BuildingNodeTableRow Node(int unlockTid, int buildingTid, int level, BuildingEffectType effect, int value) => new()
    {
        UnlockTID = unlockTid, BuildingTID = buildingTid, Level = level, EffectType = effect, EffectValue = value,
    };

    private static (User User, TestUserBuilder B) UserWith(params (int ItemTid, int Count)[] items)
    {
        var b = new TestUserBuilder().WithExcavationDrops();
        b.Unlocks.Load(
            new[]
            {
                Row(CampLv1,   new[] { Shard },       new[] { 50 }),
                Row(CampSpeed, new[] { Shard },       new[] { 100 }, required: CampLv1),
                Row(CampYield, new[] { Shard, Coin }, new[] { 10, 10 }, required: CampSpeed),
                Row(FarmLv1,   new[] { Wheat },       new[] { 50 }),
                Row(FarmSpeed, new[] { Wheat },       new[] { 100 }, required: FarmLv1),
                Row(GoldAndItem, new[] { Shard },     new[] { 10 }, gold: 500),
            },
            new[] { new WorkSlotTableRow { WorkSlotTID = 0, UnlockTID = 0 }, new WorkSlotTableRow { WorkSlotTID = 1, UnlockTID = 0 } },
            buildingRows: new[]
            {
                new BuildingTableRow { BuildingTID = 1, Name = "농장",      Industry = IndustryType.Farming },
                new BuildingTableRow { BuildingTID = 2, Name = "발굴 캠프", Industry = IndustryType.Excavation },
            },
            buildingNodeRows: new[]
            {
                Node(CampLv1,   2, 1, BuildingEffectType.None,     0),
                Node(CampSpeed, 2, 0, BuildingEffectType.SpeedAdd, 30),
                Node(CampYield, 2, 0, BuildingEffectType.YieldAdd, 50),
                Node(FarmLv1,   1, 1, BuildingEffectType.None,     0),
                Node(FarmSpeed, 1, 0, BuildingEffectType.SpeedAdd, 40),
            });

        var user = b.Build();
        foreach (var (itemTid, count) in items)
        {
            user.GainItem(itemTid, count);
        }

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    // 발굴 슬롯 하나를 돌린다 — 노드가 슬롯 속도에 붙는지 볼 때.
    private static void PlaceExcavation(User user)
    {
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = User.DefaultCharacterTid, level = 1, exp = 0 },
        });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Excavation, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
    }

    private static int SlotTraitAdd(User user)
    {
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        return slot.ToInfo().TraitAddPermille;
    }

    // ─────────────────────── 자원 비용 ───────────────────────

    [Fact]
    public void 자원이_모자라면_NotEnoughItem이고_자원과_기록은_그대로다()
    {
        var (user, b) = UserWith((Shard, 30));

        user.TryUnlock(CampLv1, CurrencyType.None, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NotEnoughItem);
        user.GetItemCount(Shard).ShouldBe(30);
        user.IsUnlocked(CampLv1).ShouldBeFalse();
        b.DB.PostedOf<SaveUnlockRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 자원을_내면_열리고_낸_만큼_빠진다()
    {
        var (user, b) = UserWith((Shard, 80));

        user.TryUnlock(CampLv1, CurrencyType.None, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(Shard).ShouldBe(30);   // 80 − 50
        user.IsUnlocked(CampLv1).ShouldBeTrue();
    }

    [Fact]
    public void 자원을_내면_줄어든_수량을_클라에_알린다()
    {
        var (user, b) = UserWith((Shard, 80));

        user.TryUnlock(CampLv1, CurrencyType.None, Base);

        var change = b.Channel.SentOf<S_UpdateItemResponse>().ShouldHaveSingleItem().ItemChangeInfos.ShouldHaveSingleItem();
        change.ItemId.ShouldBe(Shard);
        change.Count.ShouldBe(30);
    }

    [Fact]
    public void 여러_종_중_하나라도_모자라면_아무것도_빼지_않는다()
    {
        var (user, b) = UserWith((Shard, 100), (Coin, 5));
        user.GrantUnlock(CampLv1, Base);
        user.GrantUnlock(CampSpeed, Base);
        b.Channel.Sent.Clear();

        user.TryUnlock(CampYield, CurrencyType.None, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NotEnoughItem);
        user.GetItemCount(Shard).ShouldBe(100);
        user.GetItemCount(Coin).ShouldBe(5);
    }

    [Fact]
    public void 골드가_모자라면_자원도_빼지_않는다()
    {
        var (user, b) = UserWith((Shard, 50));
        user.GainGold(100);
        b.Channel.Sent.Clear();

        user.TryUnlock(GoldAndItem, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.GetItemCount(Shard).ShouldBe(50);
        user.Gold.ShouldBe(100);
    }

    [Fact]
    public void 자원이_모자라면_골드도_빼지_않는다()
    {
        var (user, b) = UserWith((Shard, 5));
        user.GainGold(1000);
        b.Channel.Sent.Clear();

        user.TryUnlock(GoldAndItem, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NotEnoughItem);
        user.Gold.ShouldBe(1000);
    }

    // ─────────────────────── 노드 효과 ───────────────────────

    [Fact]
    public void 속도_노드를_열면_그_산업_슬롯의_가산이_오른다()
    {
        var (user, _) = UserWith();
        PlaceExcavation(user);
        user.GrantUnlock(CampLv1, Base);
        var before = SlotTraitAdd(user);

        user.GrantUnlock(CampSpeed, Base);

        SlotTraitAdd(user).ShouldBe(before + 30);
    }

    [Fact]
    public void 다른_산업의_건물_노드는_슬롯에_붙지_않는다()
    {
        var (user, _) = UserWith();
        PlaceExcavation(user);
        var before = SlotTraitAdd(user);

        user.GrantUnlock(FarmLv1, Base);
        user.GrantUnlock(FarmSpeed, Base);

        SlotTraitAdd(user).ShouldBe(before);
    }

    [Fact]
    public void 산출량_노드는_그_산업에만_더한다()
    {
        var (user, _) = UserWith();
        user.GrantUnlock(CampLv1, Base);
        user.GrantUnlock(CampSpeed, Base);
        user.GrantUnlock(CampYield, Base);

        user.GetBuildingEffect(BuildingEffectType.YieldAdd, IndustryType.Excavation).ShouldBe(50);
        user.GetBuildingEffect(BuildingEffectType.YieldAdd, IndustryType.Farming).ShouldBe(0);
    }

    [Fact]
    public void 건물_레벨_행은_효과가_없다()
    {
        var (user, _) = UserWith();

        user.GrantUnlock(CampLv1, Base);

        user.GetBuildingEffect(BuildingEffectType.SpeedAdd, IndustryType.Excavation).ShouldBe(0);
    }

    [Fact]
    public void 재로그인해도_열린_노드의_효과가_남는다()
    {
        // 효과는 따로 저장하지 않는다 — 열린 해금 목록이 곧 노드 상태다.
        var (user, _) = UserWith();

        user.LoadUnlocks(new[] { new UserUnlockRow { unlock_tid = CampLv1 }, new UserUnlockRow { unlock_tid = CampSpeed } });

        user.GetBuildingEffect(BuildingEffectType.SpeedAdd, IndustryType.Excavation).ShouldBe(30);
    }
}
