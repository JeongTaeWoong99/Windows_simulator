using MikaProtocol;

namespace WSGameServer;

/// <summary>칸 변경을 한 트랜잭션으로 쓴다. 교환은 두 행이 함께 바뀌어야 한다 — 나누면 중간 실패에 한 칸이 겹친다.</summary>
public sealed class SaveStorageSlotsRepository : IRepository
{
    private readonly EStorageTab _tab;

    public SaveStorageSlotsRepository(User user, EStorageTab tab, IReadOnlyList<SlotChange> changes)
    {
        User    = user;
        _tab    = tab;
        Changes = changes;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public IReadOnlyList<SlotChange> Changes { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        var sql = SqlFor(_tab);
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in Changes)
            {
                await tx.ExecuteAsync(sql, new { userId = User.Uid, key = change.Key, slot = change.Slot });
            }
        });
    }

    public void Apply()
    {
    }

    // 키는 자원이면 item_id, 개체면 PK다. 창고(container 1)는 T-107이 같은 문장에 container를 더한다.
    private static string SqlFor(EStorageTab tab)
    {
        if (tab == EStorageTab.Resource)
        {
            return "UPDATE t_user_inventory SET slot = @slot WHERE user_id = @userId AND container = 0 AND item_id = @key";
        }

        if (tab == EStorageTab.Character)
        {
            return "UPDATE t_character SET slot = @slot WHERE user_id = @userId AND character_id = @key";
        }

        return "UPDATE t_user_equip SET slot_position = @slot WHERE user_id = @userId AND equip_id = @key";
    }
}
