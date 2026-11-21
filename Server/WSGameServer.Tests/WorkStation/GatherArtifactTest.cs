using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>산업별 채취 아티팩트의 확률 경계와 실제 정산 지급을 검증한다.</summary>
public class GatherArtifactTest
{
    private const int ExcavationArtifactTid = 32;

    [Fact]
    public void 실제_데이터에서_산업별_Lv1은_백만_경우_중_하나만_당첨된다()
    {
        GameTableFixture.EnsureLoaded();
        var catalog = new CommonRewardCatalog();
        catalog.LoadAll();

        var rolls = new Mock<Random>(MockBehavior.Strict);
        rolls.SetupSequence(r => r.Next(1_000_000)).Returns(0).Returns(1).Returns(999_999);
        catalog.RollGatherArtifacts(IndustryType.Farming, 1, 3, rolls.Object).ShouldBe((29, 1));
        catalog.RollGatherArtifacts(IndustryType.Farming, 2, 3, rolls.Object).Count.ShouldBe(0);
    }

    [Fact]
    public void 다섯_산업의_Lv1_전용_아티팩트_데이터()
    {
        GameTableFixture.EnsureLoaded();
        var rows = GameTable.GatherArtifactTable.All;
        rows.Select(row => (row.IndustryType, row.ArtifactTID)).ShouldBe(new[]
        {
            (IndustryType.Farming, 29),
            (IndustryType.Logging, 30),
            (IndustryType.Mining, 31),
            (IndustryType.Excavation, 32),
            (IndustryType.Hunting, 33),
        }, ignoreOrder: true);

        foreach (var row in rows)
        {
            row.IndustryLevel.ShouldBe(1);
            row.ChancePerMillion.ShouldBe(1);
            var artifact = GameTable.ArtifactTable[row.ArtifactTID];
            artifact.Industry.ShouldBe(row.IndustryType);
            artifact.GlobalRarity.ShouldBe(GlobalRarity.Uncommon);
            artifact.EffectType.ShouldBe(UserTraitEffect.YieldAdd);
            artifact.EffectValue.ShouldBe(4);
        }
    }

    [Fact]
    public void 채취_정산은_자원과_아티팩트를_함께_지급한다()
    {
        var (user, builder) = UserWithGuaranteedDrop();

        user.SettleWorkStation(TestUserBuilder.Base.AddSeconds(30));

        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(1);
        user.GetArtifactCount(ExcavationArtifactTid).ShouldBe(1);
        builder.Channel.SentOf<S_ArtifactInventoryResponse>().ShouldHaveSingleItem();
    }

    [Fact]
    public void 아티팩트_칸이_가득_차면_채취_아티팩트를_우편에_보관한다()
    {
        var (user, builder) = UserWithGuaranteedDrop();
        var full = Enumerable.Range(0, User.StorageCapacity)
            .Select(slot => new UserArtifactStackRow { slot = slot, artifact_tid = 2, count = User.ArtifactMaxStack });
        user.LoadArtifacts([], full.ToList());

        user.SettleWorkStation(TestUserBuilder.Base.AddSeconds(30));

        user.GetArtifactCount(ExcavationArtifactTid).ShouldBe(0);
        builder.DB.PostedOf<StoreOverflowMailRepository>().ShouldHaveSingleItem()
            .Attachment.Artifacts.ShouldBe(new[] { (ExcavationArtifactTid, 1) });
    }

    private static (User User, TestUserBuilder Builder) UserWithGuaranteedDrop()
    {
        GameTableFixture.EnsureLoaded();
        const long characterId = 500;
        var builder = new TestUserBuilder().WithExcavationDrops();
        builder.CommonRewards.Load([], [],
        [
            new GatherArtifactTableRow
            {
                IndustryType = IndustryType.Excavation,
                IndustryLevel = 1,
                ArtifactTID = ExcavationArtifactTid,
                ChancePerMillion = 1_000_000,
            },
        ]);
        var user = builder.Build();
        user.LoadCharacters([new CharacterRow { character_id = characterId, character_tid = 1001, level = 1 }]);
        user.WorkStation.Load([new WorkStationSlot(0, IndustryType.Excavation, characterId, TestUserBuilder.Base)]);
        return (user, builder);
    }
}
