using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 캐릭터·장비 개체 즉시 판매. <b>전부 성공하거나 전부 실패한다</b> — 하나라도 걸리면 아무것도 지우지 않는다.
/// 착용·배치 중인 개체를 팔면 슬롯이 없는 캐릭터를 돌리거나 착용 매핑이 허공을 가리키게 된다.
/// </summary>
public class UserEntitySellTest
{
    public UserEntitySellTest() => GameTableFixture.EnsureLoaded();

    private const int  SwordTid = 9101;
    private const long Sword    = 11;
    private const long Ring     = 12;
    private const long CharA    = 500;   // 램볼(1001) — Common, BasePrice 50
    private const long CharB    = 501;

    private static (User User, TestUserBuilder B) NewUser(params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검", GlobalRarity = GlobalRarity.Common, EquipKind = EquipKind.Weapon,
                                Industry = IndustryType.None, SpeedAddPermille = 100, BasePrice = 70 },
        });

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 },
            new CharacterRow { character_id = CharB, character_tid = 1001, level = 1, exp = 0 },
        });
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = Sword, equip_tid = SwordTid, slot_position = 0 },
            new UserEquipRow { equip_id = Ring,  equip_tid = SwordTid, slot_position = 1 },
        }, worn);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_EntitySellResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_EntitySellResponse>().Last();

    [Fact]
    public void 장비와_캐릭터를_팔면_기준가만큼_골드가_들어오고_사라진다()
    {
        // 장비 70 + 캐릭터 50 = 120 (즉시 판매가 100%)
        var (user, b) = NewUser();
        var before = user.Gold;

        user.TrySellEntities(new[] { CharA }, new[] { Sword });

        (Last(b).Result, Last(b).GainedGold).ShouldBe((EResultCode.Ok, 120L));
        user.Gold.ShouldBe(before + 120);
        user.TryGetEquip(Sword, out _).ShouldBeFalse();
        user.TryGetCharacter(CharA, out _).ShouldBeFalse();
    }

    [Fact]
    public void 판매는_개체_삭제와_잔액을_한_번에_저장한다()
    {
        var (user, b) = NewUser();

        user.TrySellEntities(new[] { CharA }, new[] { Sword, Ring });

        var saved = b.DB.PostedOf<SellEntitiesRepository>().Single();
        saved.CharacterIds.ShouldBe(new[] { CharA });
        saved.EquipIds.ShouldBe(new[] { Sword, Ring });
    }

    [Fact]
    public void 착용_중인_장비가_섞이면_아무것도_팔리지_않는다()
    {
        var (user, b) = NewUser(new CharacterEquipRow { character_id = CharB, slot = (int)EquipSlot.Weapon, equip_id = Sword });

        user.TrySellEntities(Array.Empty<long>(), new[] { Ring, Sword });

        Last(b).Result.ShouldBe(EResultCode.SellEquipWorn);
        user.TryGetEquip(Ring, out _).ShouldBeTrue();
        b.DB.PostedOf<SellEntitiesRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 장비를_낀_캐릭터는_팔_수_없다()
    {
        var (user, b) = NewUser(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Sword });

        user.TrySellEntities(new[] { CharA }, Array.Empty<long>());

        Last(b).Result.ShouldBe(EResultCode.SellCharacterBusy);
    }

    [Fact]
    public void 슬롯에_배치된_캐릭터는_팔_수_없다()
    {
        var (user, b) = NewUser();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, TestUserBuilder.Base) });

        user.TrySellEntities(new[] { CharA }, Array.Empty<long>());

        Last(b).Result.ShouldBe(EResultCode.SellCharacterBusy);
        user.TryGetCharacter(CharA, out _).ShouldBeTrue();
    }

    [Fact]
    public void 캐릭터를_하나도_남기지_않는_판매는_거부된다()
    {
        var (user, b) = NewUser();

        user.TrySellEntities(new[] { CharA, CharB }, Array.Empty<long>());

        Last(b).Result.ShouldBe(EResultCode.SellLastCharacter);
        user.Characters.Count.ShouldBe(2);
    }

    [Fact]
    public void 미보유_개체는_거부된다()
    {
        var (user, b) = NewUser();

        user.TrySellEntities(Array.Empty<long>(), new[] { 999L });
        Last(b).Result.ShouldBe(EResultCode.EquipNotOwned);

        user.TrySellEntities(new[] { 999L }, Array.Empty<long>());
        Last(b).Result.ShouldBe(EResultCode.CharacterNotOwned);
    }

    [Fact]
    public void 빈_요청과_중복_개체는_잘못된_요청이다()
    {
        // 중복을 받으면 대금이 두 번 붙는다.
        var (user, b) = NewUser();

        user.TrySellEntities(Array.Empty<long>(), Array.Empty<long>());
        Last(b).Result.ShouldBe(EResultCode.InvalidSellRequest);

        user.TrySellEntities(Array.Empty<long>(), new[] { Sword, Sword });
        Last(b).Result.ShouldBe(EResultCode.InvalidSellRequest);
        user.TryGetEquip(Sword, out _).ShouldBeTrue();
    }
}
