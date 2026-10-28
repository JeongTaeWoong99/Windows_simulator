using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 거점 건물 업그레이드(T-183) — 골드 + 자원을 내고 1레벨 올린다. 건물 레벨의 상한은 계정 레벨이다.
/// 골드·자원이 새는 지점이라 <b>거절 경로에서 보유량이 움직이면 안 된다.</b>
/// </summary>
public class UserBuildingTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int Wood = 30001;   // 잡목 — 벌목 일반
    private const int Ore  = 40001;   // 돌덩어리 — 채굴 일반

    // 테스트 전용 표 — 엑셀 값에 기대지 않는다. 관리소(1) 최대 Lv3:
    //   Lv1 골드 100
    //   Lv2 골드 300 + 잡목 20
    //   Lv3 골드 500 + 잡목 40 + 돌덩어리 10
    private const int Office = TestBuildings.Office;

    public UserBuildingTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWith(int accountLevel = 10, int officeLevel = 0, long gold = 0,
                                                           params (int ItemTid, int Count)[] items)
    {
        var b = new TestUserBuilder();
        b.Traits.Load(
            Array.Empty<UserTraitTableRow>(),
            new[] { TestBuildings.Building(Office, 3) },
            new[]
            {
                new BuildingLevelTableRow { BuildingLevelTID = 1001, BuildingTID = Office, Level = 1, Gold = 100 },
                new BuildingLevelTableRow { BuildingLevelTID = 1002, BuildingTID = Office, Level = 2, Gold = 300,
                                            ItemTIDs = new[] { Wood }, ItemCounts = new[] { 20 } },
                new BuildingLevelTableRow { BuildingLevelTID = 1003, BuildingTID = Office, Level = 3, Gold = 500,
                                            ItemTIDs = new[] { Wood, Ore }, ItemCounts = new[] { 40, 10 } },
            });

        var user = b.Build();
        user.LoadAccount(new AccountRow { level = accountLevel, exp = 0 });
        if (officeLevel > 0)
        {
            user.LoadBuildings(new[] { new UserBuildingRow { building_tid = Office, level = officeLevel } });
        }

        if (gold > 0)
        {
            user.GainGold(gold);
        }

        foreach (var (itemTid, count) in items)
        {
            user.GainItem(itemTid, count);
        }

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_BuildingUpgradeResponse Response(TestUserBuilder b)
        => b.Channel.SentOf<S_BuildingUpgradeResponse>().ShouldHaveSingleItem();

    // ─────────────────────── 성공 ───────────────────────

    [Fact]
    public void 지으면_Lv1이_되고_골드가_빠지고_저장된다()
    {
        var (user, b) = UserWith(gold: 150);

        user.TryUpgradeBuilding(Office);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Level.ShouldBe(1);
        user.GetBuildingLevel(Office).ShouldBe(1);
        user.Gold.ShouldBe(50);
        var saved = b.DB.PostedOf<SaveUserBuildingRepository>().ShouldHaveSingleItem();
        saved.BuildingTid.ShouldBe(Office);
        saved.Level.ShouldBe(1);
    }

    [Fact]
    public void 자원이_든_레벨은_적힌_종을_전부_빼고_줄어든_수량을_알린다()
    {
        var (user, b) = UserWith(officeLevel: 2, gold: 500, items: new[] { (Wood, 45), (Ore, 10) });

        user.TryUpgradeBuilding(Office);

        Response(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(Wood).ShouldBe(5);   // 45 − 40
        user.GetItemCount(Ore).ShouldBe(0);
        var changes = b.Channel.SentOf<S_UpdateItemResponse>().ShouldHaveSingleItem().ItemChangeInfos;
        changes.Single(c => c.ItemId == Wood).Count.ShouldBe(5);
    }

    [Fact]
    public void 업그레이드하면_트리_포인트가_1_는다()
    {
        var (user, _) = UserWith(officeLevel: 1, gold: 300, items: (Wood, 20));

        user.TryUpgradeBuilding(Office);

        user.GetBuildingPoints(Office).ShouldBe(2);
    }

    // ─────────────────────── 거절 — 보유량이 움직이면 안 된다 ───────────────────────

    [Fact]
    public void 다음_레벨이_계정_레벨을_넘으면_BuildingAccountLevelCap이다()
    {
        var (user, b) = UserWith(accountLevel: 1, officeLevel: 1, gold: 1000, items: (Wood, 100));

        user.TryUpgradeBuilding(Office);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.BuildingAccountLevelCap);
        res.Level.ShouldBe(1);
        user.Gold.ShouldBe(1000);
        user.GetItemCount(Wood).ShouldBe(100);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 골드가_모자라면_NotEnoughCurrency고_자원도_빼지_않는다()
    {
        var (user, b) = UserWith(officeLevel: 1, gold: 299, items: (Wood, 20));

        user.TryUpgradeBuilding(Office);

        Response(b).Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(299);
        user.GetItemCount(Wood).ShouldBe(20);
        user.GetBuildingLevel(Office).ShouldBe(1);
    }

    [Fact]
    public void 자원이_하나라도_모자라면_NotEnoughItem이고_골드도_빼지_않는다()
    {
        var (user, b) = UserWith(officeLevel: 2, gold: 1000, items: new[] { (Wood, 40), (Ore, 9) });

        user.TryUpgradeBuilding(Office);

        Response(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.Gold.ShouldBe(1000);
        user.GetItemCount(Wood).ShouldBe(40);
        user.GetItemCount(Ore).ShouldBe(9);
    }

    [Fact]
    public void 최대_레벨이면_BuildingMaxLevel이다()
    {
        var (user, b) = UserWith(officeLevel: 3, gold: 1000);

        user.TryUpgradeBuilding(Office);

        var res = Response(b);
        res.Result.ShouldBe(EResultCode.BuildingMaxLevel);
        res.Level.ShouldBe(3);
        user.Gold.ShouldBe(1000);
    }

    [Fact]
    public void 없는_건물은_InvalidBuildingTID다()
    {
        var (user, b) = UserWith(gold: 1000);

        user.TryUpgradeBuilding(99);

        Response(b).Result.ShouldBe(EResultCode.InvalidBuildingTID);
        user.Gold.ShouldBe(1000);
    }

    // ─────────────────────── 로그인 ───────────────────────

    [Fact]
    public void 로그인_때_지은_건물만_목록으로_보낸다()
    {
        var (user, b) = UserWith(officeLevel: 2);

        user.SendBuildingList();

        var info = b.Channel.SentOf<S_BuildingListResponse>().ShouldHaveSingleItem().Buildings.ShouldHaveSingleItem();
        info.BuildingTID.ShouldBe(Office);
        info.Level.ShouldBe(2);
    }

    [Fact]
    public void 지은_적_없는_건물은_Lv0이다()
    {
        var (user, _) = UserWith();

        user.GetBuildingLevel(Office).ShouldBe(0);
        user.GetBuildingPoints(Office).ShouldBe(0);
    }

    // ─────────────────────── 치트 ───────────────────────

    private static C_CheatRequest Cheat(long buildingTid, long level)
        => new() { Command = ECheatCommand.SetBuildingLevel, Arg1 = buildingTid, Arg2 = level };

    [Fact]
    public void 치트는_계정_상한과_비용_없이_레벨을_정하고_최대로_자른다()
    {
        var (user, b) = UserWith(accountLevel: 1);
        user.AdminLevel = 1;

        user.ExecuteCheat(Cheat(Office, 99), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.GetBuildingLevel(Office).ShouldBe(3);
        b.DB.PostedOf<SaveUserBuildingRepository>().ShouldHaveSingleItem().Level.ShouldBe(3);
        b.Channel.SentOf<S_BuildingListResponse>().ShouldHaveSingleItem().Buildings.ShouldHaveSingleItem().Level.ShouldBe(3);
    }

    [Fact]
    public void 치트로_0까지_내리면_목록에서_빠지고_0으로_저장한다()
    {
        var (user, b) = UserWith(officeLevel: 2);
        user.AdminLevel = 1;

        user.ExecuteCheat(Cheat(Office, 0), Base);

        user.GetBuildingLevel(Office).ShouldBe(0);
        b.DB.PostedOf<SaveUserBuildingRepository>().ShouldHaveSingleItem().Level.ShouldBe(0);
        b.Channel.SentOf<S_BuildingListResponse>().ShouldHaveSingleItem().Buildings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(99, 1)]       // 없는 건물
    [InlineData(Office, -1)]  // 음수 레벨
    public void 치트의_잘못된_인자는_InvalidCheatArgs다(long buildingTid, long level)
    {
        var (user, b) = UserWith();
        user.AdminLevel = 1;

        user.ExecuteCheat(Cheat(buildingTid, level), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }
}
