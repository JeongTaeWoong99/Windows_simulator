using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 장비 인챈트와 큐브 — 효과 합산 · 첫 부여는 일반 · 한 단계 상승 · 칸 전부 다시 뽑기.
/// 착용 중인 장비를 거절하는 것이 이 경로의 뼈대다: 이 거절이 무너지면 가동 중인 슬롯 속도가 소급으로 바뀐다.
/// </summary>
public class UserEnchantTest
{
    // 등급마다 낚시 속도 한 줄 — 값은 실데이터(EnchantOptionTable)와 같다.
    private static readonly EnchantOptionTableRow CommonFish   = Fish(1103, GlobalRarity.Common, 10);
    private static readonly EnchantOptionTableRow UncommonFish = Fish(1203, GlobalRarity.Uncommon, 20);
    private static readonly EnchantOptionTableRow RareFish     = Fish(1303, GlobalRarity.Rare, 40);
    private static readonly EnchantOptionTableRow EpicFish     = Fish(1403, GlobalRarity.Epic, 90);
    private static readonly EnchantOptionTableRow LegendFish   = Fish(1503, GlobalRarity.Legendary, 180);
    private static readonly EnchantOptionTableRow MythicFish   = Fish(1603, GlobalRarity.Mythic, 250);

    // 풀에는 넣지 않는 줄 — 칸에 직접 심어 두고 "다시 뽑혔는가"를 본다.
    private static readonly EnchantOptionTableRow UncommonFarm =
        new() { EnchantOptionTID = 1202, Grade = GlobalRarity.Uncommon, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Farming, Value = 20, Weight = 120 };

    private static readonly EnchantOptionTableRow RareAll =
        new() { EnchantOptionTID = 1301, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 20, Weight = 300 };

    private static readonly EnchantOptionTableRow RareExp =
        new() { EnchantOptionTID = 1307, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None, Value = 30, Weight = 100 };

    private static EnchantOptionTableRow Fish(int tid, GlobalRarity grade, int value)
        => new() { EnchantOptionTID = tid, Grade = grade, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = value, Weight = 120 };

    private static readonly EnchantOptionTableRow[] Pools = { CommonFish, UncommonFish, RareFish, EpicFish, LegendFish, MythicFish };

    // 판정이 시드에 매이지 않게 상승 확률을 극단값으로 둔다 — 일반은 항상 오르고 고급은 절대 오르지 않는다.
    private static readonly EnchantGradeTableRow[] GradeRows =
    {
        new() { Grade = GlobalRarity.Common,    SlotCount = 1, UpPermyriad = 10000 },
        new() { Grade = GlobalRarity.Uncommon,  SlotCount = 1, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Rare,      SlotCount = 2, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Epic,      SlotCount = 2, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Legendary, SlotCount = 3, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Mythic,    SlotCount = 3, UpPermyriad = 0 },
    };

    private const int CubeTid      = 100015;
    private const int ZeroOwnedTid = 100018;   // 표에는 있지만 지급하지 않는 큐브 — "보유 0" 경로 전용
    private const int RodTid       = 1203;
    private const long Rod         = 11;
    private const long CharA       = 500;

    private static readonly EnchantItemTableRow[] ItemRows =
    {
        new() { ItemTID = CubeTid,      UpRatePermille = 1000 },
        new() { ItemTID = ZeroOwnedTid, UpRatePermille = 1250 },
    };

    // 희귀 낚싯대(낚시 +30%) — 칸 2개.
    private static EquipTableRow RodRow(int speedAdd = 300) => new()
    {
        EquipTID = RodTid, Name = "은사 낚싯대", GlobalRarity = GlobalRarity.Rare, EquipKind = EquipKind.Weapon,
        Industry = IndustryType.Fishing, SpeedAddPermille = speedAdd,
    };

    private static Equip RodWith(GlobalRarity grade, params EnchantOptionTableRow[] options)
    {
        var equip = new Equip(Rod, RodRow(), 0);
        equip.SetEnchant(grade, options);
        return equip;
    }

    // ───────────────────────── 효과 합산 ─────────────────────────

