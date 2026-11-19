namespace WSGameServer;

/// <summary>보스 상자 1개당 골드를 항상 지급하고, 아티팩트는 지정 확률로 1개만 선택한다.</summary>
public class RaidBoxRewardTest
{
    [Theory]
    [InlineData(0, 250, 42)]
    [InlineData(4, 250, 42)]
    [InlineData(5, 250, null)]
    [InlineData(999, 250, null)]
    public void 상자에는_골드가_항상_들어_있고_아티팩트는_천분의_오로_나온다(
        int roll, int expectedGold, int? expectedArtifactTid)
    {
        var reward = new RaidBoxReward(gold: 250, artifactTid: 42, artifactChancePermille: 5);

        reward.Open(roll).ShouldBe((expectedGold, expectedArtifactTid));
    }

    [Fact]
    public void 아티팩트_확률은_일_퍼센트를_넘길_수_없다()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RaidBoxReward(250, 42, 11));
    }
}
