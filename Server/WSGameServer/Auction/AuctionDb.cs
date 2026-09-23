using System.Text.Json;
using MikaProtocol;

namespace WSGameServer;

/// <summary>t_auction_trade 조회 Row.</summary>
public sealed record AuctionTradeRow
{
    public long   trade_id    { get; init; }
    public long   seller_id   { get; init; }
    public int    kind        { get; init; }
    public int    tid         { get; init; }
    public int    count       { get; init; }
    public long   equip_id    { get; init; }
    public string snapshot    { get; init; } = "{}";
    public long   unit_price  { get; init; }
    public long   listing_fee { get; init; }
    public int    state       { get; init; }

    public long TotalPrice => unit_price * count;
}

/// <summary>t_auction_outbox 조회 Row.</summary>
public sealed record AuctionOutboxRow
{
    public long   outbox_id { get; init; }
    public long   trade_id  { get; init; }
    public int    kind      { get; init; }
    public string payload   { get; init; } = "";
}

public enum AuctionOutboxKind
{
    Register = 1,
    Confirm  = 2,
}

/// <summary>outbox 등록 메시지. 경매장 <c>ListingSnapshot</c>으로 옮겨 보낸다.</summary>
public sealed record AuctionRegisterMessage(long ListingId, long SellerId, AuctionItemSnapshot Item, long UnitPrice, long ExpiresAtUnixMs);

/// <summary>outbox 확정 메시지.</summary>
public sealed record AuctionConfirmMessage(long PurchaseId, bool Success);

/// <summary>정산 결과. 성공이면 구매자·판매자 우편이 담긴다.</summary>
public sealed record AuctionSettleResult(bool Settled, long SellerId, UserMailRow? BuyerMail, UserMailRow? SellerMail);

/// <summary>반환 결과 — 판매자에게 간 우편.</summary>
public sealed record AuctionReturnResult(long SellerId, UserMailRow Mail);

/// <summary>
/// 경매 거래 원장 SQL. 거래 상태 전이는 전부 <c>WHERE state = 1</c> 조건부다 —
/// 정산과 반환이 경합해도, 같은 메시지가 두 번 와도 먼저 커밋한 쪽만 1행을 바꾼다.
/// </summary>
public static class AuctionDb
{
    public const int Listed   = 1;
    public const int Settled  = 2;
    public const int Returned = 3;

    /// <summary>
    /// 등록 한 트랜잭션: 인벤 확정 수량 · 골드 확정 잔액 · 거래 행 · 장비 잠금 · outbox.
    /// 하나라도 빠지면 아이템이 증발하거나(차감만) 복제된다(등록만).
    /// </summary>
    public static async Task<long> RegisterAsync(
        DbConnection connection, long sellerId, AuctionItemSnapshot item, long unitPrice, long listingFee,
        IReadOnlyList<ItemChangeInfo> inventoryChanges, long gold, long dia, DateTime expiresAt, DateTime now)
    {
        long tradeId = 0;

        await connection.InTransactionAsync(async tx =>
        {
            await WriteInventoryAsync(tx, sellerId, inventoryChanges);
            await WriteCurrencyAsync(tx, sellerId, gold, dia);

            tradeId = await tx.ExecuteScalarAsync<long>(
                @"INSERT INTO t_auction_trade (seller_id, kind, tid, count, equip_id, snapshot, unit_price, listing_fee, state, created_at)
                  VALUES (@sellerId, @kind, @tid, @count, @equipId, @snapshot, @unitPrice, @listingFee, 1, @now)
                  RETURNING trade_id;",
                new
                {
                    sellerId, kind = (int)item.Kind, tid = item.Tid, count = item.Count, equipId = item.EquipId,
                    snapshot = item.ToJson(), unitPrice, listingFee, now = MailDb.ToDb(now),
                });

            if (item.Kind == EAuctionKind.Equip)
            {
                var locked = await tx.ExecuteAsync(
                    @"UPDATE t_user_equip SET auction_trade_id = @tradeId
                      WHERE equip_id = @equipId AND user_id = @sellerId AND auction_trade_id = 0;",
                    new { tradeId, equipId = item.EquipId, sellerId });
                if (locked != 1)
                {
                    throw new InvalidOperationException($"장비 잠금 실패 — 이미 잠겼거나 남의 장비다. 장비 {item.EquipId} 판매자 {sellerId}");
                }
            }

            var message = new AuctionRegisterMessage(
                tradeId, sellerId, item, unitPrice, new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds());
            await InsertOutboxAsync(tx, tradeId, AuctionOutboxKind.Register, JsonSerializer.Serialize(message), now);
        });

        return tradeId;
    }

