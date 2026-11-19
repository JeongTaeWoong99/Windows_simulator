namespace WSGameServer;

/// <summary>레이드 판정과 로그를 검증해 서버 결과와 클라 재생의 기준을 고정한다.</summary>
public class RaidBattleSimulatorTest
{
    [Fact]
    public void 시작_캐릭터_한_명이_첫_보스를_이긴다()
    {
        RaidBattleResult result = RaidBattleSimulator.Simulate([new(10, 100, 1_600)], new(8, 230, 3_200));

        // 내 공격 23회가 36.8초에 끝나고, 그 전에 보스에게 11회 공격받는다.
        (result.Outcome, result.DurationMs, result.RemainingPartyHealth[0], result.RemainingBossHealth)
            .ShouldBe((RaidBattleOutcome.Victory, 36_800, 12, 0));
    }

    [Fact]
    public void 같은_시각이면_편성_순서대로_공격하고_죽은_보스는_공격하지_않는다()
    {
        var result = RaidBattleSimulator.Simulate([new(3, 10, 1_000), new(4, 10, 1_000), new(5, 10, 1_000)],
                                                  new(10, 9, 1_000));

        result.Hits.ShouldBe(new RaidBattleHit[]
        {
            new(1_000, 0, 3, 3, 6),
            new(1_000, 1, 3, 4, 2),
            new(1_000, 2, 3, 2, 0),
        });
    }

    [Fact]
    public void 보스는_선두를_쓰러뜨린_뒤_다음_생존자를_공격한다()
    {
        var result = RaidBattleSimulator.Simulate([new(20, 5, 1_500), new(20, 5, 3_000)], new(5, 50, 1_000));

        result.Hits.ShouldBe(new RaidBattleHit[]
        {
            new(1_000, 2, 0, 5, 0),
            new(2_000, 2, 1, 5, 0),
        });
        result.Outcome.ShouldBe(RaidBattleOutcome.PartyDefeated);
    }

    [Theory]
    [InlineData(60_000, RaidBattleOutcome.Victory, 1)]
    [InlineData(60_001, RaidBattleOutcome.TimeLimitReached, 0)]
    public void 제한_시간_안에_도달한_타격만_판정한다(int intervalMs, RaidBattleOutcome expectedOutcome, int expectedHits)
    {
        var result = RaidBattleSimulator.Simulate([new(10, 10, intervalMs)], new(1, 10, 66_000));

        (result.Outcome, result.DurationMs, result.Hits.Count).ShouldBe((expectedOutcome, 60_000, expectedHits));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void 편성은_한_명부터_세_명까지만_허용한다(int size)
    {
        RaidFighterStats[] party = Enumerable.Repeat(new RaidFighterStats(1, 1, 1_000), size).ToArray();

        Should.Throw<ArgumentOutOfRangeException>(() => RaidBattleSimulator.Simulate(party, new(1, 1, 1_000)));
    }

    [Fact]
    public void 공격_주기가_0인_참가자는_받지_않는다()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RaidBattleSimulator.Simulate([new(1, 1, 0)], new(1, 1, 1_000)));
    }
}
