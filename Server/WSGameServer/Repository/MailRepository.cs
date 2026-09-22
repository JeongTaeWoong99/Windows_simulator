using System.Globalization;

namespace WSGameServer;

/// <summary>우편 저장소 공용 — 시각 문자열과 INSERT·전체 우편 복사를 한 곳에 둔다.</summary>
internal static class MailDb
{
    // SQLite datetime('now')와 같은 형식(UTC)이라 문자열 비교가 곧 시각 비교다.
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    public static string ToDb(DateTime utc) => utc.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static DateTime FromDb(string text)
        => DateTime.SpecifyKind(DateTime.ParseExact(text, TimeFormat, CultureInfo.InvariantCulture), DateTimeKind.Utc);

    /// <summary>우편 한 통 INSERT. 첨부는 보낸 순간의 값을 그대로 적는다.</summary>
    public static Task<long> InsertMailAsync(DbConnection connection, long userId, int templateTid, MailAttachment a, DateTime now)
    {
        return connection.ExecuteScalarAsync<long>(
            @"INSERT INTO t_user_mail (user_id, template_tid, gold, dia, items, character_tids, equip_tids, received_at)
              VALUES (@userId, @templateTid, @gold, @dia, @items, @characterTids, @equipTids, @receivedAt)
              RETURNING mail_id;",
            new
            {
                userId, templateTid, gold = a.Gold, dia = a.Dia,
                items = a.ItemsJson, characterTids = a.CharacterTidsJson, equipTids = a.EquipTidsJson,
                receivedAt = ToDb(now),
            });
    }

    // 기간 안의 전체 우편 중 아직 안 받은 것을 개인 우편함으로 복사한다. 복사 기록과 우편 행은 한 트랜잭션이다 —
    // 한쪽만 남으면 두 번 받거나(기록 없음) 영영 못 받는다(우편 없음).
    public static async Task<List<UserMailRow>> DeliverGlobalMailsAsync(DbConnection connection, long userId, DateTime now)
    {
        var pending = await connection.QueryAsync<PendingGlobalMailRow>(
            @"SELECT g.global_mail_id, g.template_tid
              FROM t_global_mail g
              WHERE g.ends_at > @now
                AND NOT EXISTS (SELECT 1 FROM t_user_global_mail r
                                WHERE r.user_id = @userId AND r.global_mail_id = g.global_mail_id)
              ORDER BY g.global_mail_id;",
            new { userId, now = ToDb(now) });

        var delivered = new List<UserMailRow>(pending.Count);
        foreach (var global in pending)
        {
            // 템플릿이 엑셀에서 사라졌으면 첨부를 알 수 없다 — 복사 기록만 남겨 다시 시도하지 않는다.
            MailAttachment? attachment = MailCatalog.Instance.TryGet(global.template_tid, out var template)
                ? MailCatalog.AttachmentOf(template)
                : null;

            await connection.InTransactionAsync(async tx =>
            {
                await tx.ExecuteAsync(
                    "INSERT OR IGNORE INTO t_user_global_mail (user_id, global_mail_id) VALUES (@userId, @id);",
                    new { userId, id = global.global_mail_id });

                if (attachment is null)
                {
                    return;
                }

                var mailId = await InsertMailAsync(tx, userId, global.template_tid, attachment, now);
                delivered.Add(ToRow(mailId, global.template_tid, attachment, now));
            });
        }

        return delivered;
    }

    public static UserMailRow ToRow(long mailId, int templateTid, MailAttachment a, DateTime receivedAt)
    {
        return new UserMailRow
        {
            mail_id = mailId, template_tid = templateTid, gold = a.Gold, dia = a.Dia,
            items = a.ItemsJson, character_tids = a.CharacterTidsJson, equip_tids = a.EquipTidsJson,
            received_at = ToDb(receivedAt),
        };
    }
}

/// <summary>
/// 로그인 때 우편함을 연다 — 기한 지난 받은 우편 정리 → 전체 우편 복사 → 목록 조회.
/// 한 작업으로 묶어야 목록 스냅샷과 "새 우편 도착"이 겹치지 않는다.
/// </summary>
public sealed class LoadMailboxRepository(User user, DateTime now) : IRepository
{
    private List<UserMailRow> _rows = new();

