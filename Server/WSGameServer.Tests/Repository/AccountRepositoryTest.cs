using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 로그인 첫 단계(유저 조회·자동 가입)를 실제 SQLite에 대고 검증한다.
/// 밴·삭제 유저는 예외가 아니라 정상 흐름인데도 응답 없이 멈추던 경로다 — 코드로 거절하고 세션을 끊어야 한다.
/// </summary>
public class AccountRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public AccountRepositoryTest()
    {
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private async Task<(TestUserBuilder Builder, User User)> LoginAsync(string pid)
    {
        var builder = new TestUserBuilder().WithPid(pid);
        var user    = builder.Build();
        var repo    = new AccountRepository(user);

        await repo.ExecuteAsync(new DbConnection(_db.Connection));
        repo.Apply();
        return (builder, user);
    }

    [Fact]
    public async Task 밴_유저는_Banned_응답을_받고_세션이_끊긴다()
    {
        _db.Execute("INSERT INTO t_user (provider_id, nickname, is_banned) VALUES ('banned-pid', '밴', 1)");

        var (builder, _) = await LoginAsync("banned-pid");

        builder.Channel.SentOf<S_LoginResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Banned);
        builder.Channel.DisconnectCount.ShouldBe(1);
    }

    [Fact]
    public async Task 삭제된_유저는_Deleted_응답을_받고_세션이_끊긴다()
    {
        _db.Execute("INSERT INTO t_user (provider_id, nickname, is_deleted) VALUES ('gone-pid', '탈퇴', 1)");

        var (builder, _) = await LoginAsync("gone-pid");

        builder.Channel.SentOf<S_LoginResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Deleted);
        builder.Channel.DisconnectCount.ShouldBe(1);
    }

    [Fact]
    public async Task 처음_보는_pid는_자동_가입되고_다음_단계로_넘어간다()
    {
        var (builder, user) = await LoginAsync("new-pid");

        // 가입이 끝났다는 증거는 두 가지 — 신규 표시와, 로그인 데이터 적재(LoginRepository)가 예약됐다는 것.
        user.IsNewbie.ShouldBeTrue();
        builder.DB.PostedOf<LoginRepository>().ShouldHaveSingleItem();
        builder.Channel.SentOf<S_LoginResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 신규_계정은_첫_로그인과_재로그인에_같은_램볼이_1번_슬롯에서_농사한다()
    {
        var first = LoginWithSnapshot("new-pid");
        var starter = first.SentOf<S_CharacterListResponse>().ShouldHaveSingleItem().Characters!.ShouldHaveSingleItem();
        starter.CharacterTid.ShouldBe(1001);
        AssertFarmingSlot(first, starter.CharacterId);

        var second = LoginWithSnapshot("new-pid");
        var reloaded = second.SentOf<S_CharacterListResponse>().ShouldHaveSingleItem().Characters!.ShouldHaveSingleItem();
        reloaded.CharacterId.ShouldBe(starter.CharacterId);
        AssertFarmingSlot(second, starter.CharacterId);
    }

    [Fact]
    public void 이전에_만든_계정의_빈_작업_슬롯은_로그인해도_비어_있다()
    {
        _db.Execute("INSERT INTO t_user (user_id, provider_id, nickname) VALUES (10, 'existing-pid', '기존')");
        _db.Execute("INSERT INTO t_character (user_id, character_tid) VALUES (10, 1001)");

        var channel = LoginWithSnapshot("existing-pid");
        var firstSlot = channel.SentOf<S_WorkStationSlotsResponse>().ShouldHaveSingleItem().Slots!
            .Single(slot => slot.SlotIndex == 0);
        (firstSlot.Industry, firstSlot.CharacterId).ShouldBe((EIndustryType.None, 0L));
    }

    [Fact]
    public async Task 첫_슬롯_저장에_실패하면_계정과_램볼도_롤백되어_가입을_재시도할_수_있다()
    {
        _db.Execute("""
            CREATE TEMP TRIGGER fail_starter_assignment
            BEFORE INSERT ON t_user_workstation_slot
            BEGIN
                SELECT RAISE(ABORT, 'slot write failed');
            END;
            """);

        await Should.ThrowAsync<Microsoft.Data.Sqlite.SqliteException>(() => LoginAsync("retry-pid"));

        _db.Query("SELECT COUNT(*) FROM t_user").ShouldBe(0L);
        _db.Query("SELECT COUNT(*) FROM t_character").ShouldBe(0L);
        _db.Execute("DROP TRIGGER fail_starter_assignment");

        var channel = LoginWithSnapshot("retry-pid");
        var starter = channel.SentOf<S_CharacterListResponse>().ShouldHaveSingleItem().Characters!.ShouldHaveSingleItem();
        AssertFarmingSlot(channel, starter.CharacterId);
    }

    private FakeClientChannel LoginWithSnapshot(string pid)
    {
        var logic = new FakeLogicExecutor { RunImmediately = true };
        var queue = new SqliteDBQueue(_db, logic);
        var builder = new TestUserBuilder { QueueOverride = queue, ExecutorOverride = logic }.WithPid(pid);
        queue.Post(new AccountRepository(builder.Build()));
        return builder.Channel;
    }

    private static void AssertFarmingSlot(FakeClientChannel channel, long characterId)
    {
        var slots = channel.SentOf<S_WorkStationSlotsResponse>().ShouldHaveSingleItem().Slots!;
        var firstSlot = slots.Single(slot => slot.SlotIndex == 0);
        (firstSlot.Industry, firstSlot.IndustryLevel, firstSlot.CharacterId).ShouldBe((EIndustryType.Farming, (byte)1, characterId));
        firstSlot.CurrentWorkSpeed.ShouldBeGreaterThan(0);
        slots.Single(slot => slot.SlotIndex == 1).CharacterId.ShouldBe(0L);
    }
}
