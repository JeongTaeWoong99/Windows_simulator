using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 인챈트 — 효과 합산과 부여·재롤·줄 확장. 착용 중인 장비를 거절하는 것이 이 경로의 뼈대다:
/// 그래서 정산·속도 재확정이 없다. 이 거절이 무너지면 가동 중인 슬롯 속도가 소급으로 바뀐다.
/// </summary>
public class UserEnchantTest
{
    private static readonly EnchantOptionTableRow FishSpeed =
        new() { EnchantOptionTID = 103, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = 40, Weight = 120 };

    private static readonly EnchantOptionTableRow AllSpeed =
        new() { EnchantOptionTID = 101, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 20, Weight = 300 };

    private static readonly EnchantOptionTableRow FarmSpeed =
        new() { EnchantOptionTID = 102, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Farming, Value = 40, Weight = 120 };

    private static readonly EnchantOptionTableRow Exp =
        new() { EnchantOptionTID = 107, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None, Value = 30, Weight = 100 };

    // 201·203은 실제 EnchantOptionTable의 행이다 — 값도 거기에 맞춘다(전 산업 +5% · 낚시 +9%).
    // 실데이터와 어긋난 픽스처는 합산을 단언하는 쪽에서 함정이 된다.
    private static readonly EnchantOptionTableRow EpicAllSpeed =
        new() { EnchantOptionTID = 201, Grade = GlobalRarity.Epic, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 50, Weight = 300 };

    private static readonly EnchantOptionTableRow EpicFishSpeed =
        new() { EnchantOptionTID = 203, Grade = GlobalRarity.Epic, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = 90, Weight = 120 };

    private static Equip RodWith(params EnchantOptionTableRow[] options)
    {
        // 낚시 무기 +30%. 인챈트 줄이 그 위에 더해진다.
        var row = new EquipTableRow
        {
            EquipTID = 1002, Name = "대", EquipKind = EquipKind.Weapon,
            Industry = IndustryType.Fishing, SpeedAddPermille = 300,
        };

        var equip = new Equip(11, row, 0);
        equip.SetEnchant(GlobalRarity.Rare, options);
        return equip;
    }

