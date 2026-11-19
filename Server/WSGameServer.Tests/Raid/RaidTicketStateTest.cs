namespace WSGameServer;

/// <summary>한국시간 자정에만 충전하고, 최대치를 넘긴 충전은 나중에 되살리지 않는다.</summary>
public class RaidTicketStateTest
{
    private static readonly DateTimeOffset BeforeMidnight = new(2026, 11, 18, 14, 59, 59, TimeSpan.Zero);

    [Fact]
    public void 첫_해금은_티켓_세_장으로_시작한다()
    {
        RaidTicketState.FirstUnlock(BeforeMidnight).ShouldBe(new RaidTicketState(3, new DateOnly(2026, 11, 18)));
    }

    [Fact]
    public void 한국시간_자정이_되면_한_장_충전된다()
    {
        var state = new RaidTicketState(1, new DateOnly(2026, 11, 18));

        (state.Recharge(BeforeMidnight).Count, state.Recharge(BeforeMidnight.AddSeconds(1)).Count)
            .ShouldBe((1, 2));
    }

    [Fact]
    public void 오프라인_충전은_세_장에서_멈추고_초과분은_보존하지_않는다()
    {
        var state = new RaidTicketState(0, new DateOnly(2026, 11, 10));
        var afterVictory = state.Recharge(BeforeMidnight).ConsumeAfterVictory();

        afterVictory.Recharge(BeforeMidnight).ShouldBe(new RaidTicketState(2, new DateOnly(2026, 11, 18)));
    }

    [Fact]
    public void 게임_시계가_과거로_돌아가도_충전_기준일을_되돌리지_않는다()
    {
        var state = new RaidTicketState(1, new DateOnly(2026, 11, 19));

        state.Recharge(BeforeMidnight).ShouldBe(state);
    }

    [Fact]
    public void 빈_티켓은_소모할_수_없다()
    {
        var state = new RaidTicketState(0, new DateOnly(2026, 11, 18));

        Should.Throw<InvalidOperationException>(() => state.ConsumeAfterVictory());
    }
}
