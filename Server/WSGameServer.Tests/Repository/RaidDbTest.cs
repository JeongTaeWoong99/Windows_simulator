namespace WSGameServer;

/// <summary>재접속한 뒤에도 레이드 편성과 지급 이력이 남고 티켓 차감·보상 저장이 함께 처리되도록 검증한다.</summary>
public class RaidDbTest : IDisposable
{
    private const long UserId = 7;
    private const string RequestId = "00000000000000000000000000000001";
    private const string Result = """{"won":true}""";
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteFixture _db = new();

    public RaidDbTest()
    {
        GameTableFixture.EnsureLoaded();
        _db.CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task 레이드_상태를_다시_불러오면_티켓과_편성_순서가_복원된다()
    {
        var db = new DbConnection(_db.Connection);
        (await RaidDb.LoadAsync(db, UserId)).ShouldBeNull();

        var tickets = new RaidTicketState(2, new DateOnly(2026, 11, 19));
        await RaidDb.SaveAsync(db, UserId, new RaidProfile(tickets, [41, 37]));

        var loaded = (await RaidDb.LoadAsync(db, UserId)).ShouldNotBeNull();
        (loaded.Tickets, string.Join(",", loaded.PartyCharacterIds)).ShouldBe((tickets, "41,37"));
    }

    [Fact]
    public async Task 같은_도전으로는_상자를_한_번만_보내고_정산_결과를_다시_읽는다()
    {
        var db = new DbConnection(_db.Connection);
        var tickets = new RaidTicketState(3, new DateOnly(2026, 11, 19));
        await RaidDb.SaveAsync(db, UserId, new RaidProfile(tickets, [41]));

        var mail = new MailAttachment(0, [(101, 1)], [], []);
        var profile = new RaidProfile(tickets.ConsumeAfterVictory(), [41]);
        var delivered = (await RaidDb.CommitAttemptAsync(db, UserId, profile, RequestId, Result, mail, Now)).ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            RaidDb.CommitAttemptAsync(db, UserId, profile with { Tickets = new RaidTicketState(1, new DateOnly(2026, 11, 19)) },
                                      RequestId, Result, mail, Now));

        var loaded = (await RaidDb.LoadAsync(db, UserId)).ShouldNotBeNull();
        var storedResult = await RaidDb.FindAttemptAsync(db, UserId, RequestId);
        var mailCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM t_user_mail WHERE user_id = @userId;",
                                                         new { userId = UserId });
        (loaded.Tickets.Count, storedResult, mailCount, delivered.items).ShouldBe((2, Result, 1, "[[101,1]]"));
    }

    [Fact]
    public async Task 우편함이_가득_차면_티켓과_도전_이력은_함께_되돌린다()
    {
        var db = new DbConnection(_db.Connection);
        var initial = new RaidProfile(new RaidTicketState(3, new DateOnly(2026, 11, 19)), [41]);
        await RaidDb.SaveAsync(db, UserId, initial);

        for (int count = 0; count < User.MailboxCapacity; count++)
        {
            await MailDb.InsertMailAsync(db, UserId, MailCatalog.OverflowTemplateTid, MailAttachment.Empty, Now);
        }

        var changed = initial with { Tickets = initial.Tickets.ConsumeAfterVictory() };
        await Should.ThrowAsync<RaidMailboxFullException>(() =>
            RaidDb.CommitAttemptAsync(db, UserId, changed, RequestId, Result, new MailAttachment(0, [(101, 1)], [], []), Now));

        var loaded = (await RaidDb.LoadAsync(db, UserId)).ShouldNotBeNull();
        var receipt = await RaidDb.FindAttemptAsync(db, UserId, RequestId);
        (loaded.Tickets.Count, receipt).ShouldBe((3, null));
    }
}
