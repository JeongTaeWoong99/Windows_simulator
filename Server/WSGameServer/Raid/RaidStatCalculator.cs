namespace WSGameServer;

public static class RaidStatCalculator
{
    private const int PermilleScale = 1_000;

    /// <summary>장비와 인챈트의 고정 합산값에는 인챈트 배율을 적용하지 않는다.</summary>
    public static int Calculate(int baseValue, int levelBonus, int starBonus, int enchantAddPermille, int flatBonus)
    {
        long baseWithGrowth = (long)baseValue + levelBonus + starBonus;
        long multiplier = (long)PermilleScale + enchantAddPermille;
        Int128 scaled = (Int128)baseWithGrowth * multiplier / PermilleScale;
        return checked((int)(scaled + flatBonus));
    }
}
