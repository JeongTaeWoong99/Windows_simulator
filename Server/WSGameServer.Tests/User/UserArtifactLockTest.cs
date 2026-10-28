using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 잠금(T-188 · 이슈 #87). 아티팩트는 개체가 없어 <b>번호 단위</b>로 잠그고, 잠긴 번호는 즉시 판매·경매 등록·분해에서 빠진다.
/// 도감 장착은 막지 않는다. 틀리면 아끼는 중복을 일괄 판매로 날리거나, 자원 잠금과 번호가 섞여 엉뚱한 자원이 잠긴다.
/// </summary>
public class UserArtifactLockTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int FishermanCharm = 1;   // 일반 · BasePrice 400
    private const int CrackedComb    = 2;   // 일반 · BasePrice 400

    private readonly FakeAuctionClient _client = new();

    public UserArtifactLockTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) NewUser(params int[] lockedTids)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };

        var user = b.Build();
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.LoadArtifacts(Array.Empty<UserArtifactRow>(), new[]
        {
            new UserArtifactStackRow { slot = 0, artifact_tid = FishermanCharm, count = 3 },
            new UserArtifactStackRow { slot = 1, artifact_tid = CrackedComb, count = 2 },
        });
        user.LoadArtifactLocks(lockedTids.Select(tid => new UserArtifactLockRow { artifact_tid = tid }));

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    private static List<ArtifactInfo> Request(params (int Tid, int Count)[] artifacts)
        => artifacts.Select(a => new ArtifactInfo { ArtifactTid = a.Tid, Count = a.Count }).ToList();

    // ── 잠금 · 해제 ──

    [Fact]
    public void 아티팩트를_잠그면_응답하고_저장한다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Artifact, FishermanCharm, locked: true);

        Last<S_EntityLockResponse>(b).ShouldSatisfyAllConditions(
            r => r.Result.ShouldBe(EResultCode.Ok), r => r.Target.ShouldBe(ELockTarget.Artifact), r => r.Locked.ShouldBeTrue());
        b.DB.PostedOf<EntityLockRepository>().Single().ShouldSatisfyAllConditions(
            r => r.Target.ShouldBe(ELockTarget.Artifact), r => r.Id.ShouldBe(FishermanCharm));
        user.IsArtifactLocked(FishermanCharm).ShouldBeTrue();
    }

    [Fact]
    public void 표에_없는_번호는_잠글_수_없다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Artifact, 99_999, locked: true);

        Last<S_EntityLockResponse>(b).Result.ShouldBe(EResultCode.InvalidLockTarget);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 아티팩트_잠금은_같은_번호의_자원을_잠그지_않는다()
    {
        var (user, b) = NewUser(FishermanCharm);

        user.IsItemLocked(FishermanCharm).ShouldBeFalse();
    }

    [Fact]
    public void 아티팩트_칸_스냅샷에_잠긴_번호가_실린다()
    {
        var (user, b) = NewUser(CrackedComb);

        user.SendArtifactInventory();

        Last<S_ArtifactInventoryResponse>(b).LockedArtifactTids.ShouldBe(new[] { CrackedComb });
    }

    // ── 없애는 동작에서 빠진다 ──

    [Fact]
    public void 잠긴_번호가_섞인_즉시_판매는_통째로_거절된다()
    {
        var (user, b) = NewUser(CrackedComb);

        user.TrySellArtifacts(Request((FishermanCharm, 1), (CrackedComb, 1)));

        Last<S_ArtifactSellResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.GetArtifactCount(FishermanCharm).ShouldBe(3);
        user.GetArtifactCount(CrackedComb).ShouldBe(2);
    }

    [Fact]
    public void 잠긴_번호는_경매에_올릴_수_없다()
    {
        var (user, b) = NewUser(FishermanCharm);

        user.TryRegisterAuction(EAuctionKind.Artifact, FishermanCharm, 1, 0, 400, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.GetArtifactCount(FishermanCharm).ShouldBe(3);
    }

    [Fact]
    public void 잠긴_번호는_분해할_수_없다()
    {
        var (user, b) = NewUser(FishermanCharm);

        user.TryDisassemble(new List<long>(), Request((FishermanCharm, 1)));

        Last<S_EquipDisassembleResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.GetArtifactCount(FishermanCharm).ShouldBe(3);
    }

    [Fact]
    public void 잠긴_번호도_도감에는_장착된다()
    {
        var (user, b) = NewUser(FishermanCharm);

        user.TryEquipArtifact(FishermanCharm, Now);

        Last<S_ArtifactEquipResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }
}
