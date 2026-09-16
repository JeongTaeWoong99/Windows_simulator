using GameData;

namespace WSGameServer;

/// <summary>
/// 찍은 적성 보너스가 <b>DB를 거쳐 되살아나는지</b> 실제 SQLite로 검증한다.
/// 저장 SQL과 로그인 조회 SQL의 컬럼 이름이 어긋나면 컴파일은 통과한 채 재접속 때 보너스가 사라진다.
/// </summary>
public class CharacterAptitudeRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public CharacterAptitudeRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreatePlayerTables();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task 찍은_보너스는_저장되고_로그인_조회로_되읽힌다()
    {
        const long uid = 7;

        // 지급 → 개체 PK 발급 (GrantCharacterRepository의 INSERT)
        var owner = new TestUserBuilder().Build(uid);
        var grant = new GrantCharacterRepository(owner, User.DefaultCharacterTid, CharacterGrantReason.Gacha);
        await grant.ExecuteAsync(new DbConnection(_db.Connection));
        grant.Apply();
        var characterId = owner.Characters.Single().Id;

        // 낚시 1 · 사냥 2를 찍은 상태를 저장한다
        var grown = new Character(characterId, GameTable.CharacterTable[User.DefaultCharacterTid], level: 30, exp: 0,
                                  new AptitudeBonus(Fishing: 1, Hunting: 2));
        await new SaveCharacterAptitudeRepository(owner, grown).ExecuteAsync(new DbConnection(_db.Connection));

        // 다른 세션으로 다시 로그인 — 조회 결과가 도메인까지 도달하는지 본다
        var again = new TestUserBuilder().Build(uid);
        var login = new LoginRepository(again);
        await login.ExecuteAsync(new DbConnection(_db.Connection));
        login.Apply();

        again.TryGetCharacter(characterId, out var loaded).ShouldBeTrue();
        loaded.Bonus.ShouldBe(new AptitudeBonus(Fishing: 1, Hunting: 2));
        loaded.GetAptitude(IndustryType.Fishing).ShouldBe(2);
    }
}
