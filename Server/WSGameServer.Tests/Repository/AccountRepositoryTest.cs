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
        _db.CreateUserTable();
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
}
