using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 전역 배수 치트(<c>SetGatherSpeed</c> · T-055) — 서버 전체의 채취 속도를 배수로 당긴다.
/// 지킬 것은 넷이다 — 올리기 전 구간은 예전 속도로 정산된다(소급 없음), 접속 중인 모두가 빨라진다,
/// 슬롯 정보에 배수가 실린다, <b>DB에 저장하지 않는다</b>(재시작하면 ×1.0).
/// </summary>
public class UserCheatGatherSpeedTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const long CharacterId = 500;

    public UserCheatGatherSpeedTest() => GameTableFixture.EnsureLoaded();

    private static C_CheatRequest Req(long permille)
        => new() { Command = ECheatCommand.SetGatherSpeed, Arg1 = permille };

    /// <summary>낚시 슬롯 하나가 <see cref="Base"/>부터 돌고 있는 유저. 기본 속도로 30초에 1판정이다.</summary>
    private static (User User, TestUserBuilder B) Fishing(TestUserBuilder? b = null, long uid = 1)
    {
        b ??= new TestUserBuilder().WithFishingDrops();
        var user = b.Build(uid);
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = User.DefaultCharacterTid, level = 1, exp = 0 },
        });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });
        user.RefreshWorkStationSpeed(Base, notify: false);
        b.Online.Add(user);
        b.Channel.Sent.Clear();
        return (user, b);
    }

    private static WorkStationSlot Slot(User user)
    {
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        return slot;
    }

    [Fact]
    public void 배수를_올리면_슬롯_속도가_그만큼_빨라지고_슬롯_푸시에_배수가_실린다()
    {
        var (user, b) = Fishing();
        var before = Slot(user).CurrentWorkSpeed;

        user.ExecuteCheat(Req(2000), Base);

        Slot(user).CurrentWorkSpeed.ShouldBe(before * 2);
        var info = b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem().Slot;
        info.GatherSpeedPermille.ShouldBe(2000);
        info.CurrentWorkSpeed.ShouldBe(before * 2);
        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 올리기_전_구간은_예전_속도로_정산된다()
    {
        // 5분은 ×1(10판정), 그 뒤 5분은 ×2(20판정). 배수가 소급되면 40판정이 된다.
        var (user, b) = Fishing();

        user.ExecuteCheat(Req(2000), Base.AddMinutes(5));
        user.SettleWorkStation(Base.AddMinutes(10));

        b.Channel.SentOf<S_GatherResultResponse>().Sum(r => r.JudgeCount).ShouldBe(30);
    }

    [Fact]
    public void 접속_중인_다른_유저도_빨라진다()
    {
        // 배수는 서버 전체 하나다.
        var (user, b) = Fishing();
        var otherBuilder = new TestUserBuilder().WithFishingDrops();
        otherBuilder.GatherSpeed = b.GatherSpeed;
        var (other, _) = Fishing(otherBuilder, uid: 2);
        b.Online.Add(other);
        var before = Slot(other).CurrentWorkSpeed;

        user.ExecuteCheat(Req(3000), Base);

        Slot(other).CurrentWorkSpeed.ShouldBe(before * 3);
        otherBuilder.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem().Slot.GatherSpeedPermille.ShouldBe(3000);
    }

    [Fact]
    public void 배수를_1000으로_되돌리면_원래_속도다()
    {
        var (user, _) = Fishing();
        var before = Slot(user).CurrentWorkSpeed;

        user.ExecuteCheat(Req(5000), Base);
        user.ExecuteCheat(Req(1000), Base);

        Slot(user).CurrentWorkSpeed.ShouldBe(before);
    }

    [Fact]
    public void 배수는_DB에_저장하지_않는다()
    {
        var (user, b) = Fishing();
        b.DB.Posted.Clear();

        user.ExecuteCheat(Req(2000), Base);

        b.DB.Posted.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    [InlineData(User.CheatMinGatherSpeedPermille - 1)]
    [InlineData(User.CheatMaxGatherSpeedPermille + 1)]
    public void 배수가_범위_밖이면_InvalidCheatArgs고_속도는_그대로다(long permille)
    {
        var (user, b) = Fishing();
        var before = Slot(user).CurrentWorkSpeed;

        user.ExecuteCheat(Req(permille), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.GatherSpeed.Permille.ShouldBe(GatherSpeed.DefaultPermille);
        Slot(user).CurrentWorkSpeed.ShouldBe(before);
    }
}
