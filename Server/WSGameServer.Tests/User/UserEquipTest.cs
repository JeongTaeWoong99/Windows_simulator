using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>의 장비 경로 — 거절 순서 · 자동 이동 · 정산→장착→속도 재확정 순서 · 저장 요청 · 싱크.
/// 속도가 재화 생성량에 곱해지므로, 정산보다 먼저 속도가 바뀌면 소급 지급이 된다.
/// </summary>
public class UserEquipTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;   // 전 산업 적성 ≥ 1
    private const long CharA = 500;
    private const long CharB = 501;

    // 테스트 전용 장비 표 — 엑셀 값에 기대지 않는다.
    private const int SwordTid = 1001;  // 무기 · 전 산업 +10%
    private const int RodTid   = 1002;  // 무기 · 낚시 +30%
    private const int RingTid  = 2001;  // 장신구 · 전 산업 +5%
    private const int GemTid   = 3001;  // 보석 · 농사 +50% (낚시 슬롯엔 0)

    private const long Sword = 10, Rod = 11, Ring = 12, Gem = 13;

    public UserEquipTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWithEquips(params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", EquipKind = EquipKind.Weapon,    Industry = IndustryType.None,    SpeedAddPermille = 100 },
            new EquipTableRow { EquipTID = RodTid,   Name = "대", EquipKind = EquipKind.Weapon,    Industry = IndustryType.Fishing, SpeedAddPermille = 300 },
            new EquipTableRow { EquipTID = RingTid,  Name = "링", EquipKind = EquipKind.Accessory, Industry = IndustryType.None,    SpeedAddPermille = 50 },
            new EquipTableRow { EquipTID = GemTid,   Name = "석", EquipKind = EquipKind.Gem,       Industry = IndustryType.Farming, SpeedAddPermille = 500 },
        });
        var user = b.Build();

        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharA, character_tid = AllRounderTid, level = 1, exp = 0 },
            new CharacterRow { character_id = CharB, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        user.LoadEquips(
            new[]
            {
                new UserEquipRow { equip_id = Sword, equip_tid = SwordTid, slot_position = 0 },
                new UserEquipRow { equip_id = Rod,   equip_tid = RodTid,   slot_position = 1 },
                new UserEquipRow { equip_id = Ring,  equip_tid = RingTid,  slot_position = 2 },
                new UserEquipRow { equip_id = Gem,   equip_tid = GemTid,   slot_position = 3 },
            },
            worn);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static void GiveFishingSlot(User user, long characterId)
    {
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, characterId, Base) });
    }

    private static int ExpectedSpeed(User user, long characterId, int addPermille)
    {
        user.TryGetCharacter(characterId, out var c).ShouldBeTrue();
        return WorkSpeed.From(c.GetBaseWorkSpeed(IndustryType.Fishing))
            .Add(addPermille)
            .Multiply(Global.GatherSpeedMultiplier)
            .Resolve();
    }

    // ─────────────────────── 적재 ───────────────────────

    [Fact]
    public void 로그인_매핑이_착용_상태로_복원된다()
    {
        var (user, _) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryGetEquip(Rod, out var rod).ShouldBeTrue();
        rod.EquippedCharacterId.ShouldBe(CharA);
        rod.EquippedSlot.ShouldBe(EquipSlot.Weapon);
        user.GetEquipSpeedAdd(CharA, IndustryType.Fishing).ShouldBe(300);
    }

    [Fact]
    public void 없는_캐릭터를_가리키는_매핑은_건너뛴다()
    {
        var (user, _) = UserWithEquips(new CharacterEquipRow { character_id = 999, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryGetEquip(Rod, out var rod).ShouldBeTrue();
        rod.IsEquipped.ShouldBeFalse();
    }

    // ─────────────────────── 거절 ───────────────────────

    [Fact]
    public void 미보유_캐릭터면_CharacterNotOwned다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(999, Sword, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.CharacterNotOwned);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 미보유_장비면_EquipNotOwned다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, 999, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.EquipNotOwned);
    }

    [Fact]
    public void None_칸이면_InvalidEquipSlot이다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Sword, EquipSlot.None, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidEquipSlot);
    }

    [Fact]
    public void 무기를_보석_칸에_끼우면_EquipKindMismatch다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Sword, EquipSlot.Gem, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.EquipKindMismatch);
        user.TryGetEquip(Sword, out var sword).ShouldBeTrue();
        sword.IsEquipped.ShouldBeFalse();
    }

    [Fact]
    public void 빈_칸을_해제하면_EquipSlotEmpty다()
    {
        var (user, b) = UserWithEquips();

        user.TryUnequip(CharA, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.EquipSlotEmpty);
    }

    // ─────────────────────── 장착 ───────────────────────

    [Fact]
    public void 장착하면_매핑_저장과_싱크가_나간다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Ring, EquipSlot.Accessory2, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        var sync = b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem();
        var info = sync.Equips.ShouldHaveSingleItem();
        info.EquipId.ShouldBe(Ring);
        info.EquippedCharacterId.ShouldBe(CharA);
        info.EquippedSlot.ShouldBe(EEquipSlot.Accessory2);

        var save = b.DB.PostedOf<SaveCharacterEquipRepository>().ShouldHaveSingleItem();
        save.Changes.ShouldBe(new[] { new EquipMappingChange(CharA, EquipSlot.Accessory2, Ring) });
    }

    [Fact]
    public void 장신구는_두_칸_어디든_들어간다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Ring, EquipSlot.Accessory1, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 같은_자리에_다시_장착하면_Ok지만_아무것도_안_한다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Sword });

        user.TryEquip(CharA, Sword, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.Channel.SentOf<S_EquipSyncResponse>().ShouldBeEmpty();
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 같은_칸에_있던_장비는_창고로_돌아간다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Sword });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base);

        user.TryGetEquip(Sword, out var sword).ShouldBeTrue();
        sword.IsEquipped.ShouldBeFalse();
        var sync = b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem();
        sync.Equips.Select(e => e.EquipId).OrderBy(x => x).ShouldBe(new[] { Sword, Rod });
        sync.Equips.Single(e => e.EquipId == Sword).EquippedCharacterId.ShouldBe(0);
    }

    [Fact]
    public void 다른_캐릭터가_착용_중이면_옮겨_온다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharB, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base);

        user.GetEquipSpeedAdd(CharB, IndustryType.Fishing).ShouldBe(0);
        user.GetEquipSpeedAdd(CharA, IndustryType.Fishing).ShouldBe(300);
        var save = b.DB.PostedOf<SaveCharacterEquipRepository>().ShouldHaveSingleItem();
        save.Changes.ShouldBe(new[]
        {
            new EquipMappingChange(CharB, EquipSlot.Weapon, 0),
            new EquipMappingChange(CharA, EquipSlot.Weapon, Rod),
        });
    }

    [Fact]
    public void 해제하면_창고로_가고_저장_요청이_나간다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Gem, equip_id = Gem });

        user.TryUnequip(CharA, EquipSlot.Gem, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem().Equips.ShouldHaveSingleItem().EquippedCharacterId.ShouldBe(0);
        b.DB.PostedOf<SaveCharacterEquipRepository>().ShouldHaveSingleItem()
            .Changes.ShouldBe(new[] { new EquipMappingChange(CharA, EquipSlot.Gem, 0) });
    }

    // ─────────────────────── 속도 ───────────────────────

    [Fact]
    public void 가산은_전_산업_장비와_슬롯_산업_장비만_합한다()
    {
        var (user, _) = UserWithEquips(
            new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon,     equip_id = Sword },  // 전 산업 +100
            new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Accessory1, equip_id = Ring },   // 전 산업 +50
            new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Gem,        equip_id = Gem });   // 농사 +500 — 낚시엔 0

        user.GetEquipSpeedAdd(CharA, IndustryType.Fishing).ShouldBe(150);
        user.GetEquipSpeedAdd(CharA, IndustryType.Farming).ShouldBe(650);
        user.GetEquipSpeedAdd(0,     IndustryType.Fishing).ShouldBe(0);
    }

    [Fact]
    public void 배치_중인_캐릭터에_장착하면_슬롯_속도가_바뀌어_밀려_온다()
    {
        var (user, b) = UserWithEquips();
        GiveFishingSlot(user, CharA);
        user.RefreshWorkStationSpeed(Base, notify: false);
        b.Channel.Sent.Clear();

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem()
            .Slot.CurrentWorkSpeed.ShouldBe(ExpectedSpeed(user, CharA, 300));
    }

    [Fact]
    public void 장착_전에_먼저_정산한다()
    {
        // 기본 속도 1.0배 = 30초에 1판정. 10분 뒤 장착이면 정확히 20판정이 이전 속도로 정산돼야 한다.
        var (user, b) = UserWithEquips();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, Base, WorkStationSlot.DefaultWorkSpeed) });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base.AddMinutes(10));

        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount.ShouldBe(20);
        var kinds = b.Channel.Sent.Select(p => p.GetType()).ToList();
        kinds.IndexOf(typeof(S_GatherResultResponse)).ShouldBeLessThan(kinds.IndexOf(typeof(S_EquipResponse)));
    }

    // ─────────────────────── 지급 ───────────────────────

    [Fact]
    public void 지급은_첫_빈_창고_칸을_골라_저장을_요청한다()
    {
        var (user, b) = UserWithEquips();   // 칸 0~3이 차 있다

        user.GrantEquip(SwordTid).ShouldBeTrue();

        b.DB.PostedOf<GrantEquipRepository>().ShouldHaveSingleItem();
        // 지급 완료 콜백을 흉내 낸다 — 개체 20이 칸 4에 들어온다.
        user.OnEquipGranted(20, SwordTid, 4);

        user.TryGetEquip(20, out var granted).ShouldBeTrue();
        granted.SlotPosition.ShouldBe(4);
        b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem().Equips.ShouldHaveSingleItem().EquipId.ShouldBe(20);
    }

    [Fact]
    public void 빈_칸이_중간에_있으면_그_칸을_쓴다()
    {
        var b = new TestUserBuilder();
        var user = b.Build();
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = 1, equip_tid = 1001, slot_position = 0 },
            new UserEquipRow { equip_id = 2, equip_tid = 1001, slot_position = 2 },
        }, Array.Empty<CharacterEquipRow>());

        user.NextFreeEquipPosition().ShouldBe(1);
    }

    [Fact]
    public void 지급_대기_중인_칸은_찬_것으로_본다()
    {
        var (user, b) = UserWithEquips();   // 칸 0~3

        user.GrantEquip(SwordTid);
        user.GrantEquip(RingTid);

        b.DB.PostedOf<GrantEquipRepository>().Count.ShouldBe(2);
        user.NextFreeEquipPosition().ShouldBe(6);
    }

    [Fact]
    public void 테이블에_없는_TID는_지급하지_않는다()
    {
        var (user, b) = UserWithEquips();

        user.GrantEquip(9999).ShouldBeFalse();

        b.DB.Posted.ShouldBeEmpty();
    }
}
