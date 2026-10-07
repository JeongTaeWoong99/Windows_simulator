using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 슬롯 정보의 작업속도 내역(T-055) — 클라가 서버 식을 베끼지 않고 받은 항목으로 효율 계산 줄을 그린다.
/// 틀리면 클라 화면의 "적성 기본값 · 가산 · 전역 배수"가 실제 속도와 따로 논다.
/// </summary>
public class UserWorkSpeedBreakdownTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const long CharacterId = 500;

    public UserWorkSpeedBreakdownTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) Fishing(int level)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Growth.LoadAll();   // 레벨 가산 — 빌더가 기본으로 싣지 않는다
        var user = b.Build(1);
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = User.DefaultCharacterTid, level = level, exp = 0 },
        });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        return (user, b);
    }

    private static WorkStationSlotInfo Info(User user)
    {
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        return slot.ToInfo();
    }

    [Fact]
    public void 내역의_각_항목은_서버가_속도에_더한_값과_같다()
    {
        var (user, _) = Fishing(level: 10);
        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SetTraitLevel, Arg1 = 0, Arg2 = 99 }, Base);
        user.TryGetCharacter(CharacterId, out var character).ShouldBeTrue();

        var info = Info(user);

        info.BaseWorkSpeed.ShouldBe(character.GetBaseWorkSpeed(IndustryType.Fishing));
        info.LevelAddPermille.ShouldBe(user.GetLevelSpeedAdd(CharacterId));
        info.TraitAddPermille.ShouldBe(user.GetTraitSpeedAdd(IndustryType.Fishing));
        info.EquipAddPermille.ShouldBe(user.GetEquipSpeedAdd(CharacterId, IndustryType.Fishing));
        info.StarAddPermille.ShouldBe(user.GetStarSpeedAdd(CharacterId));
        info.GatherSpeedPermille.ShouldBe(GatherSpeed.DefaultPermille);

        // 전제 — 가산이 실제로 붙어 있어야 이 테스트가 무언가를 지킨다
        info.LevelAddPermille.ShouldBeGreaterThan(0);
        info.TraitAddPermille.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void 내역으로_다시_계산하면_현재_속도와_같다()
    {
        var (user, _) = Fishing(level: 10);
        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SetTraitLevel, Arg1 = 0, Arg2 = 99 }, Base);
        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.SetGatherSpeed, Arg1 = 1500 }, Base);

        var info = Info(user);
        var add  = info.LevelAddPermille + info.TraitAddPermille + info.EquipAddPermille + info.StarAddPermille;

        WorkSpeed.From(info.BaseWorkSpeed).Add(add).Multiply(info.GatherSpeedPermille).Resolve()
            .ShouldBe(info.CurrentWorkSpeed);
    }

    [Fact]
    public void 속도가_같아도_내역이_바뀌면_바뀐_것으로_본다()
    {
        // 합이 우연히 같아도 클라 화면의 줄은 달라진다 — 속도만 비교하면 옛 내역이 남는다.
        var (user, _) = Fishing(level: 1);
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();

        slot.ApplyWorkSpeed(new WorkSpeedBreakdown(1000, 100, 0, 0, 0, GatherSpeed.DefaultPermille)).ShouldBeTrue();
        slot.ApplyWorkSpeed(new WorkSpeedBreakdown(1000, 0, 100, 0, 0, GatherSpeed.DefaultPermille)).ShouldBeTrue();
        slot.ApplyWorkSpeed(new WorkSpeedBreakdown(1000, 0, 100, 0, 0, GatherSpeed.DefaultPermille)).ShouldBeFalse();
    }
}
