using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 칸과 장비 칸이 인벤토리 장비 탭 한 격자를 함께 쓴다(T-188 · 이슈 #87).
/// 틀리면 장비와 아티팩트가 같은 칸에 겹쳐 하나가 가려지거나, 50칸을 따로 세어 격자 밖으로 넘친다.
/// </summary>
public class UserArtifactEquipSlotTest : IDisposable
{
    private const int  EquipTid      = 1001;
    private const int  FishermanCharm = 1;   // 일반
    private const int  CrackedComb    = 2;   // 일반
    private const int  ClayCup        = 14;  // 고급
    private const long SwordA        = 11;
    private const long SwordB        = 12;

    private readonly SqliteFixture _db = new();

    public UserArtifactEquipSlotTest() => GameTableFixture.EnsureLoaded();

    public void Dispose() => _db.Dispose();

    private static (User User, TestUserBuilder B) NewUser(
        (long Id, int Slot)[] equips, (int Tid, int Count, int Slot)[] artifacts, UserEquipRow[]? warehouse = null)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        var rows = equips.Select(e => new UserEquipRow { equip_id = e.Id, equip_tid = EquipTid, slot_position = e.Slot })
            .Concat(warehouse ?? Array.Empty<UserEquipRow>())
            .ToList();
        user.LoadEquips(rows, Array.Empty<CharacterEquipRow>());
        user.LoadArtifacts(new List<UserArtifactRow>(),
            artifacts.Select(a => new UserArtifactStackRow { artifact_tid = a.Tid, count = a.Count, slot = a.Slot }).ToList());
        return (user, b);
    }

    private static void Clear(TestUserBuilder b)
    {
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
    }

    private static S_StorageSlotsResponse LastSlots(TestUserBuilder b) => b.Channel.SentOf<S_StorageSlotsResponse>().Last();

    private static (int Tid, int Count, int Slot)[] ArtifactSlots(TestUserBuilder b)
        => b.Channel.SentOf<S_ArtifactInventoryResponse>().Last().Stacks!
            .Select(s => (s.ItemId, s.Count, s.Slot)).OrderBy(s => s.Slot).ToArray();

    // ── 적재 · 지급 ──

    [Fact]
    public void 장비_칸과_겹친_아티팩트는_적재할_때_빈_칸으로_비키고_저장한다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0), (SwordB, 1) }, new[] { (FishermanCharm, 3, 0) });

        user.Artifacts.Items.Single().Slot.ShouldBe(2);
        // 옛 칸 행을 지우고 새 칸에 쓴다 — 안 지우면 다음 로그인에 두 칸이 된다
        b.DB.PostedOf<SaveArtifactStacksRepository>().Single().Changes
            .Select(c => (c.Slot, c.ItemId, c.Count)).ShouldBe(new[] { (0, FishermanCharm, 0), (2, FishermanCharm, 3) }, ignoreOrder: true);
    }

    [Fact]
    public void 받은_아티팩트는_장비가_없는_칸에_놓인다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0), (SwordB, 1) }, Array.Empty<(int, int, int)>());

        user.GainArtifacts(new Dictionary<int, int> { [FishermanCharm] = 1 });

        user.Artifacts.Items.Single().Slot.ShouldBe(2);
    }

    [Fact]
    public void 받은_장비는_아티팩트가_없는_칸에_놓인다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0) }, new[] { (FishermanCharm, 1, 1) });

        user.NextFreeEquipPosition().ShouldBe(2);
    }

    [Fact]
    public void 장비와_아티팩트가_칸_수를_함께_센다()
    {
        // 장비 49칸 + 아티팩트 1칸 = 50칸(StorageCapacity) — 새 번호도 새 장비도 들어갈 자리가 없다
        var equips    = Enumerable.Range(0, User.StorageCapacity - 1).Select(i => ((long)i + 1, i)).ToArray();
        var (user, b) = NewUser(equips, new[] { (FishermanCharm, 1, User.StorageCapacity - 1) });

        user.HasArtifactRoomFor(new[] { (CrackedComb, 1) }).ShouldBeFalse();
        user.HasArtifactRoomFor(new[] { (FishermanCharm, 1) }).ShouldBeTrue();   // 같은 번호 칸에는 쌓인다
        user.HasStorageFor(Array.Empty<(int, int)>(), 0, 1).ShouldBeFalse();
    }

    // ── 칸 이동 ──

    [Fact]
    public void 장비를_아티팩트_칸에_놓으면_교환된다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0) }, new[] { (FishermanCharm, 3, 1) });
        Clear(b);

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Equip, 0, 1);

        LastSlots(b).ShouldSatisfyAllConditions(
            r => r.Result.ShouldBe(EResultCode.Ok),
            r => r.Slots!.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { (SwordA, 1) }));
        ArtifactSlots(b).ShouldBe(new[] { (FishermanCharm, 3, 0) });
        b.DB.PostedOf<SaveStorageSlotsRepository>().ShouldHaveSingleItem();
        b.DB.PostedOf<SaveArtifactStacksRepository>().Single().Changes
            .Select(c => (c.Slot, c.Count)).ShouldBe(new[] { (0, 3), (1, 0) }, ignoreOrder: true);
    }

    [Fact]
    public void 아티팩트를_장비_칸에_놓으면_교환된다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0) }, new[] { (FishermanCharm, 3, 1) });
        Clear(b);

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Equip, 1, 0);

        LastSlots(b).Slots!.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { (SwordA, 1) });
        ArtifactSlots(b).ShouldBe(new[] { (FishermanCharm, 3, 0) });
    }

    [Fact]
    public void 아티팩트를_빈_칸으로_옮긴다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0) }, new[] { (FishermanCharm, 3, 1) });
        Clear(b);

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Equip, 1, 7);

        LastSlots(b).ShouldSatisfyAllConditions(r => r.Result.ShouldBe(EResultCode.Ok), r => r.Slots!.ShouldBeEmpty());
        ArtifactSlots(b).ShouldBe(new[] { (FishermanCharm, 3, 7) });
    }

    [Fact]
    public void 같은_번호_아티팩트_칸에_놓으면_합친다()
    {
        var (user, b) = NewUser(Array.Empty<(long, int)>(), new[] { (FishermanCharm, 3, 0), (FishermanCharm, 4, 5) });
        Clear(b);

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Equip, 5, 0);

        ArtifactSlots(b).ShouldBe(new[] { (FishermanCharm, 7, 0) });
    }

    [Fact]
    public void 아티팩트_탭으로_보낸_이동도_같은_격자에서_처리한다()
    {
        var (user, b) = NewUser(new[] { (SwordA, 0) }, new[] { (FishermanCharm, 3, 1) });
        Clear(b);

        user.MoveStorageSlot(EContainer.Inventory, EStorageTab.Artifact, 1, 0);

        LastSlots(b).Slots!.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { (SwordA, 1) });
        ArtifactSlots(b).ShouldBe(new[] { (FishermanCharm, 3, 0) });
    }

    // ── 정렬 ──

    [Fact]
    public void 정렬하면_장비를_앞에_두고_아티팩트를_그_뒤에_놓는다()
    {
        // 아티팩트는 등급 내림차순 — 고급(술잔)이 일반(부적)보다 앞이다
        var (user, b) = NewUser(new[] { (SwordA, 4), (SwordB, 9) }, new[] { (FishermanCharm, 2, 0), (ClayCup, 1, 2) });
        Clear(b);

        user.SortStorage(EContainer.Inventory, EStorageTab.Equip, EStorageSortKey.Rarity, EStorageSortOrder.Descending);

        LastSlots(b).Slots!.Select(s => s.Slot).Order().ShouldBe(new[] { 0, 1 });
        ArtifactSlots(b).ShouldBe(new[] { (ClayCup, 1, 2), (FishermanCharm, 2, 3) });
    }

    // ── 창고 ──

    [Fact]
    public void 창고_장비를_아티팩트_칸에_놓을_수_없다()
    {
        var stored    = new UserEquipRow { equip_id = SwordB, equip_tid = EquipTid, slot_position = 0, container = (int)EContainer.Warehouse };
        var (user, b) = NewUser(Array.Empty<(long, int)>(), new[] { (FishermanCharm, 3, 2) }, new[] { stored });
        Clear(b);

        user.TransferWarehouse(EContainer.Warehouse, EStorageTab.Equip, 0, 2, 1);

        b.Channel.SentOf<S_WarehouseTransferResponse>().Last().Result.ShouldBe(EResultCode.StorageSlotArtifact);
        b.DB.Posted.ShouldBeEmpty();
    }

    // ── 마이그레이션 ──

    [Fact]
    public void 마이그레이션은_장비와_겹친_아티팩트_칸을_장비_뒤로_옮긴다()
    {
        var all = DbMigrator.LoadFrom(DbMigrator.DefaultDirectory);
        DbMigrator.Apply(_db.Connection, all.Where(m => m.Version < 10).ToList());
        // 유저 1: 장비 0·1 · 창고 장비 5 · 아티팩트 0·1 → 장비 뒤 2·3 / 유저 2: 장비 없음 → 그대로
        _db.Execute(@"INSERT INTO t_user_equip (equip_id, user_id, equip_tid, slot_position, container)
                      VALUES (11, 1, 1001, 0, 0), (12, 1, 1001, 1, 0), (13, 1, 1001, 5, 1)");
        _db.Execute($@"INSERT INTO t_user_artifact_stack (user_id, slot, artifact_tid, count)
                       VALUES (1, 0, {FishermanCharm}, 3), (1, 1, {CrackedComb}, 2), (2, 0, {FishermanCharm}, 1)");

        DbMigrator.Apply(_db.Connection, all);

        ((long)_db.Query($"SELECT slot FROM t_user_artifact_stack WHERE user_id = 1 AND artifact_tid = {FishermanCharm}")!).ShouldBe(2);
        ((long)_db.Query($"SELECT slot FROM t_user_artifact_stack WHERE user_id = 1 AND artifact_tid = {CrackedComb}")!).ShouldBe(3);
        ((long)_db.Query("SELECT slot FROM t_user_artifact_stack WHERE user_id = 2")!).ShouldBe(0);
    }
}
