using GameData;
using MikaUtils;

namespace WSGameServer;

public sealed class RaidCatalog : Singleton<RaidCatalog>
{
    private const int MinimumAttackIntervalMs = 100;

    private Dictionary<int, RaidBossTableRow> _bosses = new();
    private Dictionary<int, RaidBoxReward> _boxRewards = new();
    private Dictionary<(RaidStatSource Source, int SourceTid, RaidStatType StatType), int> _stats = new();

    public IReadOnlyCollection<RaidBossTableRow> Bosses => _bosses.Values;

    // 첫 보스만 자동 해금한다. 후속 보스가 생기면 자신의 해금 조건을 사용한다.
    public RaidBossTableRow? StartingBoss { get; private set; }

    public void LoadAll()
    {
        var bosses = GameTable.RaidBossTable.All.ToDictionary(row => row.RaidBossTID);
        var boxRewards = new Dictionary<int, RaidBoxReward>();
        foreach (var boss in bosses.Values)
        {
            if (boss.AttackIntervalMs < MinimumAttackIntervalMs)
            {
                throw new InvalidOperationException($"레이드 보스의 공격 주기가 너무 짧습니다. RaidBossTID={boss.RaidBossTID}");
            }

            boxRewards.Add(boss.RewardBoxItemTID, new RaidBoxReward(boss.RewardGold, boss.RewardArtifactTID,
                                                                    boss.ArtifactChancePermille));
        }

        var stats = GameTable.RaidStatTable.All.ToDictionary(row => (row.Source, row.SourceTID, row.StatType),
                                                            row => row.Value);
        foreach (var character in GameTable.CharacterTable.All)
        {
            if (character.AttackIntervalMs < MinimumAttackIntervalMs ||
                stats.GetValueOrDefault((RaidStatSource.Character, character.CharacterTID, RaidStatType.Attack)) <= 0 ||
                stats.GetValueOrDefault((RaidStatSource.Character, character.CharacterTID, RaidStatType.Health)) <= 0)
            {
                throw new InvalidOperationException($"캐릭터의 레이드 기본 스탯 또는 공격 주기가 잘못됐습니다. CharacterTID={character.CharacterTID}");
            }
        }

        _bosses = bosses;
        _boxRewards = boxRewards;
        _stats = stats;
        StartingBoss = bosses.Values.OrderBy(row => row.Level).ThenBy(row => row.RaidBossTID).FirstOrDefault();
    }

    public bool TryGetBoss(int tid, out RaidBossTableRow boss) => _bosses.TryGetValue(tid, out boss!);

    public bool TryGetBoxReward(int itemTid, out RaidBoxReward reward) => _boxRewards.TryGetValue(itemTid, out reward!);

    public int StatOf(RaidStatSource source, int sourceTid, RaidStatType stat)
        => _stats.GetValueOrDefault((source, sourceTid, stat));
}
