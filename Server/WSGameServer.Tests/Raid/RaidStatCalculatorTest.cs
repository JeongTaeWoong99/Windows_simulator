namespace WSGameServer;

/// <summary>인챈트 배율이 최종 고정 가산에 중복 적용되지 않게 한다.</summary>
public class RaidStatCalculatorTest
{
    [Fact]
    public void 인챈트_퍼센트는_기본값과_레벨과_별에만_적용한다()
    {
        // (100 + 10 + 5) × 1.23 = 141.45 → 141, 고정값 17을 더하면 158이다.
        RaidStatCalculator.Calculate(baseValue: 100, levelBonus: 10, starBonus: 5,
                                     enchantAddPermille: 230, flatBonus: 17).ShouldBe(158);
    }
}
