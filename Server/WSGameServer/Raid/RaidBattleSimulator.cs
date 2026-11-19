namespace WSGameServer;

public readonly record struct RaidFighterStats(int Attack, int MaxHealth, int AttackIntervalMs);

public enum RaidBattleOutcome
{
    Victory,
    PartyDefeated,
    TimeLimitReached,
}

/// <summary>참가자 번호는 0부터 시작하는 편성 순서이며, 보스 번호는 파티 인원수와 같다.</summary>
public readonly record struct RaidBattleHit(int ElapsedMs, int AttackerIndex, int TargetIndex, int Damage, int TargetHealth);

public sealed record RaidBattleResult(
    RaidBattleOutcome Outcome,
    int DurationMs,
    IReadOnlyList<int> RemainingPartyHealth,
    int RemainingBossHealth,
    IReadOnlyList<RaidBattleHit> Hits);

public static class RaidBattleSimulator
{
    private const int TimeLimitMs = 60_000;
    private const int MaxPartySize = 3;

    public static RaidBattleResult Simulate(IReadOnlyList<RaidFighterStats> party, RaidFighterStats boss)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Count is < 1 or > MaxPartySize)
        {
            throw new ArgumentOutOfRangeException(nameof(party), "레이드 파티는 1~3명이어야 합니다.");
        }

        int bossIndex = party.Count;
        RaidFighterStats[] fighters = [.. party, boss];
        int[] health = new int[fighters.Length];
        var hits = new List<RaidBattleHit>();
        // 시각이 같으면 편성 순서대로 공격하며, 보스가 마지막이다.
        var turns = new PriorityQueue<int, (long ElapsedMs, int AttackerIndex)>();
        for (int index = 0; index < fighters.Length; index++)
        {
            RaidFighterStats stats = fighters[index];
            ValidateFighter(stats, index == bossIndex ? nameof(boss) : nameof(party));
            health[index] = stats.MaxHealth;
            turns.Enqueue(index, (stats.AttackIntervalMs, index));
        }

        int front = 0;
        while (turns.TryDequeue(out int attacker, out var turn) && turn.ElapsedMs <= TimeLimitMs)
        {
            if (health[attacker] == 0)
            {
                continue;
            }

            int elapsedMs = (int)turn.ElapsedMs;
            int target = attacker == bossIndex ? front : bossIndex;
            int damage = Math.Min(fighters[attacker].Attack, health[target]);
            health[target] -= damage;
            hits.Add(new RaidBattleHit(elapsedMs, attacker, target, damage, health[target]));

            if (health[target] == 0)
            {
                if (target == bossIndex)
                {
                    return Finish(RaidBattleOutcome.Victory, elapsedMs);
                }

                front++;
                if (front == bossIndex)
                {
                    return Finish(RaidBattleOutcome.PartyDefeated, elapsedMs);
                }
            }

            turns.Enqueue(attacker, (turn.ElapsedMs + fighters[attacker].AttackIntervalMs, attacker));
        }

        return Finish(RaidBattleOutcome.TimeLimitReached, TimeLimitMs);

        RaidBattleResult Finish(RaidBattleOutcome outcome, int durationMs)
        {
            return new RaidBattleResult(outcome, durationMs, Array.AsReadOnly(health[..bossIndex]),
                                        health[bossIndex], hits.AsReadOnly());
        }
    }

    private static void ValidateFighter(RaidFighterStats stats, string parameterName)
    {
        if (stats.Attack <= 0 || stats.MaxHealth <= 0 || stats.AttackIntervalMs <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "공격력, 최대 체력, 공격 주기는 양수여야 합니다.");
        }
    }
}
