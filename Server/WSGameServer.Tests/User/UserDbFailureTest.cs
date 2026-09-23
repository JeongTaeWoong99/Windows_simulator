using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// DB 작업이 실패했을 때 유저가 <b>조용히 멈추지 않는지</b> 검증한다.
///
/// <para>
/// 실패한 Repository는 Apply가 불리지 않는다. 로그인 중이면 클라는 응답 없이 영원히 기다리고,
/// 로그인 뒤면 메모리와 DB가 갈라진 채 다음 쓰기가 계속된다. 둘 다 세션을 끊어 재접속 때 DB를 다시 읽게 한다.
/// </para>
/// </summary>
public class UserDbFailureTest
{
    private static readonly Exception Boom = new InvalidOperationException("database is locked");

    [Fact]
    public void 로그인_중_DB가_실패하면_DbError_응답을_보내고_세션을_끊는다()
    {
        var builder = new TestUserBuilder();
        builder.DB.FailWith = Boom;
        var user = builder.Build();

        // Create → OnCreate(AccountRepository) 예약. Drain으로 돌리면 FakeDBQueue가 곧바로 실패를 되돌린다.
        user.Create();
        builder.Executor.Drain();

        var response = builder.Channel.SentOf<S_LoginResponse>().ShouldHaveSingleItem();
        response.Result.ShouldBe(EResultCode.DbError);
        user.IsDestroyed.ShouldBeTrue();
        builder.Channel.DisconnectCount.ShouldBe(1);
    }

    [Fact]
    public void 로그인_뒤_저장이_실패하면_로그인_응답_없이_세션만_끊는다()
    {
        var builder = new TestUserBuilder();
        var user    = builder.Build();
        LogIn(user);

        // 로그인 응답은 이미 한 번 나갔다. 실패했다고 또 보내면 클라가 로그인 화면으로 되돌아간다.
        builder.DB.FailWith = Boom;
        user.GainGold(10);

        builder.Channel.SentOf<S_LoginResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.IsDestroyed.ShouldBeTrue();
        builder.Channel.DisconnectCount.ShouldBe(1);
    }

    // 낚시 슬롯에 캐릭터를 둔 유저. 종료 정산이 돌면 판정분이 지급(AddItemRepository)된다.
    private static (User User, TestUserBuilder B) FishingUser()
    {
        var b    = new TestUserBuilder().WithFishingDrops();
        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = 1, character_tid = User.DefaultCharacterTid, level = 1, exp = 0 } });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, GameData.IndustryType.Fishing, 1, TestUserBuilder.Base) });
        return (user, b);
    }

    [Fact]
    public void 정상_종료면_마지막_정산을_저장한다()
    {
        var (user, b) = FishingUser();

        user.Disconnect(TestUserBuilder.Base.AddMinutes(10));

        b.DB.PostedOf<AddItemRepository>().ShouldNotBeEmpty();
    }

    [Fact]
    public void DB_실패로_끊기면_종료_정산을_저장하지_않는다()
    {
        // 메모리가 DB와 갈라진 상태다(예: 경매 등록이 롤백돼 메모리만 차감됨). 그 메모리를 확정값으로 쓰면 롤백된 차감이 되살아난다.
        var (user, b) = FishingUser();
        user.OnDbFailed("RegisterAuctionRepository", Boom);
        b.DB.Posted.Clear();

        user.Disconnect(TestUserBuilder.Base.AddMinutes(10));

        b.DB.PostedOf<AddItemRepository>().ShouldBeEmpty();
    }

    // 실제 로그인 경로를 태운다. 캐릭터가 하나 있어야 기본 캐릭터 지급(DB 왕복)을 건너뛰고 바로 끝난다.
    private static void LogIn(User user)
    {
        user.OnLoginDataLoaded(new PlayerLoginData(
            new List<InventoryRow>(),
            null,
            new List<CharacterRow> { new() { character_id = 1, character_tid = User.DefaultCharacterTid, level = 1, exp = 0 } },
            new List<WorkStationSlotRow>(),
            new List<UserUnlockRow>(),
            new List<UserEquipRow>(),
            new List<CharacterEquipRow>()), TestUserBuilder.Base);
    }
}
