using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 우편 수령·삭제·발송 검증. <b>한 통은 전부 받거나 아무것도 안 받는다</b>(창고가 모자라면 StorageFull) —
/// 반쯤 받은 상태가 생기면 보상이 사라지거나 두 번 나간다. 안 받은 우편은 지울 수 없다.
/// </summary>
public class UserMailTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int FishTid      = 10001;
    private const int CharacterTid = 1002;
    private const int SwordTid     = 1101;

    // 운영 템플릿 1 = 점검 보상(PeriodDays 14) · 2 = 넘침 보관(PeriodDays 0)
    private const int OperationTemplate = 1;
    private const int OverflowTemplate  = 2;

    public UserMailTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWithMails(params UserMailRow[] rows)
    {
        var b    = new TestUserBuilder();
        var user = b.Build(uid: 7);
        user.OnMailboxLoaded(rows);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static UserMailRow Mail(long id, long gold = 0, string items = "[]", string characters = "[]", string equips = "[]", bool claimed = false)
    {
        return new UserMailRow
        {
            mail_id = id, template_tid = OperationTemplate, gold = gold,
            items = items, character_tids = characters, equip_tids = equips,
            received_at = MailDb.ToDb(Now.AddMinutes(id)),
            claimed_at  = claimed ? MailDb.ToDb(Now) : null,
        };
    }

    private static S_MailClaimResponse ClaimResponse(TestUserBuilder b)
        => b.Channel.SentOf<S_MailClaimResponse>().ShouldHaveSingleItem();

    // 창고 자원 칸을 kinds종으로 채운다 — 표에 없는 TID여도 칸은 종류 수만 본다.
    private static void FillItemKinds(User user, int kinds)
    {
        for (var i = 0; i < kinds; i++)
        {
            user.GainItem(900_000 + i, 1);
        }
    }

    // ─────────────────────── 목록 ───────────────────────

    [Fact]
    public void 로그인하면_우편함_전체가_내려간다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build(uid: 7);

        user.OnMailboxLoaded(new[] { Mail(1), Mail(2, claimed: true) });

        var list = b.Channel.SentOf<S_MailListResponse>().ShouldHaveSingleItem();
        list.Mails!.Select(m => m.MailId).ShouldBe(new long[] { 1, 2 });
        list.Mails[1].ClaimedAtUnixMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void 새로_받은_우편이_없으면_도착을_알리지_않는다()
    {
        var (user, b) = UserWithMails();

        user.OnMailsArrived(Array.Empty<UserMailRow>());

        b.Channel.SentOf<S_MailArrivedResponse>().ShouldBeEmpty();
    }

    // ─────────────────────── 수령 ───────────────────────

    [Fact]
    public void 우편을_받으면_첨부가_전부_지급되고_받음으로_기록된다()
    {
        var (user, b) = UserWithMails(Mail(1, gold: 1000, items: $"[[{FishTid},3]]", characters: $"[{CharacterTid}]", equips: $"[{SwordTid}]"));

        user.TryClaimMail(1, Now);

        var res = ClaimResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.ClaimedMailIds.ShouldBe(new long[] { 1 });
        res.RemainingCount.ShouldBe(0);
        user.Gold.ShouldBe(1000);
        user.GetItemCount(FishTid).ShouldBe(3);
        b.DB.PostedOf<GrantCharacterRepository>().ShouldHaveSingleItem().CharacterTids.ShouldBe(new[] { CharacterTid });
        b.DB.PostedOf<GrantEquipRepository>().ShouldHaveSingleItem();
        b.DB.PostedOf<ClaimMailRepository>().ShouldHaveSingleItem().MailIds.ShouldBe(new long[] { 1 });
        user.TryGetMail(1, out var mail).ShouldBeTrue();
        mail.IsClaimed.ShouldBeTrue();
    }

    [Fact]
    public void 창고가_모자라면_그_우편은_아무것도_지급하지_않는다()
    {
        // 200종이 찬 창고에 새 종류(붕어) — 골드도 함께 막힌다. 한 통은 원자적이다.
        var (user, b) = UserWithMails(Mail(1, gold: 1000, items: $"[[{FishTid},3]]"));
        FillItemKinds(user, 200);
        b.DB.Posted.Clear();

        user.TryClaimMail(1, Now);

        ClaimResponse(b).Result.ShouldBe(EResultCode.StorageFull);
        user.Gold.ShouldBe(0);
        user.GetItemCount(FishTid).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 이미_받은_우편은_다시_받을_수_없다()
    {
        var (user, b) = UserWithMails(Mail(1, gold: 1000, claimed: true));

        user.TryClaimMail(1, Now);

        ClaimResponse(b).Result.ShouldBe(EResultCode.MailAlreadyClaimed);
        user.Gold.ShouldBe(0);
    }

    [Fact]
    public void 없는_우편은_MailNotFound다()
    {
        var (user, b) = UserWithMails();

        user.TryClaimMail(99, Now);

        ClaimResponse(b).Result.ShouldBe(EResultCode.MailNotFound);
    }

    [Fact]
    public void 모두_받기는_오래된_순으로_받다가_막힌_우편에서_멈춘다()
    {
        // 창고 199종: 1번(붕어 — 새 종류, 200종이 됨) 통과 → 2번(새 종류 하나 더) 막힘 → 3번(골드뿐)은 시도하지 않는다
        var (user, b) = UserWithMails(
            Mail(1, items: $"[[{FishTid},1]]"),
            Mail(2, items: "[[10002,1]]"),
            Mail(3, gold: 500));
        FillItemKinds(user, 199);

        user.TryClaimMail(0, Now);

        var res = ClaimResponse(b);
        res.Result.ShouldBe(EResultCode.StorageFull);
        res.ClaimedMailIds.ShouldBe(new long[] { 1 });
        res.RemainingCount.ShouldBe(2);
        user.Gold.ShouldBe(0);
    }

    [Fact]
    public void 모두_받기는_받은_우편을_건너뛴다()
    {
        var (user, b) = UserWithMails(Mail(1, gold: 100, claimed: true), Mail(2, gold: 200));

        user.TryClaimMail(0, Now);

        var res = ClaimResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.ClaimedMailIds.ShouldBe(new long[] { 2 });
        user.Gold.ShouldBe(200);
    }

    // ─────────────────────── 삭제 ───────────────────────

    [Fact]
    public void 안_받은_우편은_지울_수_없다()
    {
        var (user, b) = UserWithMails(Mail(1));

        user.TryDeleteMail(1);

        b.Channel.SentOf<S_MailDeleteResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.MailNotClaimed);
        user.TryGetMail(1, out _).ShouldBeTrue();
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 받은_우편은_지운다()
    {
        var (user, b) = UserWithMails(Mail(1, claimed: true));

        user.TryDeleteMail(1);

        b.Channel.SentOf<S_MailDeleteResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.TryGetMail(1, out _).ShouldBeFalse();
        b.DB.PostedOf<DeleteMailRepository>().ShouldHaveSingleItem().MailId.ShouldBe(1);
    }

    // ─────────────────────── 발송(치트) ───────────────────────

    [Fact]
    public void 치트로_개인_우편을_보내면_받는_UID로_저장을_요청한다()
    {
        var (user, b) = UserWithMails();

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SendMail, Arg1 = OperationTemplate, Arg2 = 42 }, Now);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        var send = b.DB.PostedOf<SendMailRepository>().ShouldHaveSingleItem();
        send.RecipientUid.ShouldBe(42);
        send.TemplateTid.ShouldBe(OperationTemplate);
    }

    [Fact]
    public void 접속_중인_유저에게_보내면_그_유저의_우편함으로_도착한다()
    {
        // 실측(2026-09-22)에서 발견 — UserManager는 Uid가 아니라 내부 Key로 유저를 찾아서, 접속 중인데도 오프라인으로 떨어졌다.
        var (user, b) = UserWithMails();
        UserManager.Instance.JoinUser(user);

        try
        {
            user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SendMail, Arg1 = OperationTemplate, Arg2 = user.Uid }, Now);

            b.DB.PostedOf<SendMailRepository>().ShouldHaveSingleItem().Recipient.ShouldBeSameAs(user);
        }
        finally
        {
            UserManager.Instance.LeaveUser(user);
        }
    }

    [Fact]
    public void 치트로_전체_우편을_보내면_전체_우편_한_줄을_요청한다()
    {
        var (user, b) = UserWithMails();

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SendMail, Arg1 = OperationTemplate, Arg2 = 0 }, Now);

        b.DB.PostedOf<SendGlobalMailRepository>().ShouldHaveSingleItem().TemplateTid.ShouldBe(OperationTemplate);
    }

    [Fact]
    public void 기간이_0인_템플릿은_전체_우편으로_보낼_수_없다()
    {
        var (user, b) = UserWithMails();

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SendMail, Arg1 = OverflowTemplate, Arg2 = 0 }, Now);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.PostedOf<SendGlobalMailRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 없는_템플릿은_보낼_수_없다()
    {
        var (user, b) = UserWithMails();

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SendMail, Arg1 = 9999, Arg2 = 42 }, Now);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }

    // ─────────────────────── 템플릿 검증 ───────────────────────

    [Fact]
    public void 아이템과_수량_개수가_다른_템플릿은_기동에서_막는다()
    {
        var catalog = new MailCatalog();

        Should.Throw<InvalidDataException>(() => catalog.Load(new[]
        {
            new MailTemplateTableRow { MailTemplateTID = 1, ItemTIDs = new[] { FishTid, 10002 }, ItemCounts = new[] { 1 } },
        }));
    }
}