    public long Key => User.DbKey;

    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            "DELETE FROM t_user_mail WHERE user_id = @userId AND claimed_at IS NOT NULL AND claimed_at < @cutoff;",
            new { userId = User.Uid, cutoff = MailDb.ToDb(now - Mail.ClaimedRetention) });

        await MailDb.DeliverGlobalMailsAsync(connection, User.Uid, now);

        _rows = await connection.QueryAsync<UserMailRow>(
            @"SELECT mail_id, template_tid, gold, dia, items, character_tids, equip_tids, received_at, claimed_at
              FROM t_user_mail WHERE user_id = @userId ORDER BY mail_id;",
            new { userId = User.Uid });
    }

    public void Apply() => User.OnMailboxLoaded(_rows);
}

/// <summary>접속 중인 유저에게 전체 우편을 복사한다(발송 순간). 새로 받은 것만 도착 통지로 내려간다.</summary>
public sealed class DeliverGlobalMailRepository(User user, DateTime now) : IRepository
{
    private List<UserMailRow> _delivered = new();

    public long Key => User.DbKey;

    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection)
        => _delivered = await MailDb.DeliverGlobalMailsAsync(connection, User.Uid, now);

    public void Apply() => User.OnMailsArrived(_delivered);
}

/// <summary>
/// 개인 우편 한 통을 보낸다. 받는 사람이 접속 중이면 <c>recipient</c>에 넣어 도착을 알리고,
/// 아니면 DB에만 남긴다(다음 로그인 목록에 실린다). 작업 파티션은 보낸 사람(<c>owner</c>)의 것이다.
/// </summary>
public sealed class SendMailRepository(User owner, long recipientUid, User? recipient, int templateTid, MailAttachment attachment, DateTime now)
    : IRepository
{
    private long _mailId;

    public long Key => User.DbKey;

    public User User { get; } = owner;

    public long RecipientUid => recipientUid;

    public int TemplateTid => templateTid;

    public async Task ExecuteAsync(DbConnection connection)
        => _mailId = await MailDb.InsertMailAsync(connection, recipientUid, templateTid, attachment, now);

    public void Apply()
        => recipient?.OnMailsArrived(new List<UserMailRow> { MailDb.ToRow(_mailId, templateTid, attachment, now) });
}

/// <summary>전체 우편 한 줄을 남기고, 끝나면 접속 중인 유저 전원에게 복사를 요청한다.</summary>
public sealed class SendGlobalMailRepository(User owner, int templateTid, DateTime now, DateTime endsAt) : IRepository
{
    public long Key => User.DbKey;

    public User User { get; } = owner;

    public int TemplateTid => templateTid;

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            "INSERT INTO t_global_mail (template_tid, sent_at, ends_at) VALUES (@templateTid, @sentAt, @endsAt);",
            new { templateTid, sentAt = MailDb.ToDb(now), endsAt = MailDb.ToDb(endsAt) });
    }

    public void Apply()
    {
        foreach (var user in UserManager.Instance.All)
        {
            user.DeliverGlobalMails(now);
        }
    }
}

/// <summary>받은 우편 표시. 지급은 로직 스레드가 이미 끝냈다 — 실패하면 세션을 끊어 재접속 때 DB를 다시 읽는다.</summary>
public sealed class ClaimMailRepository(User user, IReadOnlyList<long> mailIds, DateTime claimedAt) : IRepository
{
    public long Key => User.DbKey;

    public User User { get; } = user;

    public IReadOnlyList<long> MailIds => mailIds;

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            "UPDATE t_user_mail SET claimed_at = @claimedAt WHERE user_id = @userId AND mail_id IN @ids;",
            new { userId = User.Uid, ids = mailIds, claimedAt = MailDb.ToDb(claimedAt) });
    }

    public void Apply()
    {
    }
}

/// <summary>받은 우편 한 통 삭제. 안 받은 우편은 로직 스레드가 이미 거절했다 — SQL에도 같은 조건을 건다.</summary>
public sealed class DeleteMailRepository(User user, long mailId) : IRepository
{
    public long Key => User.DbKey;

    public User User { get; } = user;

    public long MailId => mailId;

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            "DELETE FROM t_user_mail WHERE user_id = @userId AND mail_id = @mailId AND claimed_at IS NOT NULL;",
            new { userId = User.Uid, mailId });
    }

    public void Apply()
    {
    }
}
