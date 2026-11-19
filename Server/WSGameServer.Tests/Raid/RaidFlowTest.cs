using MikaProtocol;

namespace WSGameServer;

/// <summary>실제 SQLite에 레이드 도전과 우편 수령이 연결되고, 재전송해도 보상은 한 번 지급되는지 검증한다.</summary>
public class RaidFlowTest
{
    [Fact]
    public void 첫_레이드_재전송에도_티켓_한_장과_보상_우편_한_통만_처리한다()
    {
        using var db = new SqliteFixture();
        db.CreateSchema();
        GameTableFixture.EnsureLoaded();
        var raids = new RaidCatalog();
        raids.LoadAll();
        var logic = new FakeLogicExecutor { RunImmediately = true };
        var queue = new SqliteDBQueue(db, logic);
        var builder = new TestUserBuilder { Raids = raids, QueueOverride = queue, ExecutorOverride = logic };
        var user = builder.Build();
        queue.Post(new LoginRepository(user));

        var starter = builder.Channel.SentOf<S_CharacterListResponse>().Last().Characters!.ShouldHaveSingleItem();
        var now = GameClock.Instance.UtcNow;
        user.SendRaidStatus(now);
        var opened = builder.Channel.SentOf<S_RaidStatusResponse>().ShouldHaveSingleItem();
        (opened.Unlocked, opened.TicketCount).ShouldBe((true, 3));
        user.SaveRaidParty([starter.CharacterId], now);
        builder.Channel.SentOf<S_RaidSavePartyResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);

        var requestId = Guid.Parse("6f0b2b85-a816-4f39-ba3b-164293136051");
        user.ChallengeRaid(1, requestId, now);
        user.ChallengeRaid(1, requestId, now);

        var responses = builder.Channel.SentOf<S_RaidChallengeResponse>();
        responses.Count.ShouldBe(2);
        foreach (var response in responses)
        {
            (response.Result, response.RequestId, response.Outcome, response.DurationMs, response.TicketCount, response.RewardBoxItemTID)
                .ShouldBe((EResultCode.Ok, requestId, ERaidBattleOutcome.Victory, 36_800, 2, 100_075));
        }

        var raidMail = builder.Channel.SentOf<S_MailArrivedResponse>().ShouldHaveSingleItem().Mails!.ShouldHaveSingleItem();
        (raidMail.TemplateTid, raidMail.Items!.ShouldHaveSingleItem().ItemId)
            .ShouldBe((MailCatalog.RaidRewardTemplateTid, 100_075));
        user.TryClaimMail(raidMail.MailId, now);
        new GachaService(new GachaPoolCatalog(), raids).OpenBox(user, 100_075, 1, now);

        var claim = builder.Channel.SentOf<S_MailClaimResponse>().ShouldHaveSingleItem();
        (claim.Result, claim.RemainingCount).ShouldBe((EResultCode.Ok, 0));
        var box = builder.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem();
        (box.Result, user.Gold, user.GetItemCount(100_075)).ShouldBe((EResultCode.Ok, 250L, 0));
    }
}
