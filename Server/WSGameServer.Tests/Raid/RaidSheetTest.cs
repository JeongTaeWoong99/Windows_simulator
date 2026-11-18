using GameData;

namespace WSGameServer;

/// <summary>레이드 보상과 성장 데이터가 서로 참조하는 실제 시트를 검증한다.</summary>
public class RaidSheetTest
{
    public RaidSheetTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public void 보스의_해금과_전용_보상은_존재하고_상자별로_구분된다()
    {
        var bosses = GameTable.RaidBossTable.All;
        bosses.ShouldNotBeEmpty();
        bosses.Select(r => r.RewardBoxItemTID).ToHashSet().Count.ShouldBe(bosses.Count);

        foreach (var boss in bosses)
        {
            GameTable.UnlockTable.Contains(boss.UnlockTID).ShouldBeTrue();
            var box = GameTable.ItemTable[boss.RewardBoxItemTID];
            (box.ItemType, GameTable.GachaInfoTable[box.OpenGachaId].CostCurrency)
                .ShouldBe((ItemType.Box, CurrencyType.None));
            GameTable.ArtifactTable.Contains(boss.RewardArtifactTID).ShouldBeTrue();
            boss.ArtifactChancePermille.ShouldBeInRange(0, RaidBoxReward.MaxArtifactChancePermille);
        }
    }

    [Fact]
    public void 모든_캐릭터는_양수의_기본_공격력과_체력이_있다()
    {
        var baseStats = GameTable.RaidStatTable.All.Where(r => r.Source == RaidStatSource.Character)
            .ToDictionary(r => (r.SourceTID, r.StatType), r => r.Value);

        foreach (var character in GameTable.CharacterTable.All)
        {
            baseStats[(character.CharacterTID, RaidStatType.Attack)].ShouldBeGreaterThan(0);
            baseStats[(character.CharacterTID, RaidStatType.Health)].ShouldBeGreaterThan(0);
            character.AttackIntervalMs.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void 전투_스탯의_출처는_해당_테이블에서_찾을_수_있다()
    {
        var stats = GameTable.RaidStatTable.All;
        stats.Select(r => (r.Source, r.SourceTID, r.StatType)).ToHashSet().Count.ShouldBe(stats.Count);

        foreach (var row in stats)
        {
            bool exists = row.Source switch
            {
                RaidStatSource.Character => GameTable.CharacterTable.Contains(row.SourceTID),
                RaidStatSource.Level => GameTable.CharacterLevelTable.Contains(row.SourceTID),
                RaidStatSource.Star => GameTable.CharacterStarTable.Contains(row.SourceTID),
                RaidStatSource.Equip => GameTable.EquipTable.Contains(row.SourceTID),
                _ => false,
            };
            exists.ShouldBeTrue($"전투 스탯 TID {row.RaidStatTID}");
        }
    }

    [Fact]
    public void 승리_우편은_만료되지_않고_고정_첨부가_없다()
    {
        var mail = GameTable.MailTemplateTable[8];

        (mail.PeriodDays, mail.Gold, mail.ItemTIDs.Length, mail.ItemCounts.Length,
            mail.CharacterTIDs.Length, mail.EquipTIDs.Length).ShouldBe((0, 0L, 0, 0, 0, 0));
    }
}
