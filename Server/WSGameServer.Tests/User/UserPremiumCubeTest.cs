using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 상급 큐브의 이전 값 유지 — 굴린 결과를 보류해 두고 이전·새 값 중 하나를 고른다(T-127).
/// 보류 중 장비를 다른 행동이 건드리면 고르기 전의 결과가 장착·판매·경매로 새어 나간다.
/// </summary>
public class UserPremiumCubeTest
{
    private static readonly EnchantOptionTableRow CommonFish   = Fish(1103, GlobalRarity.Common, 10);
    private static readonly EnchantOptionTableRow UncommonFish = Fish(1203, GlobalRarity.Uncommon, 20);

    private static EnchantOptionTableRow Fish(int tid, GlobalRarity grade, int value)
        => new() { EnchantOptionTID = tid, Grade = grade, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = value, Weight = 120 };

    private static readonly EnchantOptionTableRow[] Pools =
    {
        CommonFish, UncommonFish,
        Fish(1303, GlobalRarity.Rare, 40), Fish(1403, GlobalRarity.Epic, 90),
        Fish(1503, GlobalRarity.Legendary, 180), Fish(1603, GlobalRarity.Mythic, 250),
    };

    // 일반은 항상 오른다 — 상급 큐브를 쓰면 결과가 늘 "고급 2칸"이라 이전 값(일반)과 구별된다.
    private static readonly EnchantGradeTableRow[] GradeRows =
    {
        new() { Grade = GlobalRarity.Common,    SlotCount = 1, UpPermyriad = 10000 },
        new() { Grade = GlobalRarity.Uncommon,  SlotCount = 1, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Rare,      SlotCount = 2, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Epic,      SlotCount = 2, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Legendary, SlotCount = 3, UpPermyriad = 0 },
        new() { Grade = GlobalRarity.Mythic,    SlotCount = 3, UpPermyriad = 0 },
    };

    private const int  CubeTid    = 100015;
    private const int  PremiumTid = 100018;
    private const int  RodTid     = 1203;
    private const long Rod        = 11;
    private const long CharA      = 500;

    private static readonly EnchantItemTableRow[] ItemRows =
    {
        new() { ItemTID = CubeTid,    UpRatePermille = 1000, CanKeepPrevious = false },
        new() { ItemTID = PremiumTid, UpRatePermille = 1250, CanKeepPrevious = true },
    };

    private static EquipTableRow RodRow() => new()
    {
        EquipTID = RodTid, Name = "은사 낚싯대", GlobalRarity = GlobalRarity.Rare, EquipKind = EquipKind.Weapon,
        Industry = IndustryType.Fishing, SpeedAddPermille = 300, BasePrice = 100,
    };

