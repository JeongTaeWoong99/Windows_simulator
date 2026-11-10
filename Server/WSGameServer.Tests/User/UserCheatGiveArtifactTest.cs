using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 지급 치트(<c>GiveArtifact</c> · T-198 · 이슈 #112).
/// 감정과 같은 길로 아티팩트 칸에 들어가고, 칸이 없으면 우편으로 가야 한다 —
/// 틀리면 치트로 확인한 화면이 실제 감정 결과와 달라진다.
/// </summary>
public class UserCheatGiveArtifactTest
{
    private const int FishermanCharm = 1;   // 어부의 낡은 부적
    private const int CrackedComb    = 2;   // 깨진 토기 빗

    public UserCheatGiveArtifactTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) Admin()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.AdminLevel = 1;
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static C_CheatRequest Req(long artifactTid, long count)
        => new() { Command = ECheatCommand.GiveArtifact, Arg1 = artifactTid, Arg2 = count };

    private static S_CheatResponse Reply(TestUserBuilder b) => b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem();

    private static void FillArtifactTab(User user)
    {
        var full = Enumerable.Range(0, User.StorageCapacity)
            .Select(slot => new UserArtifactStackRow { slot = slot, artifact_tid = CrackedComb, count = User.ArtifactMaxStack });
        user.LoadArtifacts(Array.Empty<UserArtifactRow>(), full.ToList());
    }

    [Fact]
    public void 아티팩트_칸에_개수만큼_들어가고_칸_스냅샷이_나간다()
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(FishermanCharm, 3), TestUserBuilder.Base);

        Reply(b).Result.ShouldBe(EResultCode.Ok);
        user.GetArtifactCount(FishermanCharm).ShouldBe(3);
        b.Channel.SentOf<S_ArtifactInventoryResponse>().ShouldHaveSingleItem().Stacks.ShouldHaveSingleItem().Count.ShouldBe(3);
        b.DB.PostedOf<SaveArtifactStacksRepository>().ShouldHaveSingleItem();
    }

    [Fact]
    public void 지급만으로는_도감에_장착되지_않는다()
    {
        var (user, _) = Admin();

        user.ExecuteCheat(Req(FishermanCharm, 1), TestUserBuilder.Base);

        user.IsArtifactEquipped(FishermanCharm).ShouldBeFalse();
    }

    [Fact]
    public void 아티팩트_칸이_가득이면_우편으로_간다()
    {
        var (user, b) = Admin();
        FillArtifactTab(user);

        user.ExecuteCheat(Req(FishermanCharm, 2), TestUserBuilder.Base);

        Reply(b).Result.ShouldBe(EResultCode.Ok);
        user.GetArtifactCount(FishermanCharm).ShouldBe(0);
        b.DB.PostedOf<StoreOverflowMailRepository>().ShouldHaveSingleItem().Attachment.Artifacts.ShouldBe(new[] { (FishermanCharm, 2) });
    }

    [Fact]
    public void 칸도_우편함도_가득이면_StorageFull이고_아무것도_보내지_않는다()
    {
        var (user, b) = Admin();
        FillArtifactTab(user);
        for (var i = 0; i < User.MailboxCapacity; i++)
        {
            user.StoreOverflowMail(new MailAttachment(1, new(), new(), new()), TestUserBuilder.Base);
        }

        b.DB.Posted.Clear();

        user.ExecuteCheat(Req(FishermanCharm, 1), TestUserBuilder.Base);

        Reply(b).Result.ShouldBe(EResultCode.StorageFull);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(99_999, 1)]   // 표에 없는 번호
    [InlineData(0, 1)]
    [InlineData(FishermanCharm, 0)]
    [InlineData(FishermanCharm, -1)]
    [InlineData(FishermanCharm, 1001)]   // 칸 하나 상한(ArtifactMaxStack 1000) 초과
    public void 없는_번호나_범위_밖_개수는_InvalidCheatArgs로_거절한다(long artifactTid, long count)
    {
        var (user, b) = Admin();

        user.ExecuteCheat(Req(artifactTid, count), TestUserBuilder.Base);

        Reply(b).Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }
}
