using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 장비 분해(T-131) — 장비를 지우고 등급별 재료(<c>DisassembleTable</c>)를 인벤토리에 넣는다. <b>전부 성공하거나 전부 실패한다.</b>
/// 재료가 다 안 들어가는데 장비만 지우면 장비가 증발한다 — 칸이 모자라면 아무것도 바꾸지 않아야 한다.
/// </summary>
public class UserEquipDisassembleTest
{
    public UserEquipDisassembleTest() => GameTableFixture.EnsureLoaded();

    private const int  CommonTid = 9101;
    private const int  EpicTid   = 9102;
    private const int  ShardTid  = 100050;   // 마력의 파편 — 일반 1개 · 영웅 15개
    private const long Sword     = 11;
    private const long Ring      = 12;
    private const long Crown     = 13;       // 영웅
    private const long CharA     = 500;

    private const int FillerTidBase = 900_000;

    private static (User User, TestUserBuilder B) NewUser(CharacterEquipRow[]? worn = null, bool lockedSword = false, bool swordInWarehouse = false)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = CommonTid, Name = "검", GlobalRarity = GlobalRarity.Common, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
            new EquipTableRow { EquipTID = EpicTid, Name = "왕관", GlobalRarity = GlobalRarity.Epic, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = Sword, equip_tid = CommonTid, slot_position = 0, is_locked = lockedSword ? 1 : 0,
                               container = swordInWarehouse ? (int)EContainer.Warehouse : (int)EContainer.Inventory },
            new UserEquipRow { equip_id = Ring,  equip_tid = CommonTid, slot_position = 1 },
            new UserEquipRow { equip_id = Crown, equip_tid = EpicTid,   slot_position = 2 },
        }, worn ?? Array.Empty<CharacterEquipRow>());

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_EquipDisassembleResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_EquipDisassembleResponse>().Last();

    // 자원 칸을 다른 종류로 전부 채운다.
    private static void FillInventory(User user, int leave = 0)
    {
        for (var i = 0; i < User.StorageCapacity - leave; i++)
        {
            user.GainItem(FillerTidBase + i, 1);
        }
    }

    [Fact]
    public void 분해하면_장비가_사라지고_등급별_재료가_들어온다()
    {
        // 일반 1 + 영웅 15 = 16
        var (user, b) = NewUser();

        user.TryDisassembleEquips(new[] { Sword, Crown });

        Last(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(ShardTid).ShouldBe(16);
        user.TryGetEquip(Sword, out _).ShouldBeFalse();
        user.TryGetEquip(Crown, out _).ShouldBeFalse();
        user.TryGetEquip(Ring, out _).ShouldBeTrue();
    }

    [Fact]
    public void 응답에_받은_재료의_칸_변경분이_실린다()
    {
        var (user, b) = NewUser();

        user.TryDisassembleEquips(new[] { Sword, Ring });

        var change = Last(b).ItemChangeInfos.ShouldHaveSingleItem();
        (change.ItemId, change.Count).ShouldBe((ShardTid, 2));
    }

    [Fact]
    public void 분해는_장비_삭제와_재료_지급을_한_번에_저장한다()
    {
        var (user, b) = NewUser();

        user.TryDisassembleEquips(new[] { Sword, Crown });

        var saved = b.DB.PostedOf<DisassembleEquipsRepository>().ShouldHaveSingleItem();
        saved.EquipIds.ShouldBe(new[] { Sword, Crown });
        saved.ItemChanges.ShouldHaveSingleItem().Count.ShouldBe(16);
        b.DB.PostedOf<AddItemRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 창고에_있는_장비도_분해된다()
    {
        var (user, b) = NewUser(swordInWarehouse: true);

        user.TryDisassembleEquips(new[] { Sword });

        Last(b).Result.ShouldBe(EResultCode.Ok);
        user.TryGetEquip(Sword, out _).ShouldBeFalse();
        user.GetItemCount(ShardTid).ShouldBe(1);
    }

    [Fact]
    public void 착용_중인_장비가_섞이면_아무것도_분해되지_않는다()
    {
        var (user, b) = NewUser(new[] { new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Sword } });

        user.TryDisassembleEquips(new[] { Ring, Sword });

        Last(b).Result.ShouldBe(EResultCode.DisassembleEquipWorn);
        user.TryGetEquip(Ring, out _).ShouldBeTrue();
        user.GetItemCount(ShardTid).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 잠긴_장비는_분해할_수_없다()
    {
        var (user, b) = NewUser(lockedSword: true);

        user.TryDisassembleEquips(new[] { Sword });

        Last(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
    }

    [Fact]
    public void 재료가_인벤토리에_다_안_들어가면_장비가_남고_아무것도_저장하지_않는다()
    {
        var (user, b) = NewUser();
        FillInventory(user);
        b.DB.Posted.Clear();

        user.TryDisassembleEquips(new[] { Sword });

        Last(b).Result.ShouldBe(EResultCode.StorageFull);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
        user.GetItemCount(ShardTid).ShouldBe(0);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 칸이_가득_차도_이미_가진_파편_칸에_자리가_있으면_분해된다()
    {
        // 파편 1 + 채움 = 칸 전부. 파편 칸(MaxStack 9,999)에 쌓여 새 칸이 필요 없다 → 1 + 15 = 16
        var (user, b) = NewUser();
        user.GainItem(ShardTid, 1);
        FillInventory(user, leave: 1);

        user.TryDisassembleEquips(new[] { Crown });

        Last(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(ShardTid).ShouldBe(16);
    }

    [Fact]
    public void 미보유_장비는_거부된다()
    {
        var (user, b) = NewUser();

        user.TryDisassembleEquips(new[] { Sword, 999L });

        Last(b).Result.ShouldBe(EResultCode.EquipNotOwned);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
    }

    [Fact]
    public void 빈_요청과_중복_장비는_잘못된_요청이다()
    {
        // 중복을 받으면 재료가 두 번 나온다.
        var (user, b) = NewUser();

        user.TryDisassembleEquips(Array.Empty<long>());
        Last(b).Result.ShouldBe(EResultCode.InvalidDisassembleRequest);

        user.TryDisassembleEquips(new[] { Sword, Sword });
        Last(b).Result.ShouldBe(EResultCode.InvalidDisassembleRequest);
        user.GetItemCount(ShardTid).ShouldBe(0);
    }
}
