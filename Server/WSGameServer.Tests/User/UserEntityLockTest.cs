using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 캐릭터·장비 잠금(T-137). 잠금은 <b>개체를 없애는 동작</b>(즉시 판매 · 경매 등록 · 응축 재료)만 막는다 —
/// 장착·배치·응축 대상은 그대로 된다. 틀리면 아끼는 개체를 실수로 날리거나, 잠근 개체를 쓰지 못하게 된다.
/// </summary>
public class UserEntityLockTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    private const int  SwordTid = 9101;
    private const long Sword    = 11;
    private const long Ring     = 12;
    private const long CharA    = 500;   // 램볼(1001)
    private const long CharB    = 501;
    private const long CharC    = 502;

    private readonly FakeAuctionClient _client = new();

    public UserEntityLockTest() => GameTableFixture.EnsureLoaded();

    private (User User, TestUserBuilder B) NewUser(bool lockedA = false, bool lockedSword = false)
    {
        var b = new TestUserBuilder().WithInlineExecutor();
        b.Auction = new AuctionService(_client, b.Executor) { FindOnlineUser = _ => null };
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", GlobalRarity = GlobalRarity.Common, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });

        var user = b.Build();
        user.OnAuctionStateLoaded(0);
        user.GainGold(1000);
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0, is_locked = lockedA ? 1 : 0 },
            new CharacterRow { character_id = CharB, character_tid = 1001, level = 1, exp = 0 },
            new CharacterRow { character_id = CharC, character_tid = 1001, level = 1, exp = 0 },
        });
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = Sword, equip_tid = SwordTid, slot_position = 0, is_locked = lockedSword ? 1 : 0 },
            new UserEquipRow { equip_id = Ring,  equip_tid = SwordTid, slot_position = 1 },
        }, Array.Empty<CharacterEquipRow>());

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    // ── 잠금 · 해제 ──

    [Fact]
    public void 캐릭터를_잠그면_응답과_목록에_실리고_저장된다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Character, CharA, locked: true);

        var res = Last<S_EntityLockResponse>(b);
        (res.Result, res.Target, res.Id, res.Locked).ShouldBe((EResultCode.Ok, ELockTarget.Character, CharA, true));
        var saved = b.DB.PostedOf<EntityLockRepository>().Single();
        (saved.Target, saved.Id, saved.Locked).ShouldBe((ELockTarget.Character, CharA, true));

        user.SendCharacters();
        Last<S_CharacterListResponse>(b).Characters!.Single(c => c.CharacterId == CharA).IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void 장비를_잠그면_목록에_실린다()
    {
        var (user, b) = NewUser();

        user.TrySetLock(ELockTarget.Equip, Sword, locked: true);

        Last<S_EntityLockResponse>(b).Result.ShouldBe(EResultCode.Ok);
        user.SendEquipList();
        Last<S_EquipListResponse>(b).Equips!.Single(e => e.EquipId == Sword).IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void 해제하면_다시_팔_수_있다()
    {
        var (user, b) = NewUser(lockedA: true);

        user.TrySetLock(ELockTarget.Character, CharA, locked: false);
        user.TrySellEntities(new[] { CharA }, Array.Empty<long>());

        Last<S_EntitySellResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 로그인_때_저장된_잠금을_읽는다()
    {
        var (user, b) = NewUser(lockedA: true, lockedSword: true);

        user.SendCharacters();
        user.SendEquipList();

        Last<S_CharacterListResponse>(b).Characters!.Single(c => c.CharacterId == CharA).IsLocked.ShouldBeTrue();
        Last<S_EquipListResponse>(b).Equips!.Single(e => e.EquipId == Sword).IsLocked.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ELockTarget.Character, 999L, EResultCode.CharacterNotOwned)]
    [InlineData(ELockTarget.Equip,     999L, EResultCode.EquipNotOwned)]
    [InlineData(ELockTarget.None,      CharA, EResultCode.InvalidLockTarget)]
    public void 없는_개체나_대상이면_거절하고_저장하지_않는다(ELockTarget target, long id, EResultCode expected)
    {
        var (user, b) = NewUser();

        user.TrySetLock(target, id, locked: true);

        Last<S_EntityLockResponse>(b).Result.ShouldBe(expected);
        b.DB.Posted.ShouldBeEmpty();
    }

    // ── 없애는 동작은 막는다 ──

    [Fact]
    public void 잠긴_캐릭터가_섞인_판매는_아무것도_팔지_않는다()
    {
        var (user, b) = NewUser(lockedA: true);

        user.TrySellEntities(new[] { CharA }, new[] { Ring });

        Last<S_EntitySellResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.TryGetCharacter(CharA, out _).ShouldBeTrue();
        user.TryGetEquip(Ring, out _).ShouldBeTrue();
    }

    [Fact]
    public void 잠긴_장비는_팔리지_않는다()
    {
        var (user, b) = NewUser(lockedSword: true);

        user.TrySellEntities(Array.Empty<long>(), new[] { Sword });

        Last<S_EntitySellResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
    }

    [Fact]
    public void 잠긴_장비는_경매에_올릴_수_없다()
    {
        var (user, b) = NewUser(lockedSword: true);

        user.TryRegisterAuction(EAuctionKind.Equip, 0, 0, Sword, 70, Now);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
    }

    [Fact]
    public void 잠긴_캐릭터는_경매에_올릴_수_없다()
    {
        var (user, b) = NewUser(lockedA: true);

        user.TryRegisterAuction(EAuctionKind.Character, 0, 0, 0, 50, Now, CharA);

        Last<S_AuctionRegisterResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.TryGetCharacter(CharA, out _).ShouldBeTrue();
    }

    [Fact]
    public void 잠긴_캐릭터는_응축_재료가_될_수_없다()
    {
        var (user, b) = NewUser(lockedA: true);

        user.TryCondenseCharacter(CharB, new[] { CharA, CharC }, Now);

        Last<S_CharacterCondenseResponse>(b).Result.ShouldBe(EResultCode.EntityLocked);
        user.TryGetCharacter(CharC, out _).ShouldBeTrue();
    }

    // ── 쓰는 동작은 그대로 ──

    [Fact]
    public void 잠긴_캐릭터도_응축_대상은_된다()
    {
        var (user, b) = NewUser(lockedA: true);

        user.TryCondenseCharacter(CharA, new[] { CharB }, Now);

        Last<S_CharacterCondenseResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 잠긴_장비도_잠긴_캐릭터에게_장착된다()
    {
        var (user, b) = NewUser(lockedA: true, lockedSword: true);

        user.TryEquip(CharA, Sword, EquipSlot.Weapon, Now);

        Last<S_EquipResponse>(b).Result.ShouldBe(EResultCode.Ok);
    }
}
