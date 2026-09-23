using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>안 받은 우편 상한 — 넘침 보관만 이 선에서 멈춘다(운영 우편은 무관). 공용 상수 시트(T-077)가 서면 옮긴다.</summary>
    public const int MailboxCapacity = 100;

    // 우편함. 도착 순(= mail_id 순)이 모두 받기의 순서다.
    private readonly SortedDictionary<long, Mail> _mails = new();

    // DB가 mail_id를 주기 전의 넘침 우편 수. 세지 않으면 응답 전 연속 개봉이 상한을 넘긴다.
    private int _pendingOverflowMails;

    public IReadOnlyCollection<Mail> Mails => _mails.Values;

    public int UnclaimedMailCount => _mails.Values.Count(m => !m.IsClaimed);

    public bool TryGetMail(long mailId, out Mail mail) => _mails.TryGetValue(mailId, out mail!);

    /// <summary>넘침 우편을 한 통 더 받을 수 있는가 — 안 받은 우편 + 저장 대기 중인 넘침 우편이 상한 미만</summary>
    public bool HasMailboxRoom => UnclaimedMailCount + _pendingOverflowMails < MailboxCapacity;

    /// <summary>창고에 안 들어간 보상을 우편 1통으로 보관한다. 호출자가 <see cref="HasMailboxRoom"/>을 먼저 확인한다.</summary>
    public void StoreOverflowMail(MailAttachment attachment, DateTime now)
    {
        _pendingOverflowMails++;
        PostDBTask(new StoreOverflowMailRepository(this, attachment, now));
        ServerLog.Info("우편", $"넘침 보관 Uid={Uid} 아이템 {attachment.Items.Count}종 · 골드 {attachment.Gold}");
    }

    public void OnOverflowMailStored(UserMailRow row)
    {
        _pendingOverflowMails = Math.Max(0, _pendingOverflowMails - 1);
        OnMailsArrived(new List<UserMailRow> { row });
    }

    /// <summary>로그인 때 우편함을 연다. 정리·전체 우편 복사·조회를 DB가 한 번에 하고 <see cref="OnMailboxLoaded"/>로 돌아온다.</summary>
    private void LoadMailbox(DateTime now) => PostDBTask(new LoadMailboxRepository(this, now));

    public void OnMailboxLoaded(IReadOnlyList<UserMailRow> rows)
    {
        _mails.Clear();
        foreach (var row in rows)
        {
            AddMail(row);
        }

        Send(new S_MailListResponse { Mails = _mails.Values.Select(m => m.ToInfo()).ToList() });
    }

    /// <summary>접속 중에 새 우편이 들어왔다. 빈 목록이면 알리지 않는다(전체 우편을 이미 받은 유저).</summary>
    public void OnMailsArrived(IReadOnlyList<UserMailRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var arrived = rows.Select(AddMail).ToList();
        Send(new S_MailArrivedResponse { Mails = arrived.Select(m => m.ToInfo()).ToList() });
    }

    /// <summary>발송된 전체 우편을 이 유저에게 복사한다. 로그인 전이면 로그인 때 복사되므로 건너뛴다.</summary>
    public void DeliverGlobalMails(DateTime now)
    {
        if (!IsLoggedIn)
        {
            return;
        }

        PostDBTask(new DeliverGlobalMailRepository(this, now));
    }

    private Mail AddMail(UserMailRow row)
    {
        var mail = new Mail(
            row.mail_id,
            row.template_tid,
            MailAttachment.FromRow(row),
            MailDb.FromDb(row.received_at),
            row.claimed_at is null ? null : MailDb.FromDb(row.claimed_at));

        _mails[mail.Id] = mail;
        return mail;
    }

    /// <summary>
    /// 우편을 받는다. <paramref name="mailId"/>가 0이면 모두 받기 — 오래된 순으로 받다가 창고에 안 들어가는 우편에서 멈춘다.
    /// 한 통은 원자적이다: 첨부가 전부 들어가지 않으면 아무것도 지급하지 않는다(<see cref="EResultCode.StorageFull"/>).
    /// </summary>
    public void TryClaimMail(long mailId, DateTime now)
    {
        var claimed = new List<long>();
        var changes = new List<ItemChangeInfo>();

        var result = mailId == 0 ? ClaimAll(now, claimed, changes) : ClaimOne(mailId, now, claimed, changes);

        if (claimed.Count > 0)
        {
            PostDBTask(new ClaimMailRepository(this, claimed, now));
        }

        Send(new S_MailClaimResponse
        {
            Result          = result,
            ClaimedMailIds  = claimed,
            RemainingCount  = UnclaimedMailCount,
            ItemChangeInfos = changes,
        });
    }

    private EResultCode ClaimOne(long mailId, DateTime now, List<long> claimed, List<ItemChangeInfo> changes)
    {
        if (!_mails.TryGetValue(mailId, out var mail))
        {
            return EResultCode.MailNotFound;
        }

        if (mail.IsClaimed)
        {
            return EResultCode.MailAlreadyClaimed;
        }

        return TryGrantMail(mail, now, claimed, changes);
    }

    private EResultCode ClaimAll(DateTime now, List<long> claimed, List<ItemChangeInfo> changes)
    {
        foreach (var mail in _mails.Values.Where(m => !m.IsClaimed).ToList())
        {
            var result = TryGrantMail(mail, now, claimed, changes);
            if (result != EResultCode.Ok)
            {
                return result;
            }
        }

        return EResultCode.Ok;
    }

    // 칸 검사 → 받음 표시 → 지급. 지급은 기존 경로(가챠·치트와 같은 함수)라 저장·통지까지 같다.
    private EResultCode TryGrantMail(Mail mail, DateTime now, List<long> claimed, List<ItemChangeInfo> changes)
    {
        var a = mail.Attachment;
        if (!HasStorageFor(a.Items.Select(i => i.Tid), a.CharacterTids.Count, a.EquipTids.Count + a.Equips.Count))
        {
            return EResultCode.StorageFull;
        }

        mail.MarkClaimed(now);
        claimed.Add(mail.Id);

        foreach (var (tid, count) in a.Items)
        {
            changes.Add(GainItem(tid, count));
        }

        if (a.Gold > 0)
        {
            GainGold(a.Gold);
        }

        if (a.Dia > 0)
        {
            GainDia(a.Dia);
        }

        if (a.CharacterTids.Count > 0)
        {
            GrantGachaCharacters(a.CharacterTids);
        }

        foreach (var equipTid in a.EquipTids)
        {
            GrantEquip(equipTid);
        }

        foreach (var equip in a.Equips)
        {
            UnlockMailEquip(equip);
        }

        ServerLog.Info("우편", $"수령 Uid={Uid} MailId={mail.Id} Template={mail.TemplateTid}");
        return EResultCode.Ok;
    }

    /// <summary>받은 우편을 지운다. 안 받은 우편은 지울 수 없다 — 보상을 실수로 버리지 않게 막는다.</summary>
    public void TryDeleteMail(long mailId)
    {
        var result = DeleteMail(mailId);
        Send(new S_MailDeleteResponse { Result = result, MailId = mailId });
    }

    private EResultCode DeleteMail(long mailId)
    {
        if (!_mails.TryGetValue(mailId, out var mail))
        {
            return EResultCode.MailNotFound;
        }

        if (!mail.IsClaimed)
        {
            return EResultCode.MailNotClaimed;
        }

        _mails.Remove(mailId);
        PostDBTask(new DeleteMailRepository(this, mailId));
        return EResultCode.Ok;
    }

    /// <summary>
    /// 운영 우편을 보낸다(지금은 치트 전용 — 운영툴이 생기면 같은 함수를 쓴다).
    /// <paramref name="recipientUid"/>가 0이면 전체 우편 — 템플릿의 <c>PeriodDays</c> 동안 로그인한 계정이 받는다.
    /// </summary>
    public (EResultCode, string) SendOperationMail(int templateTid, long recipientUid, DateTime now)
    {
        if (!_mailCatalog.TryGet(templateTid, out var template))
        {
            return (EResultCode.InvalidCheatArgs, $"MailTemplateTable에 없는 TID {templateTid}");
        }

        if (recipientUid < 0)
        {
            return (EResultCode.InvalidCheatArgs, $"받는 UID는 0(전체) 이상 — {recipientUid}");
        }

        if (recipientUid == 0)
        {
            if (template.PeriodDays <= 0)
            {
                return (EResultCode.InvalidCheatArgs, $"템플릿 {templateTid}은 PeriodDays가 0 — 전체 우편으로 보낼 수 없다");
            }

            PostDBTask(new SendGlobalMailRepository(this, templateTid, now, now.AddDays(template.PeriodDays)));
            return (EResultCode.Ok, $"전체 우편 {templateTid} 발송 · {template.PeriodDays}일");
        }

        UserManager.Instance.TryGetUserByUid(recipientUid, out var recipient);
        PostDBTask(new SendMailRepository(this, recipientUid, recipient, templateTid, MailCatalog.AttachmentOf(template), now));
        return (EResultCode.Ok, $"우편 {templateTid} → Uid {recipientUid}{(recipient is null ? " (오프라인 — 다음 로그인)" : "")}");
    }
}
