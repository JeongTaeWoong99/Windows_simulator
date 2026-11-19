namespace WSGameServer;

public sealed class RaidBoxReward
{
    public const int RollCount = 1_000;
    public const int MaxArtifactChancePermille = 10;

    private readonly int _gold;
    private readonly int _artifactTid;
    private readonly int _artifactChancePermille;

    public RaidBoxReward(int gold, int artifactTid, int artifactChancePermille)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(artifactTid);
        ArgumentOutOfRangeException.ThrowIfNegative(artifactChancePermille);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(artifactChancePermille, MaxArtifactChancePermille);

        _gold = gold;
        _artifactTid = artifactTid;
        _artifactChancePermille = artifactChancePermille;
    }

    public (int Gold, int? ArtifactTid) Open(int roll)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(roll);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(roll, RollCount);

        int? artifactTid = roll < _artifactChancePermille ? _artifactTid : null;
        return (_gold, artifactTid);
    }
}