    [Fact]
    public void 산업이_일치하는_줄과_전_산업_줄이_기본값에_더해진다()
    {
        var equip = RodWith(FishSpeed, AllSpeed);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40 + 20);
    }

    [Fact]
    public void 산업이_다른_줄은_더해지지_않는다()
    {
        var equip = RodWith(FarmSpeed, AllSpeed);

        // 장비 자체가 낚시 전용이라 농사 슬롯에서는 기본값도 0이다.
        equip.SpeedAddPermilleFor(IndustryType.Farming).ShouldBe(40 + 20);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 20);
    }

    [Fact]
    public void 경험치_줄은_속도에_섞이지_않는다()
    {
        var equip = RodWith(Exp, FishSpeed);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40);
        equip.ExpAddPermille.ShouldBe(30);
    }

    [Fact]
    public void 인챈트가_없으면_기본값만_남는다()
    {
        var row = new EquipTableRow
        {
            EquipTID = 1002, Name = "대", EquipKind = EquipKind.Weapon,
            Industry = IndustryType.Fishing, SpeedAddPermille = 300,
        };
        var equip = new Equip(11, row, 0);

        equip.EnchantGrade.ShouldBe(GlobalRarity.None);
        equip.EnchantLineCount.ShouldBe(0);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300);
        equip.ExpAddPermille.ShouldBe(0);
    }

    // ───────────────────────── 부여·재롤·확장 ─────────────────────────

    private const int GrantTid  = 9001;  // Grant · 확정
    private const int CubeTid   = 9003;  // GradeUp
    private const int ExpandTid = 9004;  // ExpandLine · 확정
    private const int RodTid    = 1002;
    private const long Rod      = 11;
    private const long CharA    = 500;

    // 실패 경로 전용 — SuccessPermille=0이라 결과가 시드에 상관없이 항상 실패한다.
    private const int FailGrantTid  = 9002;  // Grant · 확정 실패
    private const int FailExpandTid = 9005;  // ExpandLine · 확정 실패

    // ItemRows에는 등록돼 있지만 UserWithRod가 GainItem을 부르지 않는 TID — "보유 0" 경로 전용.
    private const int ZeroOwnedTid = 9006;

    private static readonly EnchantGradeTableRow[] GradeRows =
    {
        new() { Grade = GlobalRarity.Rare,      UpPermille = 1000 },  // 테스트에서는 확정 상승으로 둔다
        new() { Grade = GlobalRarity.Epic,      UpPermille = 0 },
        new() { Grade = GlobalRarity.Legendary, UpPermille = 0 },
    };

    private static readonly EnchantItemTableRow[] ItemRows =
    {
        new() { ItemTID = GrantTid,      Action = EnchantAction.Grant,      SuccessPermille = 1000 },
        new() { ItemTID = CubeTid,       Action = EnchantAction.GradeUp,    SuccessPermille = 1000 },
        new() { ItemTID = ExpandTid,     Action = EnchantAction.ExpandLine, SuccessPermille = 1000 },
        new() { ItemTID = FailGrantTid,  Action = EnchantAction.Grant,      SuccessPermille = 0 },
        new() { ItemTID = FailExpandTid, Action = EnchantAction.ExpandLine, SuccessPermille = 0 },
        new() { ItemTID = ZeroOwnedTid,  Action = EnchantAction.Grant,      SuccessPermille = 1000 },
    };

    private static (User User, TestUserBuilder B) UserWithRod(params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = RodTid, Name = "대", EquipKind = EquipKind.Weapon, Industry = IndustryType.Fishing, SpeedAddPermille = 300 },
        });
        b.Enchants.Load(new[] { AllSpeed, FishSpeed, Exp, EpicAllSpeed, EpicFishSpeed }, GradeRows, ItemRows);

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[] { new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0 } }, worn);
        user.GainItem(GrantTid, 5);
        user.GainItem(CubeTid, 5);
        user.GainItem(ExpandTid, 5);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_EquipEnchantResponse LastResponse(TestUserBuilder b)
        => b.Channel.SentOf<S_EquipEnchantResponse>().Last();

    [Fact]
    public void 착용_중인_장비는_인챈트가_거부된다()
    {
        var (user, b) = UserWithRod(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryEnchant(Rod, GrantTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantEquipped);
        user.GetItemCount(GrantTid).ShouldBe(5);   // 소모되지 않는다
    }

    [Fact]
    public void 인챈트가_없는_장비에_큐브를_쓰면_거부된다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, CubeTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantNotRolled);
    }

    [Fact]
    public void 이미_인챈트가_있으면_부여가_거부된다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryEnchant(Rod, GrantTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantAlreadyRolled);
    }

    [Fact]
    public void 아이템이_없으면_거부된다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, 99999, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantItemNotOwned);
    }

    [Fact]
    public void 세_줄이_되면_확장이_거부된다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryEnchant(Rod, ExpandTid, new Random(1));

        user.TryEnchant(Rod, ExpandTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantLineMax);
    }

    [Fact]
    public void 부여하면_Rare_두_줄이_생긴다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        equip.EnchantGrade.ShouldBe(GlobalRarity.Rare);
        equip.EnchantLineCount.ShouldBe(2);

        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeTrue();
        res.BeforeGrade.ShouldBe(0);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Rare);
        res.Options.Count.ShouldBe(2);

        user.GetItemCount(GrantTid).ShouldBe(4);
        b.DB.PostedOf<SaveEquipEnchantRepository>().Count.ShouldBe(1);
        b.Channel.SentOf<S_EquipSyncResponse>().Count.ShouldBe(1);
    }

    [Fact]
    public void 큐브가_성공하면_등급이_오르고_줄을_다시_뽑는다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryEnchant(Rod, CubeTid, new Random(1));

        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        equip.EnchantGrade.ShouldBe(GlobalRarity.Epic);
        equip.EnchantLineCount.ShouldBe(2);   // 줄 수는 그대로다

        var res = LastResponse(b);
        res.BeforeGrade.ShouldBe((int)GlobalRarity.Rare);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Epic);
        res.Success.ShouldBeTrue();
    }

    [Fact]
    public void 큐브가_실패해도_줄은_다시_뽑는다()
    {
        // Epic의 UpPermille이 0이라 상승은 반드시 실패한다.
        var (user, b) = UserWithRod();

        // Epic 등급에 Rare 풀에서만 나오는 TID(AllSpeed·FishSpeed)를 일부러 심어 둔다.
        // 재롤이 실제로 실행되면 이 값은 반드시 사라진다 — 실행되지 않으면 그대로 남는다.
        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        equip.SetEnchant(GlobalRarity.Epic, new List<EnchantOptionTableRow> { AllSpeed, FishSpeed });
        var beforeTids = equip.EnchantOptionTids.ToList();

        user.TryEnchant(Rod, CubeTid, new Random(7));

        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeFalse();
        res.BeforeGrade.ShouldBe((int)GlobalRarity.Epic);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Epic);
        res.Options.Count.ShouldBe(2);                            // 재롤은 됐다
        res.Options.Intersect(beforeTids).ShouldBeEmpty();        // 심어 둔 Rare TID는 사라졌다
        res.Options.ShouldAllBe(tid => tid == 201 || tid == 203); // Epic 풀에서만 나온다

        user.GetItemCount(CubeTid).ShouldBe(4);
    }

    [Fact]
    public void 확장하면_세_줄이_되고_새_줄만_늘어난다()
    {
        var (user, _) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryGetEquip(Rod, out var before).ShouldBeTrue();
        var kept = before.EnchantOptionTids.ToList();

        user.TryEnchant(Rod, ExpandTid, new Random(1));

        user.TryGetEquip(Rod, out var after).ShouldBeTrue();
        after.EnchantLineCount.ShouldBe(3);
        after.EnchantOptionTids.Take(2).ShouldBe(kept);   // 기존 줄은 유지된다
    }

    [Fact]
    public void 부여가_실패하면_등급이_없는_채로_남는다()
    {
        var (user, b) = UserWithRod();
        user.GainItem(FailGrantTid, 1);

        user.TryEnchant(Rod, FailGrantTid, new Random(1));

        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        equip.EnchantGrade.ShouldBe(GlobalRarity.None);
        equip.EnchantLineCount.ShouldBe(0);

        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeFalse();
    }

    [Fact]
    public void 확장이_실패하면_줄_수도_기존_줄도_그대로다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryGetEquip(Rod, out var before).ShouldBeTrue();
        var beforeTids = before.EnchantOptionTids.ToList();
        user.GainItem(FailExpandTid, 1);

        user.TryEnchant(Rod, FailExpandTid, new Random(1));

        user.TryGetEquip(Rod, out var after).ShouldBeTrue();
        after.EnchantLineCount.ShouldBe(2);
        after.EnchantOptionTids.ShouldBe(beforeTids);   // 실패면 아무것도 안 바뀐다

        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeFalse();
    }

    [Fact]
    public void 두_줄_인챈트의_저장_페이로드는_세_번째_칸이_비어_있다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        var tids = equip.EnchantOptionTids;

        var saved = b.DB.PostedOf<SaveEquipEnchantRepository>().Single();
        saved.EquipId.ShouldBe(Rod);
        saved.Grade.ShouldBe((int)GlobalRarity.Rare);
        saved.Option1.ShouldBe(tids[0]);
        saved.Option2.ShouldBe(tids[1]);
        saved.Option3.ShouldBe(0);   // 2줄이라 세 번째 칸은 저장하지 않는다
    }

    [Fact]
    public void 미보유_장비는_거부된다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(999999, GrantTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EquipNotOwned);
    }

    [Fact]
    public void 등록된_아이템이어도_보유가_0이면_거부된다()
    {
        // 99999(테이블에 없음)와 달리 ZeroOwnedTid는 EnchantItemTable에 있다 —
        // TryGetItem은 통과하고 그다음 보유 수량 검사에서 걸린다.
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, ZeroOwnedTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantItemNotOwned);
        user.GetItemCount(ZeroOwnedTid).ShouldBe(0);
    }
}
