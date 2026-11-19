using System.Globalization;

namespace WSGameServer;

public sealed record RaidProfile(RaidTicketState Tickets, IReadOnlyList<long> PartyCharacterIds);

public sealed class RaidMailboxFullException() : InvalidOperationException("레이드 보상을 보낼 우편함 공간이 없습니다.");

public static class RaidDb
{
    private const string RechargeDateFormat = "yyyy-MM-dd";

    public static async Task<RaidProfile?> LoadAsync(DbConnection connection, long userId)
    {
        var row = await connection.QueryFirstOrDefaultAsync<RaidProfileRow>(
            @"SELECT ticket_count, last_recharge_date, party_slot_1, party_slot_2, party_slot_3
              FROM t_user_raid WHERE user_id = @userId;",
            new { userId });

        if (row is null)
        {
            return null;
        }

        var rechargeDate = DateOnly.ParseExact(row.last_recharge_date, RechargeDateFormat, CultureInfo.InvariantCulture);
        long[] slots = [row.party_slot_1, row.party_slot_2, row.party_slot_3];
        return new RaidProfile(new RaidTicketState(row.ticket_count, rechargeDate),
                               slots.Where(id => id != 0).ToArray());
    }

    public static Task SaveAsync(DbConnection connection, long userId, RaidProfile profile)
    {
        if (profile.PartyCharacterIds.Count > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "레이드 편성은 최대 3명입니다.");
        }

        return connection.ExecuteAsync(
            @"INSERT INTO t_user_raid (user_id, ticket_count, last_recharge_date, party_slot_1, party_slot_2, party_slot_3)
              VALUES (@userId, @count, @rechargeDate, @slot1, @slot2, @slot3)
              ON CONFLICT(user_id) DO UPDATE SET
                 ticket_count = excluded.ticket_count, last_recharge_date = excluded.last_recharge_date,
                 party_slot_1 = excluded.party_slot_1, party_slot_2 = excluded.party_slot_2, party_slot_3 = excluded.party_slot_3;",
            new
            {
                userId,
                count = profile.Tickets.Count,
                rechargeDate = profile.Tickets.LastRechargeDate.ToString(RechargeDateFormat, CultureInfo.InvariantCulture),
                slot1 = profile.PartyCharacterIds.ElementAtOrDefault(0),
                slot2 = profile.PartyCharacterIds.ElementAtOrDefault(1),
                slot3 = profile.PartyCharacterIds.ElementAtOrDefault(2),
            });
    }

    public static Task<string?> FindAttemptAsync(DbConnection connection, long userId, string requestId)
    {
        return connection.QueryFirstOrDefaultAsync<string>(
            "SELECT result_json FROM t_user_raid_attempt WHERE user_id = @userId AND request_id = @requestId;",
            new { userId, requestId });
    }

    /// <summary>도전 결과와 티켓 잔량, 승리 상자 우편을 한 번에 저장한다.</summary>
    public static async Task<UserMailRow?> CommitAttemptAsync(DbConnection connection, long userId, RaidProfile profile,
        string requestId, string resultJson, MailAttachment? overflow, DateTime now)
    {
        UserMailRow? delivered = null;
        await connection.InTransactionAsync(async tx =>
        {
            int inserted = await tx.ExecuteAsync(
                @"INSERT INTO t_user_raid_attempt (user_id, request_id, result_json)
                  VALUES (@userId, @requestId, @resultJson)
                  ON CONFLICT(user_id, request_id) DO NOTHING;",
                new { userId, requestId, resultJson });
            if (inserted == 0)
            {
                throw new InvalidOperationException("이미 정산한 레이드 도전입니다.");
            }

            await SaveAsync(tx, userId, profile);
            if (overflow is null)
            {
                return;
            }

            int unclaimed = await tx.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM t_user_mail WHERE user_id = @userId AND claimed_at IS NULL;",
                new { userId });
            if (unclaimed >= User.MailboxCapacity)
            {
                throw new RaidMailboxFullException();
            }

            int templateTid = MailCatalog.RaidRewardTemplateTid;
            long mailId = await MailDb.InsertMailAsync(tx, userId, templateTid, overflow, now);
            delivered = MailDb.ToRow(mailId, templateTid, overflow, now);
        });

        return delivered;
    }

    private sealed record RaidProfileRow
    {
        public int    ticket_count       { get; init; }
        public string last_recharge_date { get; init; } = "";
        public long   party_slot_1       { get; init; }
        public long   party_slot_2       { get; init; }
        public long   party_slot_3       { get; init; }
    }
}
