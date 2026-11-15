using MikaProtocol;

namespace WSGameServer;

// 환급·우편·원장 삭제는 한 트랜잭션이다. 로그인 재시도나 중간 실패로 영약을 복제·유실하지 않는다.
internal static class LegacySlotAutoUseRefund
{
    private sealed record Row
    {
        public int item_tid { get; init; }
        public int count { get; init; }
    }

    public static Task RefundAsync(DbConnection connection, long userId, DateTime now)
        => connection.InTransactionAsync(async tx =>
        {
            var pending = await tx.QueryAsync<Row>(
                "SELECT item_tid, count FROM t_user_slot_auto_use_refund WHERE user_id = @userId ORDER BY slot_index, item_tid;",
                new { userId });
            if (pending.Count == 0)
            {
                return;
            }

            var rows = await tx.QueryAsync<InventoryRow>(
                "SELECT item_id, count, slot, container FROM t_user_inventory WHERE user_id = @userId AND container = @container;",
                new { userId, container = (int)EContainer.Inventory });
            var inventory = new Inventory(EContainer.Inventory, User.StorageCapacity);
            var changes = inventory.Load(rows.Select(r => new Item(r.item_id, r.count, r.slot)));
            var overflow = new List<(int Tid, int Count)>();
            foreach (var refund in pending.Where(r => r.count > 0).GroupBy(r => r.item_tid))
            {
                var count = refund.Sum(r => r.count);
                changes.AddRange(inventory.AddItem(refund.Key, count, User.MaxStackOf(refund.Key), out var added));
                if (added < count)
                {
                    overflow.Add((refund.Key, count - added));
                }
            }

            foreach (var change in changes)
            {
                await SaveItemChangesRepository.WriteAsync(tx, userId, change);
            }

            if (overflow.Count > 0)
            {
                await MailDb.InsertMailAsync(tx, userId, MailCatalog.OverflowTemplateTid,
                    new MailAttachment(0, overflow, new(), new()), now);
            }

            await tx.ExecuteAsync("DELETE FROM t_user_slot_auto_use_refund WHERE user_id = @userId;", new { userId });
        });
}