    /// <summary>
    /// 정산 한 트랜잭션: 거래 Listed→Settled · 장비 소유 이전 · 구매자 골드 · 구매자 우편(물건) · 판매자 우편(대금) · outbox(확정).
    /// 거래가 이미 끝났거나 가격이 어긋나면 아무것도 바꾸지 않고 outbox(실패)만 남긴다.
    /// </summary>
    public static async Task<AuctionSettleResult> SettleAsync(
        DbConnection connection, long tradeId, long buyerId, long purchaseId, long totalPrice, long buyerGold, long buyerDia, DateTime now)
    {
        AuctionSettleResult result = new(false, 0, null, null);

        await connection.InTransactionAsync(async tx =>
        {
            var trade = await FindTradeAsync(tx, tradeId);
            if (trade is null || trade.state != Listed || trade.TotalPrice != totalPrice || trade.seller_id == buyerId)
            {
                await InsertOutboxAsync(tx, tradeId, AuctionOutboxKind.Confirm, JsonSerializer.Serialize(new AuctionConfirmMessage(purchaseId, false)), now);
                result = new AuctionSettleResult(false, trade?.seller_id ?? 0, null, null);
                return;
            }

            var saleFee = AuctionRules.SaleFee(totalPrice);
            var changed = await tx.ExecuteAsync(
                @"UPDATE t_auction_trade
                     SET state = 2, buyer_id = @buyerId, purchase_id = @purchaseId, sale_fee = @saleFee, closed_at = @now
                   WHERE trade_id = @tradeId AND state = 1;",
                new { tradeId, buyerId, purchaseId, saleFee, now = MailDb.ToDb(now) });
            if (changed != 1)
            {
                throw new InvalidOperationException($"정산 전이 실패 — 거래 {tradeId}");
            }

            var item = AuctionItemSnapshot.FromJson(trade.snapshot);
            if (item.Kind == EAuctionKind.Equip)
            {
                // 잠금은 그대로 둔다 — 구매자가 우편을 받을 때 푼다. 그때까지 누구의 메모리에도 없다.
                var moved = await tx.ExecuteAsync(
                    "UPDATE t_user_equip SET user_id = @buyerId WHERE equip_id = @equipId AND auction_trade_id = @tradeId;",
                    new { buyerId, equipId = trade.equip_id, tradeId });
                if (moved != 1)
                {
                    throw new InvalidOperationException($"장비 이전 실패 — 거래 {tradeId} 장비 {trade.equip_id}");
                }
            }

            await WriteCurrencyAsync(tx, buyerId, buyerGold, buyerDia);

            var buyerAttachment  = item.ToAttachment(0);
            var sellerAttachment = new MailAttachment(totalPrice - saleFee, 0, new(), new(), new());
            var buyerMailId  = await MailDb.InsertMailAsync(tx, buyerId, AuctionMail.PurchasedTemplateTid, buyerAttachment, now);
            var sellerMailId = await MailDb.InsertMailAsync(tx, trade.seller_id, AuctionMail.SoldTemplateTid, sellerAttachment, now);

            await InsertOutboxAsync(tx, tradeId, AuctionOutboxKind.Confirm, JsonSerializer.Serialize(new AuctionConfirmMessage(purchaseId, true)), now);

            result = new AuctionSettleResult(
                true,
                trade.seller_id,
                MailDb.ToRow(buyerMailId, AuctionMail.PurchasedTemplateTid, buyerAttachment, now),
                MailDb.ToRow(sellerMailId, AuctionMail.SoldTemplateTid, sellerAttachment, now));
        });

        return result;
    }

    /// <summary>
    /// 반환 한 트랜잭션: 거래 Listed→Returned · 판매자 우편(물건 + 사유에 따라 등록비).
    /// 이미 끝난 거래면 null — 취소 이벤트와 대사가 겹쳐도 한 번만 돌려준다.
    /// </summary>
    public static async Task<AuctionReturnResult?> ReturnAsync(DbConnection connection, long tradeId, AuctionReturnReason reason, DateTime now)
    {
        AuctionReturnResult? result = null;

        await connection.InTransactionAsync(async tx =>
        {
            var trade = await FindTradeAsync(tx, tradeId);
            if (trade is null || trade.state != Listed)
            {
                return;
            }

            var changed = await tx.ExecuteAsync(
                "UPDATE t_auction_trade SET state = 3, closed_at = @now WHERE trade_id = @tradeId AND state = 1;",
                new { tradeId, now = MailDb.ToDb(now) });
            if (changed != 1)
            {
                return;
            }

            var refund     = AuctionMail.RefundsFee(reason) ? trade.listing_fee : 0;
            var attachment = AuctionItemSnapshot.FromJson(trade.snapshot).ToAttachment(refund);
            var template   = AuctionMail.ReturnTemplate(reason);
            var mailId     = await MailDb.InsertMailAsync(tx, trade.seller_id, template, attachment, now);

            result = new AuctionReturnResult(trade.seller_id, MailDb.ToRow(mailId, template, attachment, now));
        });

        return result;
    }

