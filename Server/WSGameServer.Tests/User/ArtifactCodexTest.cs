using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 도감 장착 · 켜고 끄기 · 즉시 판매 · 아티팩트 탭(T-181 · 이슈 #81).
/// 틀리면 인벤토리 재고에 효과가 붙거나, 장착한 1개가 팔려 효과가 증발하거나, 같은 번호를 두 번 장착해 효과가 겹친다.
/// </summary>
public class ArtifactCodexTest : IDisposable
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int FishermanCharm = 1;   // 어부의 낡은 부적 — 일반 · 전 산업 작업속도 +1‰ · BasePrice 400
    private const int CrackedComb    = 2;   // 깨진 토기 빗 — 일반 · 농사 작업속도 +2‰ · BasePrice 400
    private const int ClayCup        = 14;  // 토기 술잔 — 고급 · BasePrice 1200

    private readonly SqliteFixture _db = new();

    public ArtifactCodexTest()
    {
        GameTableFixture.EnsureLoaded();
    }

    public void Dispose() => _db.Dispose();

    private static (User User, TestUserBuilder B) NewUser(IEnumerable<(int Tid, int Count)> stacks, params int[] equipped)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadArtifacts(
            equipped.Select(tid => new UserArtifactRow { artifact_tid = tid }).ToList(),
            stacks.Select((s, slot) => new UserArtifactStackRow { slot = slot, artifact_tid = s.Tid, count = s.Count }).ToList());
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    // ── 장착 ──

    [Fact]
    public void 장착하면_인벤토리에서_1개가_빠지고_효과가_켜진다()
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 3) });

        user.TryEquipArtifact(FishermanCharm, Now);

        Last<S_ArtifactEquipResponse>(b).Result.ShouldBe(EResultCode.Ok);
        user.GetArtifactCount(FishermanCharm).ShouldBe(2);
        user.IsArtifactEquipped(FishermanCharm).ShouldBeTrue();
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(1);

        var saved = b.DB.PostedOf<EquipArtifactRepository>().ShouldHaveSingleItem();
        saved.ArtifactTid.ShouldBe(FishermanCharm);
        saved.Changes.ShouldHaveSingleItem().Count.ShouldBe(2);
        Last<S_ArtifactListResponse>(b).Artifacts.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            a => a.ArtifactTid.ShouldBe(FishermanCharm), a => a.IsActive.ShouldBeTrue());
        Last<S_ArtifactInventoryResponse>(b).Stacks.ShouldHaveSingleItem().Count.ShouldBe(2);
    }

    [Fact]
    public void 이미_장착한_번호는_다시_장착할_수_없다()
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 2) }, FishermanCharm);

        user.TryEquipArtifact(FishermanCharm, Now);

        Last<S_ArtifactEquipResponse>(b).Result.ShouldBe(EResultCode.ArtifactAlreadyEquipped);
        user.GetArtifactCount(FishermanCharm).ShouldBe(2);
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(1);   // 두 번 붙지 않는다
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 인벤토리에_없으면_장착할_수_없다()
    {
        var (user, b) = NewUser(new[] { (CrackedComb, 1) });

        user.TryEquipArtifact(FishermanCharm, Now);

        Last<S_ArtifactEquipResponse>(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.IsArtifactEquipped(FishermanCharm).ShouldBeFalse();
    }

    [Fact]
    public void 표에_없는_번호는_InvalidArtifact다()
    {
        var (user, b) = NewUser(Array.Empty<(int, int)>());

        user.TryEquipArtifact(99_999, Now);

        Last<S_ArtifactEquipResponse>(b).Result.ShouldBe(EResultCode.InvalidArtifact);
    }

    // ── 항상 켜짐 (#90) ──

    [Fact]
    public void 옛_켜고_끄기_요청은_바꾸지_않고_켜짐으로_답한다()
    {
        var (user, b) = NewUser(Array.Empty<(int, int)>(), CrackedComb);

        user.ReplyArtifactActive(CrackedComb);

        Last<S_ArtifactActiveResponse>(b).ShouldSatisfyAllConditions(r => r.Result.ShouldBe(EResultCode.Ok), r => r.IsActive.ShouldBeTrue());
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(2);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 장착하지_않은_번호의_켜고_끄기_요청은_거절한다()
    {
        var (user, b) = NewUser(new[] { (CrackedComb, 1) });

        user.ReplyArtifactActive(CrackedComb);

        Last<S_ArtifactActiveResponse>(b).ShouldSatisfyAllConditions(
            r => r.Result.ShouldBe(EResultCode.ArtifactNotEquipped), r => r.IsActive.ShouldBeFalse());
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(0);
    }

    // ── 즉시 판매 ──

    [Fact]
    public void 인벤토리의_아티팩트를_팔면_BasePrice만큼_골드를_받는다()
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 3), (ClayCup, 1) });

        user.TrySellArtifacts(new[]
        {
            new ArtifactInfo { ArtifactTid = FishermanCharm, Count = 2 },
            new ArtifactInfo { ArtifactTid = ClayCup, Count = 1 },
        });

        Last<S_ArtifactSellResponse>(b).ShouldSatisfyAllConditions(r => r.Result.ShouldBe(EResultCode.Ok), r => r.GainedGold.ShouldBe(2000));   // 400 × 2 + 1200
        user.Gold.ShouldBe(2000);
        user.GetArtifactCount(FishermanCharm).ShouldBe(1);
        user.GetArtifactCount(ClayCup).ShouldBe(0);
        b.DB.PostedOf<SellArtifactsRepository>().ShouldHaveSingleItem().Gold.ShouldBe(2000);
    }

    [Fact]
    public void 하나라도_모자라면_아무것도_팔리지_않는다()
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 3), (ClayCup, 1) });

        user.TrySellArtifacts(new[]
        {
            new ArtifactInfo { ArtifactTid = FishermanCharm, Count = 1 },
            new ArtifactInfo { ArtifactTid = ClayCup, Count = 2 },
        });

        Last<S_ArtifactSellResponse>(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.Gold.ShouldBe(0);
        user.GetArtifactCount(FishermanCharm).ShouldBe(3);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 도감에_장착한_것은_팔리지_않는다()
    {
        var (user, b) = NewUser(Array.Empty<(int, int)>(), FishermanCharm);

        user.TrySellArtifacts(new[] { new ArtifactInfo { ArtifactTid = FishermanCharm, Count = 1 } });

        Last<S_ArtifactSellResponse>(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.GetArtifactEffect(UserTraitEffect.SpeedAdd, IndustryType.Farming).ShouldBe(1);
    }

    [Theory]
    [InlineData(FishermanCharm, 0)]
    [InlineData(99_999, 1)]
    public void 수량이_0_이하이거나_표에_없는_번호면_InvalidArtifact다(int artifactTid, int count)
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 3) });

        user.TrySellArtifacts(new[] { new ArtifactInfo { ArtifactTid = artifactTid, Count = count } });

        Last<S_ArtifactSellResponse>(b).Result.ShouldBe(EResultCode.InvalidArtifact);
    }

    // ── 아티팩트 탭 ──

    [Fact]
    public void 아티팩트_탭에서_칸을_옮기면_장비_격자_응답으로_아티팩트_칸에_저장된다()
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 3) });

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Artifact, 0, 4);

        var res = Last<S_StorageSlotsResponse>(b);
        (res.Result, res.Tab).ShouldBe((EResultCode.Ok, EStorageTab.Equip));   // 장비 칸과 한 격자다(T-188)
        user.Artifacts.TryGetAt(4, out var moved).ShouldBeTrue();
        moved.Id.ShouldBe(FishermanCharm);
        b.DB.PostedOf<SaveArtifactStacksRepository>().ShouldHaveSingleItem();
        b.DB.PostedOf<SaveItemChangesRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 창고에는_아티팩트_탭이_없다()
    {
        var (user, b) = NewUser(new[] { (FishermanCharm, 3) });

        user.MoveStorageSlot(EContainer.Warehouse, EStorageTab.Artifact, 0, 1);

        Last<S_StorageSlotsResponse>(b).Result.ShouldBe(EResultCode.StorageSlotOutOfRange);
    }

    // ── DB ──

    private DbConnection Conn => new(_db.Connection);

    private long Scalar(string sql) => Convert.ToInt64(_db.Query(sql) ?? 0L);

    [Fact]
    public async Task 장착은_칸_차감과_도감_행을_같이_쓴다()
    {
        _db.CreateSchema();
        _db.Execute($"INSERT INTO t_user_artifact_stack (user_id, slot, artifact_tid, count) VALUES (1, 0, {FishermanCharm}, 1)");
        var (user, _) = NewUser(Array.Empty<(int, int)>());
        var removed = new List<ItemChangeInfo> { new() { ItemId = FishermanCharm, Count = 0, Slot = 0, Kind = EItemChangeKind.Remove } };

        await new EquipArtifactRepository(user, FishermanCharm, removed).ExecuteAsync(Conn);

        Scalar("SELECT COUNT(*) FROM t_user_artifact_stack").ShouldBe(0);
        Scalar($"SELECT COUNT(*) FROM t_user_artifact WHERE user_id = 1 AND artifact_tid = {FishermanCharm}").ShouldBe(1);
    }

    [Fact]
    public void 마이그레이션은_1개를_도감에_남기고_나머지를_인벤토리로_옮긴다()
    {
        var all = DbMigrator.LoadFrom(DbMigrator.DefaultDirectory);
        DbMigrator.Apply(_db.Connection, all.Where(m => m.Version < 9).ToList());
        _db.Execute($"INSERT INTO t_user_artifact (user_id, artifact_tid, count) VALUES (1, {FishermanCharm}, 3), (1, {CrackedComb}, 1), (1, {ClayCup}, 0)");

        DbMigrator.Apply(_db.Connection, all);

        Scalar("SELECT COUNT(*) FROM t_user_artifact").ShouldBe(2);   // 수량 0이던 번호는 장착한 적 없는 것으로
        Scalar($"SELECT COUNT(*) FROM t_user_artifact WHERE artifact_tid = {CrackedComb}").ShouldBe(1);
        Scalar($"SELECT count FROM t_user_artifact_stack WHERE artifact_tid = {FishermanCharm}").ShouldBe(2);
        Scalar("SELECT COUNT(*) FROM t_user_artifact_stack").ShouldBe(1);
    }

    [Fact]
    public void 마이그레이션은_꺼_둔_번호도_장착한_채로_켜짐_칸을_지운다()
    {
        var all = DbMigrator.LoadFrom(DbMigrator.DefaultDirectory);
        DbMigrator.Apply(_db.Connection, all.Where(m => m.Version < 11).ToList());
        _db.Execute($"INSERT INTO t_user_artifact (user_id, artifact_tid, is_active) VALUES (1, {FishermanCharm}, 0), (1, {CrackedComb}, 1)");

        DbMigrator.Apply(_db.Connection, all);

        Scalar("SELECT COUNT(*) FROM t_user_artifact").ShouldBe(2);
        Scalar("SELECT COUNT(*) FROM pragma_table_info('t_user_artifact') WHERE name = 'is_active'").ShouldBe(0);
    }
}
