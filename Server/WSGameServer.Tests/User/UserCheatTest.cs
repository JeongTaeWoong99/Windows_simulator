using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 치트 명령 검증 (<c>Server/docs/치트.md</c>).
///
/// 두 가지를 지킨다 — <b>권한이 없으면 아무것도 바뀌지 않는다</b>, 그리고 <b>치트는 게임과 같은 지급 경로를 탄다</b>
/// (재화 푸시·인벤토리 저장·경험치 레벨업이 실제 플레이와 같은 패킷·Repository로 나간다).
/// </summary>
public class UserCheatTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    public UserCheatTest() => GameTableFixture.EnsureLoaded();

    /// <summary>관리자(admin_level 1) 유저. 캐릭터 1장을 보유한다.</summary>
    private static (User User, TestUserBuilder B) Admin()
    {
        var b    = new TestUserBuilder().WithFishingDrops();
        var user = b.Build();
        user.AdminLevel = 1;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        b.Growth.Load(new[]
        {
            new CharacterLevelTableRow { CharacterLevelTID = 1, RequiredExp = 0 },
            new CharacterLevelTableRow { CharacterLevelTID = 2, RequiredExp = 10 },
            new CharacterLevelTableRow { CharacterLevelTID = 3, RequiredExp = 12 },   // Lv2가 만렙이면 이월 조각이 0으로 버려진다
        });
        return (user, b);
    }

    private static C_CheatRequest Req(ECheatCommand command, long arg1 = 0, long arg2 = 0)
        => new() { Command = command, Arg1 = arg1, Arg2 = arg2 };

    [Fact]
    public void 권한이_없으면_NoPermission으로_거절하고_아무것도_바꾸지_않는다()
    {
        var (user, b) = Admin();
        user.AdminLevel = 0;

        user.ExecuteCheat(Req(ECheatCommand.GiveGold, 1000), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NoPermission);
        user.Gold.ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 골드를_지급하면_잔액이_늘고_재화_푸시가_나간다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveGold, 1000), Base);

        user.Gold.ShouldBe(1000);
        b.Channel.SentOf<S_CurrencyResponse>().ShouldHaveSingleItem().Gold.ShouldBe(1000);
        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 음수_골드는_차감이다()
    {
        var (user, _) = Admin();
        user.GainGold(1000);

        user.ExecuteCheat(Req(ECheatCommand.GiveGold, -300), Base);

        user.Gold.ShouldBe(700);
    }

    [Fact]
    public void 차감할_골드가_모자라면_NotEnoughCurrency고_잔액은_그대로다()
    {
        var (user, b) = Admin();
        user.GainGold(100);

        user.ExecuteCheat(Req(ECheatCommand.GiveGold, -300), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(100);
    }

    [Fact]
    public void 금액이_0이면_InvalidCheatArgs다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveGold, 0), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
    }

    /// <summary>ItemTable의 실제 첫 행(붕어). 테스트용 드롭 TID(1001)는 ItemTable에 없어서 치트의 존재 검사에 걸린다.</summary>
    private const int RealItemTid = 10001;

    [Fact]
    public void 아이템을_지급하면_인벤토리에_들어가고_저장된다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveItem, RealItemTid, 3), Base);

        b.DB.PostedOf<AddItemRepository>().ShouldHaveSingleItem();
        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 테이블에_없는_아이템은_InvalidCheatArgs다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveItem, 999_999, 1), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 캐릭터_경험치를_주면_레벨업까지_같은_경로로_간다()
    {
        // 곡선 Lv2 = 10. 13을 주면 Lv2, 남은 3 — 정산 경로(GrantCharacterExp)와 같은 결과여야 한다.
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveCharacterExp, CharacterId, 13), Base);

        user.TryGetCharacter(CharacterId, out var character).ShouldBeTrue();
        character.Level.ShouldBe(2);
        character.Exp.ShouldBe(3);
        b.DB.PostedOf<SaveCharacterGrowthRepository>().ShouldHaveSingleItem();
        b.Channel.SentOf<S_CharacterSyncResponse>().ShouldHaveSingleItem().Character!.Level.ShouldBe(2);
    }

    [Fact]
    public void 미보유_캐릭터의_경험치는_CharacterNotOwned다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveCharacterExp, 999, 10), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.CharacterNotOwned);
    }

    [Fact]
    public void 캐릭터를_지급하면_가챠와_같은_지급_요청이_나간다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveCharacter, AllRounderTid, 2), Base);

        var grant = b.DB.PostedOf<GrantCharacterRepository>().ShouldHaveSingleItem();
        grant.CharacterTids.ShouldBe(new[] { AllRounderTid, AllRounderTid });
    }

    [Fact]
    public void 캐릭터_장수가_범위_밖이면_InvalidCheatArgs다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.GiveCharacter, AllRounderTid, 11), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void Settle은_넘긴_시각으로_정산한다()
    {
        // 기본 속도 30초에 1판정. 5분이면 10판정 — SettleWorkStation과 같은 결과다.
        var (user, b) = Admin();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });

        user.ExecuteCheat(Req(ECheatCommand.Settle), Base.AddMinutes(5));

        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount.ShouldBe(10);
    }

    [Fact]
    public void 해금을_지급하면_조건_없이_열리고_칸이_생긴다()
    {
        // 실데이터 1003(3번 칸)은 1500골드 + 선행 1002가 조건이다. 골드 0·선행 없음에도 열려야 GrantUnlock 경로다.
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.Unlock, 1003), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.IsUnlocked(1003).ShouldBeTrue();
        user.Gold.ShouldBe(0);
        b.DB.PostedOf<SaveUnlockRepository>().ShouldHaveSingleItem();
        user.WorkStation.TryGet(3, out _).ShouldBeTrue();
    }

    [Fact]
    public void 이미_열린_해금_지급은_AlreadyUnlocked다()
    {
        var (user, b) = Admin();
        user.GrantUnlock(1002, Base);

        user.ExecuteCheat(Req(ECheatCommand.Unlock, 1002), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.AlreadyUnlocked);
    }

    [Fact]
    public void 없는_해금_지급은_InvalidCheatArgs다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(ECheatCommand.Unlock, 9999), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 없는_명령은_InvalidCheatCommand다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req((ECheatCommand)99), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatCommand);
    }
}
