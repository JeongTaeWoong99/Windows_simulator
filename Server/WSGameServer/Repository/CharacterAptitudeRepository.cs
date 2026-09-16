namespace WSGameServer;

/// <summary>
/// 캐릭터 개체가 찍은 적성 보너스를 DB에 반영한다. 델타가 아니라 <b>확정값 5열</b>을 쓴다 —
/// 재시도·중복 전송이 포인트 복제가 되지 않는다. 남은 포인트는 저장하지 않는다(레벨에서 계산).
/// </summary>
public sealed class SaveCharacterAptitudeRepository : IRepository
{
    public SaveCharacterAptitudeRepository(User user, Character character)
    {
        User        = user;
        CharacterId = character.Id;
        Bonus       = character.Bonus;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public long CharacterId { get; }

    public AptitudeBonus Bonus { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        // user_id를 함께 거는 이유 — 개체 PK가 새어도 남의 캐릭터를 갱신하지 못하게 한다.
        await connection.ExecuteAsync(
            @"UPDATE t_character
              SET farming_bonus = @farming, fishing_bonus = @fishing, mining_bonus = @mining,
                  logging_bonus = @logging, hunting_bonus = @hunting
              WHERE character_id = @characterId AND user_id = @userId;",
            new
            {
                farming = Bonus.Farming, fishing = Bonus.Fishing, mining = Bonus.Mining,
                logging = Bonus.Logging, hunting = Bonus.Hunting,
                characterId = CharacterId, userId = User.Uid,
            });
    }

    public void Apply()
    {
    }
}
