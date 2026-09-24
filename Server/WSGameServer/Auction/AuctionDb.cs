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
    public long   listing_fee  { get; init; }
    public int    state        { get; init; }
    public int    listed_count { get; init; }

    /// <summary>남은 수량 전부의 값 — 경매장 즉시구매는 남은 것을 통째로 산다.</summary>
    public long TotalPrice => unit_price * count;

    /// <summary>
    /// 남은 수량에 해당하는 등록비 — 일부 팔린 매물이 만료되면 팔리지 않은 비율만큼만 돌려준다.
    /// 등록 수량이 0인 옛 행은 남은 수량이 곧 등록 수량이다.
    /// </summary>
    public long UnsoldListingFee => listed_count <= 0 ? listing_fee : listing_fee * count / listed_count;
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

/// <summary>거래소 예약 한 줄 — 이 거래(매물)에서 이 수량을 이 단가로 잡았다.</summary>
public sealed record MarketAllocation(long TradeId, int Quantity, long UnitPrice)
{
    public long Price => UnitPrice * Quantity;
}

/// <summary>거래소 정산 결과. 성공이면 구매자 우편 1통과 판매자별 대금 우편이 담긴다.</summary>
/// <c>ClosedListings</c>는 이번 정산으로 다 팔린 그 판매자의 매물 수 — 판매 중 건수에서 뺀다.
public sealed record AuctionMarketSettleResult(bool Settled, UserMailRow? BuyerMail, IReadOnlyList<(long SellerId, UserMailRow Mail, int ClosedListings)> SellerMails);

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
                @"INSERT INTO t_auction_trade (seller_id, kind, tid, count, listed_count, equip_id, snapshot, unit_price, listing_fee, state, created_at)
                  VALUES (@sellerId, @kind, @tid, @count, @count, @equipId, @snapshot, @unitPrice, @listingFee, 1, @now)
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
    /// 거래가 이미 끝났거나 가격이 어긋나면 구매자 잔액을 되돌려 쓰고 outbox(실패)만 남긴다.
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
                // 메모리는 대금을 이미 뺐다. 그 사이 다른 저장이 뺀 잔액을 썼을 수 있으니 돌려준 잔액을 같은 트랜잭션에 쓴다 —
                // 로직 스레드의 반환 저장까지 기다리면 그 사이에 죽을 때 대금이 사라진다.
                await WriteCurrencyAsync(tx, buyerId, checked(buyerGold + totalPrice), buyerDia);
                await InsertConfirmAsync(tx, tradeId, purchaseId, false, now);
                result = new AuctionSettleResult(false, trade?.seller_id ?? 0, null, null);
                return;
            }

            var saleFee = AuctionRules.SaleFee(totalPrice);
            var changed = await tx.ExecuteAsync(
                @"UPDATE t_auction_trade
                     SET state = 2, count = 0, buyer_id = @buyerId, purchase_id = @purchaseId, sale_fee = sale_fee + @saleFee, closed_at = @now
                   WHERE trade_id = @tradeId AND state = 1;",
                new { tradeId, buyerId, purchaseId, saleFee, now = MailDb.ToDb(now) });
            if (changed != 1)
            {
                throw new InvalidOperationException($"정산 전이 실패 — 거래 {tradeId}");
            }

            var item = AuctionItemSnapshot.FromJson(trade.snapshot) with { Count = trade.count };
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

            await InsertConfirmAsync(tx, tradeId, purchaseId, true, now);

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

            // 일부 팔린 매물이면 남은 수량만 돌아가고, 등록비도 팔리지 않은 비율만큼이다.
            var refund     = AuctionMail.RefundsFee(reason) ? trade.UnsoldListingFee : 0;
            var attachment = (AuctionItemSnapshot.FromJson(trade.snapshot) with { Count = trade.count }).ToAttachment(refund);
            var template   = AuctionMail.ReturnTemplate(reason);
            var mailId     = await MailDb.InsertMailAsync(tx, trade.seller_id, template, attachment, now);

            result = new AuctionReturnResult(trade.seller_id, MailDb.ToRow(mailId, template, attachment, now));
        });

        return result;
    }

    // 거래소 정산을 통째로 되돌리는 신호. 트랜잭션 안에서 던져 앞서 줄인 수량까지 롤백한다.
    private sealed class MarketSettleRejected(string reason) : Exception(reason);

    /// <summary>
    /// 거래소 정산 한 트랜잭션: 거래마다 남은 수량 조건부 차감(0이면 Settled) · 구매자 골드 · 구매자 우편 1통(수량 합) ·
    /// 판매자별 대금 우편(수수료 제외) · outbox(확정). <b>한 줄이라도 어긋나면 전부 되돌리고</b>
    /// 구매자 잔액을 복원한 뒤 outbox(실패)만 남긴다 — 일부만 산 상태는 없다.
    /// </summary>
    public static async Task<AuctionMarketSettleResult> SettleMarketAsync(
        DbConnection connection, long buyerId, long purchaseId, int tid, IReadOnlyList<MarketAllocation> allocations,
        long buyerGold, long buyerDia, DateTime now)
    {
        var total = allocations.Sum(a => a.Price);
        var firstTrade = allocations.Count > 0 ? allocations[0].TradeId : 0;

        try
        {
            AuctionMarketSettleResult result = new(false, null, Array.Empty<(long, UserMailRow, int)>());
            await connection.InTransactionAsync(async tx =>
            {
                var proceeds = new SortedDictionary<long, long>();   // 판매자 → 수수료 뺀 대금
                var closed   = new Dictionary<long, int>();          // 판매자 → 다 팔린 매물 수

                foreach (var a in allocations)
                {
                    var trade = await FindTradeAsync(tx, a.TradeId);
                    if (trade is null || trade.state != Listed || trade.kind != (int)EAuctionKind.Item || trade.tid != tid
                        || trade.unit_price != a.UnitPrice || trade.count < a.Quantity || trade.seller_id == buyerId || a.Quantity <= 0)
                    {
                        throw new MarketSettleRejected($"거래 {a.TradeId}가 예약과 어긋난다");
                    }

                    var fee = AuctionRules.SaleFee(a.Price);
                    var changed = await tx.ExecuteAsync(
                        @"UPDATE t_auction_trade
                             SET count       = count - @quantity,
                                 state       = CASE WHEN count - @quantity = 0 THEN 2 ELSE 1 END,
                                 closed_at   = CASE WHEN count - @quantity = 0 THEN @now ELSE closed_at END,
                                 buyer_id    = @buyerId,
                                 purchase_id = @purchaseId,
                                 sale_fee    = sale_fee + @fee
                           WHERE trade_id = @tradeId AND state = 1 AND count >= @quantity;",
                        new { quantity = a.Quantity, now = MailDb.ToDb(now), buyerId, purchaseId, fee, tradeId = a.TradeId });
                    if (changed != 1)
                    {
                        throw new MarketSettleRejected($"거래 {a.TradeId} 수량 차감 실패");
                    }

                    proceeds[trade.seller_id] = proceeds.GetValueOrDefault(trade.seller_id) + a.Price - fee;
                    if (trade.count == a.Quantity)
                    {
                        closed[trade.seller_id] = closed.GetValueOrDefault(trade.seller_id) + 1;
                    }
                }

                await WriteCurrencyAsync(tx, buyerId, buyerGold, buyerDia);

                var quantity        = allocations.Sum(a => a.Quantity);
                var buyerAttachment = new MailAttachment(0, 0, new List<(int, int)> { (tid, quantity) }, new(), new());
                var buyerMailId     = await MailDb.InsertMailAsync(tx, buyerId, AuctionMail.PurchasedTemplateTid, buyerAttachment, now);

                var sellerMails = new List<(long, UserMailRow, int)>();
                foreach (var (sellerId, gold) in proceeds)
                {
                    var attachment = new MailAttachment(gold, 0, new(), new(), new());
                    var mailId     = await MailDb.InsertMailAsync(tx, sellerId, AuctionMail.SoldTemplateTid, attachment, now);
                    sellerMails.Add((sellerId, MailDb.ToRow(mailId, AuctionMail.SoldTemplateTid, attachment, now), closed.GetValueOrDefault(sellerId)));
                }

                await InsertConfirmAsync(tx, firstTrade, purchaseId, true, now);

                result = new AuctionMarketSettleResult(true, MailDb.ToRow(buyerMailId, AuctionMail.PurchasedTemplateTid, buyerAttachment, now), sellerMails);
            });

            return result;
        }
        catch (MarketSettleRejected e)
        {
            ServerLog.Warn("경매", $"거래소 정산 실패 — {e.Message}. 구매 {purchaseId} 구매자 {buyerId}");

            // 메모리는 대금을 이미 뺐다 — 돌려준 잔액을 확정 메시지와 한 트랜잭션에 쓴다(SettleAsync 실패 분기와 같은 이유).
            await connection.InTransactionAsync(async tx =>
            {
                await WriteCurrencyAsync(tx, buyerId, checked(buyerGold + total), buyerDia);
                await InsertConfirmAsync(tx, firstTrade, purchaseId, false, now);
            });

            return new AuctionMarketSettleResult(false, null, Array.Empty<(long, UserMailRow, int)>());
        }
    }

    /// <summary>
    /// 경매장 예약을 놓는다 — 예약 응답을 버렸을 때(응답 유실·유저 이탈·예약이 요청과 어긋남) Confirm(실패)를 보낸다.
    /// 경매장에 그 구매가 없거나 이미 끝났으면 무시되므로 언제 보내도 안전하다. 안 보내면 타임아웃(2분)까지 그 수량이 잠긴다.
    /// </summary>
    public static Task ReleasePurchaseAsync(DbConnection connection, long purchaseId, DateTime now)
        => InsertConfirmAsync(connection, 0, purchaseId, false, now);

    public static async Task<AuctionTradeRow?> FindTradeAsync(DbConnection connection, long tradeId)
    {
        return await connection.QueryFirstOrDefaultAsync<AuctionTradeRow>(
            @"SELECT trade_id, seller_id, kind, tid, count, equip_id, snapshot, unit_price, listing_fee, state, listed_count
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
    /// <param name="afterTradeId">이 ID 다음부터 본다(커서). 정상 판매 중인 매물이 앞을 막아 뒤쪽이 영영 안 보이는 것을 막는다.</param>
    // 아직 경매장에 안 간 메시지가 걸린 거래는 뺀다 — 등록(trade_id)이든 확정(마지막으로 이 거래를 산 purchase_id)이든.
    // 확정은 순서대로 나가므로 마지막 구매의 확정이 나갔으면 앞선 구매의 확정도 나갔다.
    public static Task<List<long>> FindStaleListedAsync(DbConnection connection, DateTime createdBefore, int max, long afterTradeId = 0)
    {
        return connection.QueryAsync<long>(
            @"SELECT t.trade_id FROM t_auction_trade t
              WHERE t.state = 1 AND t.created_at <= @cutoff AND t.trade_id > @afterTradeId
                AND NOT EXISTS (SELECT 1 FROM t_auction_outbox o
                                WHERE o.sent_at IS NULL
                                  AND (o.trade_id = t.trade_id OR (t.purchase_id <> 0 AND o.purchase_id = t.purchase_id)))
              ORDER BY t.trade_id LIMIT @max;",
            new { cutoff = MailDb.ToDb(createdBefore), max, afterTradeId });
    }

    private static Task InsertOutboxAsync(DbConnection tx, long tradeId, AuctionOutboxKind kind, string payload, DateTime now, long purchaseId = 0)
    {
        return tx.ExecuteAsync(
            "INSERT INTO t_auction_outbox (trade_id, kind, payload, created_at, purchase_id) VALUES (@tradeId, @kind, @payload, @now, @purchaseId);",
            new { tradeId, kind = (int)kind, payload, now = MailDb.ToDb(now), purchaseId });
    }

    private static Task InsertConfirmAsync(DbConnection tx, long tradeId, long purchaseId, bool success, DateTime now)
        => InsertOutboxAsync(tx, tradeId, AuctionOutboxKind.Confirm, JsonSerializer.Serialize(new AuctionConfirmMessage(purchaseId, success)), now, purchaseId);

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
