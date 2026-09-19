namespace WSGameServer;

/// <summary>
/// 계정 레벨·남은 특성 포인트가 <b>DB를 거쳐 되살아나는지</b> 실제 SQLite로 검증한다.
/// 저장 SQL과 로그인 조회 SQL의 컬럼 이름이 어긋나면 재접속 때 계정이 레벨 1로 돌아가고 포인트가 사라진다.
/// </summary>
public class AccountLevelRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public AccountLevelRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreatePlayerTables();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task 계정_레벨은_저장되고_로그인_조회로_되읽힌다()
    {
        const long uid = 7;
        var owner = new TestUserBuilder().Build(uid);

        // 두 번 써도 행은 하나다(UPSERT) — 마지막 확정값이 남는다.
        await new SaveAccountRepository(owner, level: 3, exp: 40, traitPoint: 1).ExecuteAsync(new DbConnection(_db.Connection));
        await new SaveAccountRepository(owner, level: 12, exp: 5_000_000_000L, traitPoint: 2).ExecuteAsync(new DbConnection(_db.Connection));

        var again = new TestUserBuilder().Build(uid);
        var login = new LoginRepository(again);
        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        again.AccountLevel.ShouldBe(12);
        again.AccountExp.ShouldBe(5_000_000_000L);   // int를 넘는 값 — 곡선이 캐릭터의 8배라 실제로 닿는다
        again.TraitPoint.ShouldBe(2);
    }
}
