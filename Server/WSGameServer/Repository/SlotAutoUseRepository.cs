using MikaProtocol;

namespace WSGameServer;

/// <summary>자동 사용 칸 한 줄의 확정값. 개수가 0이면 행을 지운다.</summary>
public readonly record struct SlotAutoUseChange(int SlotIndex, int ItemTid, int Count);

/// <summary>
/// 작업슬롯 자동 사용 칸(이슈 #113)과, 같이 바뀐 버프·인벤토리를 한 트랜잭션으로 기록한다.
/// 칸 · 버프는 델타가 아니라 확정값을 쓴다 — 넣고 빼는 사이 아이템이 두 곳에 있거나 사라지지 않게 함께 저장한다.
/// </summary>
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

    /// <summary>칸 한 줄의 확정값을 쓴다. 개수 0이면 행을 지운다.</summary>
    public static Task WriteAsync(DbConnection tx, long userId, SlotAutoUseChange change)
    {
        if (change.Count <= 0)
        {
            return tx.ExecuteAsync(
                "DELETE FROM t_user_slot_auto_use WHERE user_id = @userId AND slot_index = @slotIndex AND item_tid = @itemTid;",
                new { userId, slotIndex = change.SlotIndex, itemTid = change.ItemTid });
        }

        return tx.ExecuteAsync(
            @"INSERT INTO t_user_slot_auto_use (user_id, slot_index, item_tid, count)
              VALUES (@userId, @slotIndex, @itemTid, @count)
              ON CONFLICT (user_id, slot_index, item_tid) DO UPDATE SET count = excluded.count;",
            new { userId, slotIndex = change.SlotIndex, itemTid = change.ItemTid, count = change.Count });
    }

    public void Apply()
    {
    }
}
