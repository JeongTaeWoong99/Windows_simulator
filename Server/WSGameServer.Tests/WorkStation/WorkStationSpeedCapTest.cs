using GameData;
using WSGameServer;

namespace WSGameServer;

/// <summary>
/// 판정 최소 주기(T-102 · T-149) — 속도는 <c>JudgeCostUnits ÷ Constants.MinCycleMs</c>(1600ms)에서 잘린다.
/// 틀리면 판정이 슬롯 연출 한 바퀴(1.6초)보다 빨리 나와 클라 진행 바와 실제 판정이 어긋난다.
/// 넘친 속도는 버린다 — 산출·경험치로 바꾸지 않는다.
/// </summary>
public class WorkStationSpeedCapTest
{
    private static readonly DateTime Base = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    // Lv1 판정 비용 30,000,000 ÷ 1600 = 18750
    private const int Lv1Cap = 18750;

    // Lv2 판정 비용 90,000,000 ÷ 1600 = 56250
    private const long Lv2Cost = 90_000_000L;
    private const int Lv2Cap = 56250;

    public WorkStationSpeedCapTest() => GameTableFixture.EnsureLoaded();

    private static WorkStationSlot Slot(int speed, long? judgeCostUnits = null)
        => new(slotIndex: 0, IndustryType.Excavation, characterId: 100, Base, speed, judgeCostUnits: judgeCostUnits);

    [Fact]
    public void 상한을_넘는_속도는_상한으로_잘린다()
    {
        Slot(50_000).CurrentWorkSpeed.ShouldBe(Lv1Cap);
    }

    [Fact]
    public void 상한_아래_속도는_그대로다()
    {
        Slot(14_000).CurrentWorkSpeed.ShouldBe(14_000);
    }

    [Fact]
    public void 잘린_속도로는_판정_간격이_최소_주기_아래로_내려가지_않는다()
    {
        var slot = Slot(100_000);

        // 1599ms × 18750 = 29,981,250 < 30,000,000 · 1600ms × 18750 = 30,000,000 — 딱 1.6초에 첫 판정
        slot.ConsumeJudgeCount(Base.AddMilliseconds(1599)).ShouldBe(0);
        slot.ConsumeJudgeCount(Base.AddMilliseconds(1600)).ShouldBe(1);
    }

    [Fact]
    public void 상한은_레벨의_판정_비용을_따라간다()
    {
        Slot(100_000, Lv2Cost).CurrentWorkSpeed.ShouldBe(Lv2Cap);
    }

    [Fact]
    public void 비싼_레벨에서_싼_레벨로_옮기면_속도를_다시_자른다()
    {
        var slot = Slot(100_000, Lv2Cost);

        slot.Assign(IndustryType.Excavation, characterId: 100, Base, industryLevel: 1, judgeCostUnits: WorkStationSlot.JudgeCost);

        slot.CurrentWorkSpeed.ShouldBe(Lv1Cap);
    }

    [Fact]
    public void 싼_레벨에서_잘렸던_속도는_비싼_레벨로_옮기면_되살아난다()
    {
        // 잘린 값을 저장하면 Lv2로 옮겨도 18750에 묶인다 — 원래 속도 100,000이 Lv2 상한 56250까지 살아야 한다.
        var slot = Slot(100_000);

        slot.Assign(IndustryType.Excavation, characterId: 100, Base, industryLevel: 2, judgeCostUnits: Lv2Cost);

        slot.CurrentWorkSpeed.ShouldBe(Lv2Cap);
    }

    [Fact]
    public void 상한에_걸린_채_원래_속도만_오르면_바뀌지_않았다고_본다()
    {
        // 확정 속도가 그대로면 슬롯 푸시를 다시 보낼 이유가 없다.
        var slot = Slot(50_000);

        slot.ApplyWorkSpeed(60_000).ShouldBeFalse();
        slot.CurrentWorkSpeed.ShouldBe(Lv1Cap);
    }

    [Fact]
    public void 내역은_자르기_전_값_그대로_나간다()
    {
        // 클라가 "속도는 이만큼인데 상한에 걸렸다"를 그린다 — 배수 ×20이 그대로 보여야 한다.
        var slot = Slot(1000);

        slot.ApplyWorkSpeed(new WorkSpeedBreakdown(1000, 0, 0, 0, 0, 20_000));

        var info = slot.ToInfo();
        info.GatherSpeedPermille.ShouldBe(20_000);
        info.CurrentWorkSpeed.ShouldBe(Lv1Cap);
    }
}
