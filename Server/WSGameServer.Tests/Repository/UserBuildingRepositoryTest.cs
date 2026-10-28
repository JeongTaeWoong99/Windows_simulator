namespace WSGameServer;

/// <summary>
/// 건물 레벨이 <b>DB를 거쳐 되살아나는지</b> 실제 SQLite로 검증한다(T-183 · 마이그레이션 0010).
/// 저장 SQL과 로그인 조회 SQL이 어긋나면 재접속 때 건물이 Lv0으로 돌아가고 트리 포인트가 사라진다.
/// </summary>
public class UserBuildingRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public UserBuildingRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task 건물_레벨은_저장되고_로그인_조회로_되읽힌다()
    {
        const long uid = 7;
        var owner = new TestUserBuilder().Build(uid);

        // 두 번 써도 행은 하나다(UPSERT) — 마지막 값이 남는다.
        await new SaveUserBuildingRepository(owner, buildingTid: 2, level: 3).ExecuteAsync(new DbConnection(_db.Connection));
        await new SaveUserBuildingRepository(owner, buildingTid: 2, level: 7).ExecuteAsync(new DbConnection(_db.Connection));

        var again = new TestUserBuilder().Build(uid);
        var login = new LoginRepository(again);
        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        again.GetBuildingLevel(2).ShouldBe(7);
    }
}
