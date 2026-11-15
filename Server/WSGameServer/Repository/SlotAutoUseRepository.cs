using MikaProtocol;

namespace WSGameServer;

/// <summary>자동 사용 설정의 확정값. OFF이면 행을 지운다.</summary>
public readonly record struct SlotAutoUseChange(int SlotIndex, int ItemTid, bool On);

// 설정·버프·인벤토리 확정값을 함께 쓴다. 일부만 저장되면 재접속 때 영약을 중복 소비하거나 복제한다.
public sealed class SaveSlotAutoUseRepository : IRepository
{
    // ⚠️ DB 스레드가 순회한다 — 호출자는 넘긴 뒤 목록을 고치지 않는다.
    public IReadOnlyList<SlotAutoUseChange> Changes     { get; }
    public IReadOnlyList<SlotBuffChange>    BuffChanges { get; }
    public IReadOnlyList<ItemChangeInfo>    ItemChanges { get; }

    public SaveSlotAutoUseRepository(User user, IReadOnlyList<SlotAutoUseChange> changes,
        IReadOnlyList<SlotBuffChange>? buffChanges = null, IReadOnlyList<ItemChangeInfo>? itemChanges = null)
    {
        User        = user;
        Changes     = changes;
        BuffChanges = buffChanges ?? Array.Empty<SlotBuffChange>();
        ItemChanges = itemChanges == null ? Array.Empty<ItemChangeInfo>() : new List<ItemChangeInfo>(itemChanges);
    }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in Changes)
            {
                await WriteAsync(tx, User.Uid, change);
            }

            foreach (var change in BuffChanges)
            {
                await SaveSlotBuffRepository.WriteAsync(tx, User.Uid, change);
            }

            foreach (var change in ItemChanges)
            {
                await SaveItemChangesRepository.WriteAsync(tx, User.Uid, change);
            }
        });
    }

    /// <summary>ON인 설정만 행으로 남긴다.</summary>
    public static Task WriteAsync(DbConnection tx, long userId, SlotAutoUseChange change)
    {
        if (!change.On)
        {
            return tx.ExecuteAsync(
                "DELETE FROM t_user_slot_auto_use WHERE user_id = @userId AND slot_index = @slotIndex AND item_tid = @itemTid;",
                new { userId, slotIndex = change.SlotIndex, itemTid = change.ItemTid });
        }

        return tx.ExecuteAsync(
            @"INSERT INTO t_user_slot_auto_use (user_id, slot_index, item_tid)
              VALUES (@userId, @slotIndex, @itemTid)
              ON CONFLICT (user_id, slot_index, item_tid) DO NOTHING;",
            new { userId, slotIndex = change.SlotIndex, itemTid = change.ItemTid });
    }

    public void Apply()
    {
    }
}
