using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 완료 탭 조회(T-156) — 요청을 DB 조회로 넘기고, 결과를 클라 형식으로 옮긴다. 빈도 제한은 검색과 같은 통이다.
/// </summary>
public class UserAuctionHistoryTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    public UserAuctionHistoryTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) NewUser()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    [Fact]
    public void 요청한_구분_기간_커서로_조회한다()
    {
        var (user, b) = NewUser();

        user.TryGetAuctionHistory(new C_AuctionHistoryRequest
        {
            Type = EAuctionHistoryType.Sold, Days = 7, CursorHistoryId = 40, PageSize = 20, MarkSeen = true,
        }, Now);

        var posted = b.DB.PostedOf<AuctionHistoryRepository>().Single();
        (posted.Type, posted.Days, posted.Cursor, posted.PageSize, posted.MarkSeen).ShouldBe((EAuctionHistoryType.Sold, 7, 40L, 20, true));
    }

    [Fact]
    public void 결과를_클라_형식으로_옮긴다()
    {
        var (user, b) = NewUser();
        var row = new AuctionHistoryRow
        {
            history_id = 9, type = 1, kind = 2, tid = 1001, count = 1, unit_price = 500, amount = 475, fee = 25, enchant_grade = 4,
            created_at = MailDb.ToDb(new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc)),
        };

        user.OnAuctionHistoryLoaded(new AuctionHistoryPage(new List<AuctionHistoryRow> { row }, HasMore: true, UnseenCount: 3));

        var res = b.Channel.SentOf<S_AuctionHistoryResponse>().Single();
        (res.Result, res.HasMore, res.UnseenCount).ShouldBe((EResultCode.Ok, true, 3));
        var e = res.Entries!.Single();
        (e.HistoryId, e.Type, e.Kind, e.Tid, e.Count, e.UnitPrice, e.Amount, e.Fee, e.EnchantGrade)
            .ShouldBe((9L, EAuctionHistoryType.Sold, EAuctionKind.Equip, 1001, 1, 500L, 475L, 25L, 4));
        e.AtUnixMs.ShouldBe(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
    }

    [Fact]
    public void 너무_잦으면_조회하지_않는다()
    {
        var (user, b) = NewUser();

        for (var i = 0; i <= AuctionRules.SearchBurst; i++)
        {
            user.TryGetAuctionHistory(new C_AuctionHistoryRequest { PageSize = 0 }, Now);
        }

        b.DB.PostedOf<AuctionHistoryRepository>().Count.ShouldBe(AuctionRules.SearchBurst);
        b.Channel.SentOf<S_AuctionHistoryResponse>().Last().Result.ShouldBe(EResultCode.AuctionTooManyRequests);
    }
}