    // 희귀 낚싯대에 일반 낚시 2칸을 심어 둔다 — 이것이 "이전 값"이다.
    private static (User User, TestUserBuilder B) UserWithRod(UserEquipRow? row = null)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[] { RodRow() });
        b.Enchants.Load(Pools, GradeRows, ItemRows);

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[]
        {
            row ?? new UserEquipRow { equip_id = Rod, equip_tid = RodTid, enchant_grade = (int)GlobalRarity.Common, enchant_1 = 1103, enchant_2 = 1103 },
        }, Array.Empty<CharacterEquipRow>());
        user.GainItem(CubeTid, 5);
        user.GainItem(PremiumTid, 5);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static Equip RodOf(User user)
    {
        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        return equip;
    }

    private static void UsePremium(User user) => user.TryEnchant(Rod, PremiumTid, new Random(1));

    private static T Last<T>(TestUserBuilder b) where T : IPacket => b.Channel.SentOf<T>().Last();

    // ───────────────────────── 굴리기 ─────────────────────────

    [Fact]
    public void 상급_큐브를_쓰면_장비는_이전_값_그대로고_새_값은_보류된다()
    {
        var (user, _) = UserWithRod();

        UsePremium(user);

        var rod = RodOf(user);
        rod.EnchantGrade.ShouldBe(GlobalRarity.Common);
        rod.EnchantOptionTids.ShouldBe(new[] { 1103, 1103 });
        rod.PendingEnchantGrade.ShouldBe(GlobalRarity.Uncommon);
        rod.PendingEnchantOptionTids.ShouldBe(new[] { 1203, 1203 });
    }

    [Fact]
    public void 보류_응답은_새_값을_싣고_고르기를_기다린다고_알린다()
    {
        var (user, b) = UserWithRod();

        UsePremium(user);

        var res = Last<S_EquipEnchantResponse>(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.AwaitingChoice.ShouldBeTrue();
        (res.BeforeGrade, res.AfterGrade).ShouldBe(((int)GlobalRarity.Common, (int)GlobalRarity.Uncommon));
        res.Options.ShouldBe(new[] { 1203, 1203 });
    }

    [Fact]
    public void 보류된_새_값은_싱크에_따로_실린다()
    {
        var (user, b) = UserWithRod();

        UsePremium(user);

        var info = b.Channel.SentOf<S_EquipSyncResponse>().Single().Equips.Single();
        info.EnchantOptions.ShouldBe(new[] { 1103, 1103 });
        info.PendingEnchantGrade.ShouldBe((int)GlobalRarity.Uncommon);
        info.PendingEnchantOptions.ShouldBe(new[] { 1203, 1203 });
    }

    [Fact]
    public void 보류도_큐브_소모와_함께_저장된다()
    {
        var (user, b) = UserWithRod();

        UsePremium(user);

        user.GetItemCount(PremiumTid).ShouldBe(4);
        var saved = b.DB.PostedOf<SaveEquipEnchantRepository>().Single();
        (saved.Grade, saved.Option1, saved.Option2).ShouldBe(((int)GlobalRarity.Common, 1103, 1103));
        (saved.PendingGrade, saved.Pending1, saved.Pending2, saved.Pending3).ShouldBe(((int)GlobalRarity.Uncommon, 1203, 1203, 0));
    }

    [Fact]
    public void 인챈트_큐브는_보류_없이_바로_덮어쓴다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, CubeTid, new Random(1));

        RodOf(user).EnchantGrade.ShouldBe(GlobalRarity.Uncommon);
        RodOf(user).PendingEnchantGrade.ShouldBe(GlobalRarity.None);
        Last<S_EquipEnchantResponse>(b).AwaitingChoice.ShouldBeFalse();
    }

    // ───────────────────────── 고르기 ─────────────────────────

    [Fact]
    public void 새_값을_고르면_보류가_장비에_적용되고_보류는_비워진다()
    {
        var (user, b) = UserWithRod();
        UsePremium(user);
        b.DB.Posted.Clear();

        user.TryChooseEnchant(Rod, keepNew: true);

        var rod = RodOf(user);
        rod.EnchantGrade.ShouldBe(GlobalRarity.Uncommon);
        rod.EnchantOptionTids.ShouldBe(new[] { 1203, 1203 });
        rod.PendingEnchantGrade.ShouldBe(GlobalRarity.None);
        var saved = b.DB.PostedOf<SaveEquipEnchantRepository>().Single();
        (saved.Grade, saved.Option1, saved.PendingGrade, saved.Pending1).ShouldBe(((int)GlobalRarity.Uncommon, 1203, 0, 0));
    }

    [Fact]
    public void 이전_값을_고르면_등급_상승까지_되돌아간다()
    {
        var (user, b) = UserWithRod();
        UsePremium(user);
        b.DB.Posted.Clear();

        user.TryChooseEnchant(Rod, keepNew: false);

        var rod = RodOf(user);
        rod.EnchantGrade.ShouldBe(GlobalRarity.Common);
        rod.EnchantOptionTids.ShouldBe(new[] { 1103, 1103 });
        rod.PendingEnchantGrade.ShouldBe(GlobalRarity.None);
        b.DB.PostedOf<SaveEquipEnchantRepository>().Single().PendingGrade.ShouldBe(0);
    }

    [Fact]
    public void 고르면_응답과_싱크가_온다()
    {
        var (user, b) = UserWithRod();
        UsePremium(user);
        b.Channel.Sent.Clear();

        user.TryChooseEnchant(Rod, keepNew: true);

        var res = Last<S_EquipEnchantChooseResponse>(b);
        (res.Result, res.EquipId, res.KeepNew).ShouldBe((EResultCode.Ok, Rod, true));
        var info = b.Channel.SentOf<S_EquipSyncResponse>().Single().Equips.Single();
        info.EnchantOptions.ShouldBe(new[] { 1203, 1203 });
        info.PendingEnchantGrade.ShouldBe(0);
    }

    [Fact]
    public void 보류가_없으면_고르기는_거절된다()
    {
        var (user, b) = UserWithRod();

        user.TryChooseEnchant(Rod, keepNew: true);

        Last<S_EquipEnchantChooseResponse>(b).Result.ShouldBe(EResultCode.EnchantNoPending);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 미보유_장비의_고르기는_거절된다()
    {
        var (user, b) = UserWithRod();

        user.TryChooseEnchant(999999, keepNew: true);

        Last<S_EquipEnchantChooseResponse>(b).Result.ShouldBe(EResultCode.EquipNotOwned);
    }

    // ───────────────────────── 보류 중 거절 ─────────────────────────

    [Fact]
    public void 보류_중에는_큐브를_다시_쓸_수_없고_큐브가_남는다()
    {
        var (user, b) = UserWithRod();
        UsePremium(user);

        user.TryEnchant(Rod, CubeTid, new Random(1));

        Last<S_EquipEnchantResponse>(b).Result.ShouldBe(EResultCode.EnchantPending);
        user.GetItemCount(CubeTid).ShouldBe(5);
        RodOf(user).PendingEnchantGrade.ShouldBe(GlobalRarity.Uncommon);
    }

    [Fact]
    public void 보류_중에는_장착할_수_없다()
    {
        var (user, b) = UserWithRod();
        UsePremium(user);

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, TestUserBuilder.Base);

        Last<S_EquipResponse>(b).Result.ShouldBe(EResultCode.EnchantPending);
        RodOf(user).IsEquipped.ShouldBeFalse();
    }

    [Fact]
    public void 보류_중에는_팔_수_없다()
    {
        var (user, b) = UserWithRod();
        UsePremium(user);

        user.TrySellEntities(Array.Empty<long>(), new[] { Rod });

        Last<S_EntitySellResponse>(b).Result.ShouldBe(EResultCode.EnchantPending);
        user.TryGetEquip(Rod, out _).ShouldBeTrue();
    }

    // ───────────────────────── 로드 ─────────────────────────

    [Fact]
    public void 저장된_보류는_재접속해도_남아_고를_수_있다()
    {
        var (user, _) = UserWithRod(new UserEquipRow
        {
            equip_id = Rod, equip_tid = RodTid, enchant_grade = (int)GlobalRarity.Common, enchant_1 = 1103, enchant_2 = 1103,
            pending_grade = (int)GlobalRarity.Uncommon, pending_1 = 1203, pending_2 = 1203,
        });

        user.TryChooseEnchant(Rod, keepNew: true);

        RodOf(user).EnchantOptionTids.ShouldBe(new[] { 1203, 1203 });
    }

    [Fact]
    public void 풀이_없는_등급으로_저장된_보류는_버려진다()
    {
        var (user, _) = UserWithRod(new UserEquipRow
        {
            equip_id = Rod, equip_tid = RodTid, enchant_grade = (int)GlobalRarity.Common, enchant_1 = 1103, enchant_2 = 1103,
            pending_grade = 99, pending_1 = 1203,
        });

        RodOf(user).PendingEnchantGrade.ShouldBe(GlobalRarity.None);
        RodOf(user).EnchantOptionTids.ShouldBe(new[] { 1103, 1103 });
    }
}
