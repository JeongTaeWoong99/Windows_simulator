using MikaProtocol;

namespace WSGameServer;

/// <summary>개체 하나가 옮겨 간 보관함과 칸.</summary>
public readonly record struct EntityMove(long Id, EContainer Container, int Slot);

/// <summary>
/// 창고 이동을 한 트랜잭션으로 쓴다. 스왑은 두 보관함의 행이 함께 바뀌어야 한다 —
/// 나누면 중간 실패에 한쪽만 옮겨져 자원이 늘거나 사라진다.
/// </summary>
public sealed class WarehouseTransferRepository : IRepository
{
    private readonly EStorageTab _tab;

    public WarehouseTransferRepository(User user, EStorageTab tab, List<ItemChangeInfo> items, List<EntityMove> entities)
    {
        User     = user;
        _tab     = tab;
        Items    = new List<ItemChangeInfo>(items);
        Entities = new List<EntityMove>(entities);
    }

    public long Key => User.DbKey;

    public User User { get; }

    public IReadOnlyList<ItemChangeInfo> Items { get; }

    public IReadOnlyList<EntityMove> Entities { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in Items)
            {
                await SaveItemChangesRepository.WriteAsync(tx, User.Uid, change);
            }

            var sql = _tab == EStorageTab.Character
                ? "UPDATE t_character SET container = @container, slot = @slot WHERE user_id = @userId AND character_id = @id"
                : "UPDATE t_user_equip SET container = @container, slot_position = @slot WHERE user_id = @userId AND equip_id = @id";
            foreach (var move in Entities)
            {
                await tx.ExecuteAsync(sql, new { userId = User.Uid, id = move.Id, container = (int)move.Container, slot = move.Slot });
            }
        });
    }

    public void Apply()
    {
    }
}
