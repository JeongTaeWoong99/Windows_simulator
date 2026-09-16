namespace WSGameServer;

/// <summary>
/// 캐릭터 개체의 레벨·경험치를 DB에 반영한다.
/// 델타가 아니라 <b>확정값</b>을 쓴다 — 재시도·중복 전송이 경험치 복제가 되지 않는다.
/// </summary>
public sealed class SaveCharacterGrowthRepository : IRepository
{
    private readonly long _characterId;
    private readonly int  _level;
    private readonly int  _exp;

    public SaveCharacterGrowthRepository(User user, Character character)
    {
        User         = user;
        _characterId = character.Id;
        _level       = character.Level;
        _exp         = character.Exp;
    }

    public long Key => User.DbKey;

    public User User { get; }

    /// <summary>저장 대상 개체. 테스트가 어느 캐릭터의 성장이 저장됐는지 볼 때 쓴다.</summary>
    public long CharacterId => _characterId;

    public int Level => _level;
    public int Exp   => _exp;

    public async Task ExecuteAsync(DbConnection connection)
    {
        // user_id를 함께 거는 이유 — 개체 PK가 새어도 남의 캐릭터를 갱신하지 못하게 한다.
        await connection.ExecuteAsync(
            @"UPDATE t_character
              SET level = @level, exp = @exp
              WHERE character_id = @characterId AND user_id = @userId;",
            new { level = _level, exp = _exp, characterId = _characterId, userId = User.Uid });
    }

    public void Apply()
    {
    }
}
