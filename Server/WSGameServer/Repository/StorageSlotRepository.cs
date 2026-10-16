using MikaProtocol;

namespace WSGameServer;

/// <summary>칸 변경을 한 트랜잭션으로 쓴다. 교환은 두 행이 함께 바뀌어야 한다 — 나누면 중간 실패에 한 칸이 겹친다.</summary>
public sealed class SaveStorageSlotsRepository : IRepository
{
    private readonly EContainer  _container;
    private readonly EStorageTab _tab;

    public SaveStorageSlotsRepository(User user, EContainer container, EStorageTab tab, IReadOnlyList<SlotChange> changes)
    {
        User       = user;
        _container = container;
        _tab       = tab;
        Changes    = changes;
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
                await tx.ExecuteAsync(sql, new { userId = User.Uid, container = (int)_container, key = change.Key, slot = change.Slot });
            }
        });
    }

    public void Apply()
    {
    }

    // 캐릭터·장비만 온다 — 자원은 칸이 키라 SaveItemChangesRepository로 칸째 쓴다(T-160).
    // 개체 PK는 보관함과 무관하게 유일하지만, 보관함이 어긋난 행을 덮지 않게 함께 건다.
    private static string SqlFor(EStorageTab tab)
    {
        if (tab == EStorageTab.Character)
        {
            return "UPDATE t_character SET slot = @slot WHERE user_id = @userId AND container = @container AND character_id = @key";
        }

        return "UPDATE t_user_equip SET slot_position = @slot WHERE user_id = @userId AND container = @container AND equip_id = @key";
    }
}
