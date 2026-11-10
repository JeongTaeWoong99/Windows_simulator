namespace WSGameServer;

/// <summary>슬롯 버프 한 줄의 확정값. 남은 횟수가 0이면 행을 지운다.</summary>
public readonly record struct SlotBuffChange(int SlotIndex, int ItemTid, int Remaining);

/// <summary>
/// 작업슬롯 소모품 버프의 남은 횟수를 <c>t_user_slot_buff</c>에 기록한다(T-195). 델타가 아니라 확정값을 쓴다 — 재시도해도 같다.
/// </summary>
public sealed class SaveSlotBuffRepository : IRepository
{
    // ⚠️ DB 스레드가 순회한다 — 호출자는 넘긴 뒤 목록을 고치지 않는다.
    public IReadOnlyList<SlotBuffChange> Changes { get; }

    public SaveSlotBuffRepository(User user, IReadOnlyList<SlotBuffChange> changes)
    {
        User    = user;
        Changes = changes;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        foreach (var change in Changes)
        {
            if (change.Remaining <= 0)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM t_user_slot_buff WHERE user_id = @userId AND slot_index = @slotIndex AND item_tid = @itemTid;",
                    new { userId = User.Uid, slotIndex = change.SlotIndex, itemTid = change.ItemTid });
                continue;
            }

            await connection.ExecuteAsync(
                @"INSERT INTO t_user_slot_buff (user_id, slot_index, item_tid, remaining)
                  VALUES (@userId, @slotIndex, @itemTid, @remaining)
                  ON CONFLICT (user_id, slot_index, item_tid) DO UPDATE SET remaining = excluded.remaining;",
                new { userId = User.Uid, slotIndex = change.SlotIndex, itemTid = change.ItemTid, remaining = change.Remaining });
        }
    }

    public void Apply()
    {
    }
}
