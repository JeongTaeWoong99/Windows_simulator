namespace WSGameServer;

/// <summary>
/// 우편 SQL을 실제 SQLite에 대고 검증한다. 전체 우편은 <b>한 유저에게 한 번만</b> 복사돼야 하고,
/// 받은 우편만 7일 뒤 지워져야 한다 — 틀리면 보상이 두 번 나가거나 안 받은 보상이 사라진다.
/// </summary>
public class MailRepositoryTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int OperationTemplate = 1;   // 점검 보상 — 골드 1000 · 나무 상자 3

    private readonly SqliteFixture _db = new();

    public MailRepositoryTest()
    {
        GameTableFixture.EnsureLoaded();
        MailCatalog.Instance.LoadAll();   // 전체 우편 복사는 DB 스레드에서 전역 카탈로그를 읽는다
        _db.CreateMailTables();
    }

    public void Dispose() => _db.Dispose();

    private static User NewUser() => new TestUserBuilder().Build(uid: 7);

    private long Count(string sql) => (long)_db.Query(sql)!;

    private void InsertGlobal(DateTime endsAt)
        => _db.Execute($"INSERT INTO t_global_mail (template_tid, ends_at) VALUES ({OperationTemplate}, '{MailDb.ToDb(endsAt)}')");

    [Fact]
    public async Task 전체_우편은_한_유저에게_한_번만_복사된다()
    {
        InsertGlobal(Now.AddDays(14));
        var user = NewUser();

        await new DeliverGlobalMailRepository(user, Now).ExecuteAsync(new DbConnection(_db.Connection));
        await new DeliverGlobalMailRepository(user, Now).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_mail WHERE user_id = 7").ShouldBe(1);
    }

    [Fact]
    public async Task 기간이_끝난_전체_우편은_복사하지_않는다()
    {
        InsertGlobal(Now.AddSeconds(-1));

        await new DeliverGlobalMailRepository(NewUser(), Now).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_mail").ShouldBe(0);
    }

    [Fact]
    public async Task 복사된_우편에는_템플릿_첨부가_적힌다()
    {
        InsertGlobal(Now.AddDays(14));

        await new DeliverGlobalMailRepository(NewUser(), Now).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_mail WHERE gold = 1000 AND items = '[[100007,3]]'").ShouldBe(1);
    }

    [Fact]
    public async Task 로그인하면_7일_지난_받은_우편만_지운다()
    {
        // 받은 지 8일(삭제) · 받은 지 6일(남김) · 안 받음 30일 전 도착(남김 — 안 받은 우편은 만료되지 않는다)
        _db.Execute($@"
            INSERT INTO t_user_mail (mail_id, user_id, template_tid, received_at, claimed_at) VALUES
              (1, 7, 1, '{MailDb.ToDb(Now.AddDays(-9))}',  '{MailDb.ToDb(Now.AddDays(-8))}'),
              (2, 7, 1, '{MailDb.ToDb(Now.AddDays(-7))}',  '{MailDb.ToDb(Now.AddDays(-6))}'),
              (3, 7, 1, '{MailDb.ToDb(Now.AddDays(-30))}', NULL);");

        await new LoadMailboxRepository(NewUser(), Now).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_mail WHERE mail_id = 1").ShouldBe(0);
        Count("SELECT COUNT(*) FROM t_user_mail WHERE mail_id IN (2, 3)").ShouldBe(2);
    }

    [Fact]
    public async Task 안_받은_우편은_삭제_SQL에서도_지워지지_않는다()
    {
        _db.Execute("INSERT INTO t_user_mail (mail_id, user_id, template_tid) VALUES (1, 7, 1)");

        await new DeleteMailRepository(NewUser(), 1).ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_mail").ShouldBe(1);
    }
}