    [Fact]
    public void 산업이_일치하는_칸과_전_산업_칸이_기본값에_더해진다()
    {
        RodWith(GlobalRarity.Rare, RareFish, RareAll).SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40 + 20);
    }

    [Fact]
    public void 산업이_다른_칸은_더해지지_않는다()
    {
        var equip = RodWith(GlobalRarity.Uncommon, UncommonFarm, UncommonFish);

        // 장비 자체가 낚시 전용이라 농사 슬롯에서는 기본값도 0이다.
        equip.SpeedAddPermilleFor(IndustryType.Farming).ShouldBe(20);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 20);
    }

    [Fact]
    public void 경험치_칸은_속도에_섞이지_않는다()
    {
        var equip = RodWith(GlobalRarity.Rare, RareExp, RareFish);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40);
        equip.ExpAddPermille.ShouldBe(30);
    }

    [Fact]
    public void 인챈트가_없으면_기본값만_남는다()
    {
        var equip = new Equip(Rod, RodRow(), 0);

        equip.EnchantGrade.ShouldBe(GlobalRarity.None);
        equip.EnchantLineCount.ShouldBe(0);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300);
    }

    // ───────────────────────── 큐브 ─────────────────────────

    private static (User User, TestUserBuilder B) UserWithRod(params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[] { RodRow() });
        b.Enchants.Load(Pools, GradeRows, ItemRows);

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[] { new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0 } }, worn);
        user.GainItem(CubeTid, 5);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static Equip RodOf(User user)
    {
        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        return equip;
    }

    private static S_EquipEnchantResponse LastResponse(TestUserBuilder b)
        => b.Channel.SentOf<S_EquipEnchantResponse>().Last();

    private static void UseCube(User user, int itemTid = CubeTid)
        => user.TryEnchant(Rod, itemTid, new Random(1));

    [Fact]
    public void 처음_쓰면_일반_등급으로_칸_수만큼_채운다()
    {
        // 일반의 상승 확률이 100%여도 첫 부여는 상승 판정 없이 일반으로 시작한다. 희귀 장비라 2칸이다.
        var (user, b) = UserWithRod();

        UseCube(user);

        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.Common);
        RodOf(user).EnchantOptionTids.ShouldBe(new[] { 1103, 1103 });
        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeFalse();
        res.BeforeGrade.ShouldBe(0);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Common);
        res.Options.ShouldBe(new[] { 1103, 1103 });
    }

    [Fact]
    public void 큐브를_쓰면_하나가_소모되고_개체가_싱크된다()
    {
        var (user, b) = UserWithRod();

        UseCube(user);

        user.GetItemCount(CubeTid).ShouldBe(4);
        b.Channel.SentOf<S_EquipSyncResponse>().Single().Equips.Single().EnchantOptions.ShouldBe(new[] { 1103, 1103 });
    }

    [Fact]
    public void 상승에_성공하면_한_단계_오르고_칸_전부를_새_등급으로_뽑는다()
    {
        var (user, b) = UserWithRod();
        RodOf(user).SetEnchant(GlobalRarity.Common, new[] { CommonFish, CommonFish });

        UseCube(user);

        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.Uncommon);
        RodOf(user).EnchantOptionTids.ShouldBe(new[] { 1203, 1203 });
        var res = LastResponse(b);
        res.Success.ShouldBeTrue();
        (res.BeforeGrade, res.AfterGrade).ShouldBe(((int)GlobalRarity.Common, (int)GlobalRarity.Uncommon));
    }

    [Fact]
    public void 상승에_실패해도_같은_등급에서_칸_전부를_다시_뽑는다()
    {
        // 고급의 상승 확률은 0이다. 풀에 없는 고급 농사 칸을 심어 두면, 다시 뽑혔을 때만 사라진다.
        var (user, b) = UserWithRod();
        RodOf(user).SetEnchant(GlobalRarity.Uncommon, new[] { UncommonFarm, UncommonFarm });

        UseCube(user);

        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.Uncommon);
        RodOf(user).EnchantOptionTids.ShouldBe(new[] { 1203, 1203 });
        LastResponse(b).Success.ShouldBeFalse();
    }

    [Fact]
    public void 저장_페이로드는_등급과_칸_위치_그대로다()
    {
        var (user, b) = UserWithRod();

        UseCube(user);

        var saved = b.DB.PostedOf<SaveEquipEnchantRepository>().Single();
        (saved.EquipId, saved.Grade).ShouldBe((Rod, (int)GlobalRarity.Common));
        (saved.Option1, saved.Option2, saved.Option3).ShouldBe((1103, 1103, 0));
    }

    [Fact]
    public void 착용_중인_장비는_거부되고_큐브가_남는다()
    {
        var (user, b) = UserWithRod(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        UseCube(user);

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantEquipped);
        user.GetItemCount(CubeTid).ShouldBe(5);
        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.None);
    }

    [Fact]
    public void 큐브가_아니면_쓸_수_없는_아이템으로_거부된다()
    {
        var (user, b) = UserWithRod();

        UseCube(user, itemTid: 99999);

        LastResponse(b).Result.ShouldBe(EResultCode.ItemNotUsable);
    }

    [Fact]
    public void 큐브가_없으면_거부되고_아무것도_바뀌지_않는다()
    {
        var (user, b) = UserWithRod();

        UseCube(user, itemTid: ZeroOwnedTid);

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantItemNotOwned);
        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.None);
    }

    [Fact]
    public void 미보유_장비는_거부된다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(999999, CubeTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EquipNotOwned);
    }

    // ───────────────────────── 로드 ─────────────────────────

    [Fact]
    public void 칸_수를_넘게_저장된_칸과_없는_옵션은_버린다()
    {
        // 희귀 장비 2칸 — 9999(표에 없음)는 건너뛰고, 남은 셋 중 앞의 둘만 남는다.
        var (user, _) = UserWithRod();
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0, enchant_grade = (int)GlobalRarity.Rare, enchant_1 = 9999, enchant_2 = 1303, enchant_3 = 1303 },
        }, Array.Empty<CharacterEquipRow>());

        RodOf(user).EnchantOptionTids.ShouldBe(new[] { 1303, 1303 });
    }

    [Fact]
    public void 풀이_없는_등급으로_저장된_인챈트는_로드에서_버려진다()
    {
        var (user, _) = UserWithRod();
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0, enchant_grade = 99, enchant_1 = 1303 },
        }, Array.Empty<CharacterEquipRow>());

        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.None);
        RodOf(user).EnchantLineCount.ShouldBe(0);
    }

    // ───────────────────────── 속도·경험치 반영 ─────────────────────────

    private static int SlotSpeed(User user)
    {
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        return slot.CurrentWorkSpeed;
    }

    private static int ExpectedSpeed(User user, IndustryType industry, int addPermille)
    {
        user.TryGetCharacter(CharA, out var c).ShouldBeTrue();
        return WorkSpeed.From(c.GetBaseWorkSpeed(industry))
            .Add(addPermille)
            .Resolve();
    }

    [Fact]
    public void 인챈트한_장비를_장착하면_가산이_속도에_실린다()
    {
        // 낚싯대 +300에 일반 낚시 칸 +10 두 개가 붙는다 → +320.
        var (user, _) = UserWithRod();
        UseCube(user);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, TestUserBuilder.Base) });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, TestUserBuilder.Base);

        SlotSpeed(user).ShouldBe(ExpectedSpeed(user, IndustryType.Fishing, 320));
    }

    [Fact]
    public void 산업이_다른_장비의_칸도_맞는_슬롯에서는_속도에_실린다()
    {
        // 낚싯대(낚시 +30%)에 고급 농사 +2% 두 칸. 농사 슬롯에서는 기본값 300이 빠지고 칸만 붙는다 → +40.
        var (user, _) = UserWithRod();
        RodOf(user).SetEnchant(GlobalRarity.Uncommon, new[] { UncommonFarm, UncommonFarm });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Farming, CharA, TestUserBuilder.Base) });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, TestUserBuilder.Base);

        SlotSpeed(user).ShouldBe(ExpectedSpeed(user, IndustryType.Farming, 40));
    }

    /// <summary>
    /// 낚시 슬롯에 캐릭터를 배치하고 속도 가산 0인 낚시대를 채운 뒤 5분 정산한다. 판정당 경험치 100, 레벨업은 일어나지 않는다.
    /// enchanted면 희귀 경험치 칸(+30‰) 두 개를 심는다.
    /// </summary>
    private static (long Exp, int Judges) SettleWithRod(bool enchanted)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Equips.Load(new[] { RodRow(speedAdd: 0) });
        b.Enchants.Load(Pools, GradeRows, ItemRows);
        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow
            {
                IndustryLevelTID = 201, IndustryType = IndustryType.Fishing, Level = 1,
                Name = "개울", RequiredScore = 30_000, ExpPerJudge = 100,
            },
        });
        b.Growth.Load(new[]
        {
            new CharacterLevelTableRow { CharacterLevelTID = 1, RequiredExp = 0 },
            new CharacterLevelTableRow { CharacterLevelTID = 2, RequiredExp = 1_000_000 },
        });

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[] { new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0 } }, Array.Empty<CharacterEquipRow>());
        if (enchanted)
        {
            RodOf(user).SetEnchant(GlobalRarity.Rare, new[] { RareExp, RareExp });
        }

        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, TestUserBuilder.Base) });
        user.TryEquip(CharA, Rod, EquipSlot.Weapon, TestUserBuilder.Base);
        b.Channel.Sent.Clear();

        user.SettleWorkStation(TestUserBuilder.Base.AddMinutes(5));

        var judges = b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount;
        user.TryGetCharacter(CharA, out var character).ShouldBeTrue();
        character.Level.ShouldBe(1);
        return (character.Exp, judges);
    }

    [Fact]
    public void 정산하면_경험치_칸_가산만큼_더_번다()
    {
        // 판정당 100 × (1000 + 30 × 2) / 1000 = 106.
        var (exp, judges) = SettleWithRod(enchanted: true);

        judges.ShouldBeGreaterThan(0);
        exp.ShouldBe(judges * 106L);
    }

    [Fact]
    public void 경험치_칸이_없으면_정산_경험치는_기본값_그대로다()
    {
        var (exp, judges) = SettleWithRod(enchanted: false);

        judges.ShouldBeGreaterThan(0);
        exp.ShouldBe(judges * 100L);
    }
}
