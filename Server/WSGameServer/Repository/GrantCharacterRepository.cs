namespace WSGameServer;

/// <summary>
/// 지급 후 무엇을 이어서 할지. <b>DB 작업은 같고 후속만 다르다</b> —
/// 지급 SQL을 두 벌로 나누지 않으려고 이유를 값으로 받는다.
/// </summary>
public enum CharacterGrantReason
{
    /// <summary>보유 캐릭터가 0명인 로그인. 지급 후 로그인을 마무리한다.</summary>
    Login = 0,

    /// <summary>가챠 당첨. 지급 후 캐릭터 목록을 다시 내려보낸다.</summary>
    Gacha = 1,
}

/// <summary>
/// 캐릭터를 지급(INSERT)하고 발급된 개체 PK를 로직 스레드로 돌려준다.
/// 레벨·경험치는 DB 기본값(1·0)으로 시작한다.
///
/// <para>
/// <b>여러 장을 한 번에 받는다.</b> 10연차가 캐릭터 10장을 주므로, 장당 DB 왕복을 하면
/// 목록 갱신이 열 번 쪼개져 내려간다. 발급 순서는 넘긴 TID 순서와 같다.
/// </para>
/// </summary>
public sealed class GrantCharacterRepository : IRepository
{
    private readonly IReadOnlyList<int> _characterTids;
    private readonly CharacterGrantReason _reason;
    private readonly List<(long Id, int Tid)> _granted = new();

    public long Key => User.SessionId;

    public User User { get; }

    /// <summary>지급을 요청받은 캐릭터 종류들. 같은 TID가 여러 번 들어 있을 수 있다.</summary>
    public IReadOnlyList<int> CharacterTids => _characterTids;

    public GrantCharacterRepository(User user, int characterTid, CharacterGrantReason reason)
        : this(user, new[] { characterTid }, reason)
    {
    }

    public GrantCharacterRepository(User user, IReadOnlyList<int> characterTids, CharacterGrantReason reason)
    {
        User           = user;
        _characterTids = characterTids;
        _reason        = reason;
    }

    // === DB 스레드에서 실행 ===
    public async Task ExecuteAsync(DbConnection connection)
    {
        foreach (var tid in _characterTids)
        {
            var characterId = await connection.ExecuteScalarAsync<long>(
                @"INSERT INTO t_character (user_id, character_tid)
                  VALUES (@userId, @tid) RETURNING character_id;",
                new { userId = User.Uid, tid });

            _granted.Add((characterId, tid));
        }
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        if (_reason == CharacterGrantReason.Login)
        {
            User.OnDefaultCharacterGranted(_granted[0].Id, DateTime.UtcNow);
            return;
        }

        User.OnGachaCharactersGranted(_granted);
    }
}