    public static async Task<AuctionTradeRow?> FindTradeAsync(DbConnection connection, long tradeId)
    {
        return await connection.QueryFirstOrDefaultAsync<AuctionTradeRow>(
            @"SELECT trade_id, seller_id, kind, tid, count, equip_id, snapshot, unit_price, listing_fee, state
              FROM t_auction_trade WHERE trade_id = @tradeId;",
            new { tradeId });
    }

    /// <summary>판매 중(Listed) 거래 수 — 등록 건수 상한 판정.</summary>
    public static Task<long> CountActiveAsync(DbConnection connection, long sellerId)
    {
        return connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM t_auction_trade WHERE seller_id = @sellerId AND state = 1;", new { sellerId });
    }

    /// <summary>우편으로 온 잠긴 장비를 받는다 — 잠금을 풀고 창고 칸을 준다. 이미 풀렸으면 false.</summary>
    public static async Task<bool> UnlockEquipAsync(DbConnection connection, long equipId, long userId, int slotPosition)
    {
        var changed = await connection.ExecuteAsync(
            @"UPDATE t_user_equip SET auction_trade_id = 0, slot_position = @slotPosition
              WHERE equip_id = @equipId AND user_id = @userId AND auction_trade_id <> 0;",
            new { equipId, userId, slotPosition });

        return changed == 1;
    }

    public static Task<List<AuctionOutboxRow>> LoadUnsentAsync(DbConnection connection, int max)
    {
        return connection.QueryAsync<AuctionOutboxRow>(
            @"SELECT outbox_id, trade_id, kind, payload FROM t_auction_outbox
              WHERE sent_at IS NULL ORDER BY outbox_id LIMIT @max;",
            new { max });
    }

    public static Task MarkSentAsync(DbConnection connection, long outboxId, DateTime now)
    {
        return connection.ExecuteAsync(
            "UPDATE t_auction_outbox SET sent_at = @now WHERE outbox_id = @outboxId;", new { outboxId, now = MailDb.ToDb(now) });
    }

    /// <summary>
    /// 대사 대상 — 오래 Listed이고 등록 메시지를 이미 보낸 거래. 아직 안 보냈으면 경매장이 모르는 게 정상이라 뺀다.
    /// </summary>
    public static Task<List<long>> FindStaleListedAsync(DbConnection connection, DateTime createdBefore, int max)
    {
        return connection.QueryAsync<long>(
            @"SELECT t.trade_id FROM t_auction_trade t
              WHERE t.state = 1 AND t.created_at <= @cutoff
                AND NOT EXISTS (SELECT 1 FROM t_auction_outbox o WHERE o.trade_id = t.trade_id AND o.sent_at IS NULL)
              ORDER BY t.trade_id LIMIT @max;",
            new { cutoff = MailDb.ToDb(createdBefore), max });
    }

    private static Task InsertOutboxAsync(DbConnection tx, long tradeId, AuctionOutboxKind kind, string payload, DateTime now)
    {
        return tx.ExecuteAsync(
            "INSERT INTO t_auction_outbox (trade_id, kind, payload, created_at) VALUES (@tradeId, @kind, @payload, @now);",
            new { tradeId, kind = (int)kind, payload, now = MailDb.ToDb(now) });
    }

    // 확정 수량을 쓴다(델타 아님) — 즉시 판매(SellItemsRepository)와 같은 방식이다. 0개는 행을 지운다.
    private static async Task WriteInventoryAsync(DbConnection tx, long userId, IReadOnlyList<ItemChangeInfo> changes)
    {
        foreach (var change in changes)
        {
            if (change.Count == 0)
            {
                await tx.ExecuteAsync(
                    "DELETE FROM t_user_inventory WHERE user_id = @userId AND item_id = @itemId",
                    new { userId, itemId = change.ItemId });
                continue;
            }

            await tx.ExecuteAsync(
                @"INSERT INTO t_user_inventory (user_id, item_id, count) VALUES (@userId, @itemId, @count)
                  ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count;",
                new { userId, itemId = change.ItemId, count = change.Count });
        }
    }

    // 확정 잔액을 쓴다 — 재시도·중복 전송이 곧 재화 복제가 되지 않게.
    private static Task WriteCurrencyAsync(DbConnection tx, long userId, long gold, long dia)
    {
        return tx.ExecuteAsync(
            @"INSERT INTO t_user_currency (user_id, gold, dia) VALUES (@userId, @gold, @dia)
              ON CONFLICT (user_id) DO UPDATE SET gold = excluded.gold, dia = excluded.dia;",
            new { userId, gold, dia });
    }
}
