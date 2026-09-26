using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 로그인 때 작업슬롯이 보유하지 않은 캐릭터를 가리키면 그 배치를 풀어 적재한다(이슈 #39).
/// 풀지 않으면 클라가 없는 캐릭터를 찾다 경고를 띄우고, 그 슬롯은 기본 속도로 조용히 돈다.
/// </summary>
public class WorkStationOrphanTest
{
    private const long Owned    = 1;
    private const long NotOwned = 99;

    private static (User User, TestUserBuilder B) LogIn(params WorkStationSlotRow[] slots)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.OnLoginDataLoaded(new PlayerLoginData(
            new List<InventoryRow>(),
            null,
            new List<CharacterRow> { new() { character_id = Owned, character_tid = User.DefaultCharacterTid, level = 1, exp = 0 } },
            slots.ToList(),
            new List<UserUnlockRow>(),
            new List<UserEquipRow>(),
            new List<CharacterEquipRow>()), TestUserBuilder.Base);
        return (user, b);
    }

    private static WorkStationSlotRow Slot(int index, long characterId)
        => new() { slot_index = index, industry = (int)IndustryType.Fishing, industry_level = 1, character_id = characterId };

    [Fact]
    public void 미보유_캐릭터를_가리키는_칸은_빈_칸으로_적재한다()
    {
        var (user, _) = LogIn(Slot(0, NotOwned));

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.CharacterId.ShouldBe(0);
    }

    [Fact]
    public void 풀린_칸은_산업을_그대로_둔다()
    {
        var (user, _) = LogIn(Slot(0, NotOwned));

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.Industry.ShouldBe(IndustryType.Fishing);
    }

    [Fact]
    public void 풀린_칸을_DB에도_빈_칸으로_저장한다()
    {
        // 저장하지 않으면 로그인마다 같은 경고가 남는다.
        var (_, b) = LogIn(Slot(0, NotOwned));

        var saved = b.DB.PostedOf<SaveWorkStationSlotRepository>().ShouldHaveSingleItem();
        saved.Slots.Select(s => (s.SlotIndex, s.CharacterId)).ShouldBe(new[] { (0, 0L) });
    }

    [Fact]
    public void 클라에는_빈_칸으로_내려간다()
    {
        var (_, b) = LogIn(Slot(0, NotOwned));

        var info = b.Channel.SentOf<S_WorkStationSlotsResponse>().Last().Slots!.Single(s => s.SlotIndex == 0);
        info.CharacterId.ShouldBe(0);
    }

    [Fact]
    public void 보유한_캐릭터의_배치는_그대로다()
    {
        var (user, b) = LogIn(Slot(0, Owned));

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.CharacterId.ShouldBe(Owned);
        b.DB.PostedOf<SaveWorkStationSlotRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 여러_칸이_풀려도_저장은_한_번이다()
    {
        var (_, b) = LogIn(Slot(0, NotOwned), Slot(1, NotOwned + 1));

        b.DB.PostedOf<SaveWorkStationSlotRepository>().ShouldHaveSingleItem().Slots.Count.ShouldBe(2);
    }
}
