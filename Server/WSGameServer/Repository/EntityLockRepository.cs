using MikaProtocol;

namespace WSGameServer;

/// <summary>캐릭터·장비 한 개체의 잠금을 확정값으로 쓴다(T-137). 경매에 넘어간 개체는 건드리지 않는다.</summary>
public sealed class EntityLockRepository : IRepository
{
    public EntityLockRepository(User user, ELockTarget target, long id, bool locked)
    {
        User   = user;
        Target = target;
        Id     = id;
        Locked = locked;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public ELockTarget Target { get; }
    public long        Id     { get; }
    public bool        Locked { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        // user_id를 함께 거는 이유 — 개체 PK가 새어도 남의 개체를 잠그지 못하게 한다.
        var table = Target == ELockTarget.Character ? "t_character" : "t_user_equip";
        var idCol = Target == ELockTarget.Character ? "character_id" : "equip_id";

        await connection.ExecuteAsync(
            $"UPDATE {table} SET is_locked = @locked WHERE {idCol} = @id AND user_id = @userId AND auction_trade_id = 0",
            new { locked = Locked ? 1 : 0, id = Id, userId = User.Uid });
    }

    public void Apply()
    {
    }
}
