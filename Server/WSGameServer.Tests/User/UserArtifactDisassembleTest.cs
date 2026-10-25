using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 분해(T-185 · 이슈 #89) — 아티팩트를 등급별 마석(<c>DisassembleTable.ArtifactItemCount</c> = 장비의 2배)으로 바꾼다.
/// 장비와 한 요청에 섞여 와도 <b>전부 성공하거나 전부 실패한다.</b> 틀리면 아티팩트만 빠지고 마석이 안 들어오거나,
/// 한쪽이 거절됐는데 다른 쪽만 사라진다.
/// </summary>
public class UserArtifactDisassembleTest
{
    public UserArtifactDisassembleTest() => GameTableFixture.EnsureLoaded();

    private const int  CommonEquipTid = 9101;
    private const int  ShardTid       = 100050;  // 마력의 파편 — 장비 일반 1 · 아티팩트 일반 2 · 고급 4
    private const int  FishermanCharm = 1;       // 일반 아티팩트
    private const int  ClayCup        = 14;      // 고급 아티팩트
    private const long Sword          = 11;      // 일반 장비

    private const int FillerTidBase = 900_000;

    private static readonly long[] NoEquips = Array.Empty<long>();

    private static (User User, TestUserBuilder B) NewUser(params (int Tid, int Count)[] stacks)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = CommonEquipTid, Name = "검", GlobalRarity = GlobalRarity.Common, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });

        var user = b.Build();
        user.LoadEquips(new[] { new UserEquipRow { equip_id = Sword, equip_tid = CommonEquipTid, slot_position = 0 } },
                        Array.Empty<CharacterEquipRow>());
        user.LoadArtifacts(Array.Empty<UserArtifactRow>(),
            stacks.Select((s, slot) => new UserArtifactStackRow { slot = slot, artifact_tid = s.Tid, count = s.Count }).ToList());

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static ArtifactInfo[] Art(params (int Tid, int Count)[] items)
        => items.Select(i => new ArtifactInfo { ArtifactTid = i.Tid, Count = i.Count }).ToArray();

    private static S_EquipDisassembleResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_EquipDisassembleResponse>().Last();

    [Fact]
    public void 아티팩트를_분해하면_칸에서_빠지고_등급별로_장비의_2배_마석이_들어온다()
    {
        // 일반 2 × 3개 + 고급 4 × 1개 = 10
        var (user, b) = NewUser((FishermanCharm, 5), (ClayCup, 1));

        user.TryDisassemble(NoEquips, Art((FishermanCharm, 3), (ClayCup, 1)));

        Last(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(ShardTid).ShouldBe(10);
        user.GetArtifactCount(FishermanCharm).ShouldBe(2);
        user.GetArtifactCount(ClayCup).ShouldBe(0);
    }

    [Fact]
    public void 장비와_아티팩트를_한_번에_분해한다()
    {
        // 장비 일반 1 + 아티팩트 일반 2 = 3
        var (user, b) = NewUser((FishermanCharm, 1));

        user.TryDisassemble(new[] { Sword }, Art((FishermanCharm, 1)));

        Last(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(ShardTid).ShouldBe(3);
        user.TryGetEquip(Sword, out _).ShouldBeFalse();
        user.GetArtifactCount(FishermanCharm).ShouldBe(0);
    }

    [Fact]
    public void 같은_번호가_두_줄로_와도_합쳐서_분해한다()
    {
        // 1 + 2 = 3개 → 일반 2 × 3 = 6
        var (user, b) = NewUser((FishermanCharm, 3));

        user.TryDisassemble(NoEquips, Art((FishermanCharm, 1), (FishermanCharm, 2)));

        Last(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(ShardTid).ShouldBe(6);
        user.GetArtifactCount(FishermanCharm).ShouldBe(0);
    }

    [Fact]
    public void 분해는_장비_삭제와_아티팩트_칸_차감과_재료_지급을_한_번에_저장한다()
    {
        var (user, b) = NewUser((FishermanCharm, 2));

        user.TryDisassemble(new[] { Sword }, Art((FishermanCharm, 1)));

        var saved = b.DB.PostedOf<DisassembleEquipsRepository>().ShouldHaveSingleItem();
        saved.EquipIds.ShouldBe(new[] { Sword });
        saved.ArtifactChanges.ShouldHaveSingleItem().Count.ShouldBe(1);
        saved.ItemChanges.ShouldHaveSingleItem().Count.ShouldBe(3);
        b.DB.PostedOf<SaveArtifactStacksRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 아티팩트를_분해하면_아티팩트_칸_스냅샷을_보낸다()
    {
        var (user, b) = NewUser((FishermanCharm, 2));

        user.TryDisassemble(NoEquips, Art((FishermanCharm, 1)));

        b.Channel.SentOf<S_ArtifactInventoryResponse>().Last().Stacks.ShouldHaveSingleItem().Count.ShouldBe(1);
    }

    [Fact]
    public void 아티팩트가_모자라면_섞인_장비도_분해되지_않는다()
    {
        var (user, b) = NewUser((FishermanCharm, 1));

        user.TryDisassemble(new[] { Sword }, Art((FishermanCharm, 2)));

        Last(b).Result.ShouldBe(EResultCode.NotEnoughItem);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
        user.GetArtifactCount(FishermanCharm).ShouldBe(1);
        user.GetItemCount(ShardTid).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 표에_없는_아티팩트_번호는_거부된다()
    {
        var (user, b) = NewUser((FishermanCharm, 1));

        user.TryDisassemble(NoEquips, Art((99_999, 1)));

        Last(b).Result.ShouldBe(EResultCode.InvalidArtifact);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 수량이_0_이하인_아티팩트는_잘못된_요청이다()
    {
        var (user, b) = NewUser((FishermanCharm, 1));

        user.TryDisassemble(NoEquips, Art((FishermanCharm, 0)));

        Last(b).Result.ShouldBe(EResultCode.InvalidDisassembleRequest);
        user.GetArtifactCount(FishermanCharm).ShouldBe(1);
    }

    [Fact]
    public void 장비도_아티팩트도_없으면_잘못된_요청이다()
    {
        var (user, b) = NewUser((FishermanCharm, 1));

        user.TryDisassemble(NoEquips, Array.Empty<ArtifactInfo>());

        Last(b).Result.ShouldBe(EResultCode.InvalidDisassembleRequest);
    }

    [Fact]
    public void 재료가_인벤토리에_다_안_들어가면_아티팩트가_남는다()
    {
        var (user, b) = NewUser((FishermanCharm, 1));
        for (var i = 0; i < User.StorageCapacity; i++)
        {
            user.GainItem(FillerTidBase + i, 1);
        }
        b.DB.Posted.Clear();

        user.TryDisassemble(NoEquips, Art((FishermanCharm, 1)));

        Last(b).Result.ShouldBe(EResultCode.StorageFull);
        user.GetArtifactCount(FishermanCharm).ShouldBe(1);
        b.DB.Posted.ShouldBeEmpty();
    }
}
