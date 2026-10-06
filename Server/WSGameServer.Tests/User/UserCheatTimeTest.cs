using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 시간 치트(<c>AdvanceTime</c>·<c>ResetTime</c> · T-128 · 이슈 #48) — 서버 전체의 게임 시계를 넘긴다.
/// 지킬 것은 셋이다 — <b>넘긴 시간은 채취에 쌓이지 않는다</b>, 접속 중인 모두가 새 서버 시각을 받는다, 경매장 릴레이를 깨운다.
/// </summary>
public class UserCheatTimeTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const long CharacterId = 500;
    private const long OneDay      = 86_400;

    public UserCheatTimeTest() => GameTableFixture.EnsureLoaded();

    private static C_CheatRequest Req(ECheatCommand command, long arg1 = 0)
        => new() { Command = command, Arg1 = arg1 };

    private static long UnixMs(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

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
        b.Online.Add(user);
        return (user, b);
    }

    [Fact]
    public void 시간을_넘기면_오프셋이_쌓이고_새_서버_시각이_나간다()
    {
        var (user, b) = Fishing();

        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, 3600), Base);
        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, OneDay), Base.AddHours(1));

        b.Clock.Offset.ShouldBe(TimeSpan.FromHours(25));
        b.Channel.SentOf<S_ServerTimeResponse>().Last().ServerNowUnixMs.ShouldBe(UnixMs(Base.AddHours(25)));
        b.Channel.SentOf<S_CheatResponse>().ShouldAllBe(r => r.Result == EResultCode.Ok);
    }

    [Fact]
    public void 넘긴_시간은_채취에_쌓이지_않는다()
    {
        // 5분 돌린 뒤 하루를 넘겼다. 쌓인 것은 5분치 10판정뿐이다 — 하루치(2,880판정)가 들어오면 보상이 튄다.
        var (user, b) = Fishing();
        var now = Base.AddMinutes(5);

        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, OneDay), now);
        user.SettleWorkStation(now.AddDays(1));

        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount.ShouldBe(10);
    }

    [Fact]
    public void 슬롯_스냅샷이_밀린_기준_시각으로_다시_나간다()
    {
        // 클라가 진행도를 서버 시각 기준으로 그린다 — 기준 시각이 안 따라오면 막대가 꽉 찬 채 멈춘다.
        var (user, b) = Fishing();

        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, OneDay), Base);

        var slot = b.Channel.SentOf<S_WorkStationSlotsResponse>().ShouldHaveSingleItem().Slots.ShouldHaveSingleItem();
        slot.LastTickAtUnixMs.ShouldBe(UnixMs(Base.AddDays(1)));
    }

    [Fact]
    public void 접속_중인_다른_유저도_새_서버_시각과_밀린_슬롯을_받는다()
    {
        // 오프셋은 서버 전체 하나다 — 경매가 공용이라 계정별 시계는 의미가 없다.
        var (user, b) = Fishing();
        var otherBuilder = new TestUserBuilder().WithFishingDrops();
        var (other, _) = Fishing(otherBuilder, uid: 2);
        b.Online.Add(other);

        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, OneDay), Base);

        otherBuilder.Channel.SentOf<S_ServerTimeResponse>().ShouldHaveSingleItem().ServerNowUnixMs.ShouldBe(UnixMs(Base.AddDays(1)));
        other.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.LastTickAt.ShouldBe(Base.AddDays(1));
    }

    [Fact]
    public void 시간을_넘기면_경매장_릴레이를_깨운다()
    {
        // 경매장 오프셋은 릴레이가 맞춘다. 1초 주기를 기다리면 그 사이 검색이 옛 시각으로 나간다.
        var kicks = 0;
        var b = new TestUserBuilder().WithFishingDrops();
        b.Auction = new AuctionService(null, null) { KickRelay = () => kicks++ };
        var (user, _) = Fishing(b);

        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, 60), Base);

        kicks.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(365 * OneDay + 1)]
    public void 넘길_초가_범위_밖이면_InvalidCheatArgs고_시계는_그대로다(long seconds)
    {
        var (user, b) = Fishing();

        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, seconds), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.Clock.Offset.ShouldBe(TimeSpan.Zero);
        b.Channel.SentOf<S_ServerTimeResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 초기화하면_오프셋이_0이고_되돌린_만큼_서버_시각이_돌아간다()
    {
        var (user, b) = Fishing();
        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, OneDay), Base);

        user.ExecuteCheat(Req(ECheatCommand.ResetTime), Base.AddDays(1).AddMinutes(1));

        b.Clock.Offset.ShouldBe(TimeSpan.Zero);
        b.Channel.SentOf<S_ServerTimeResponse>().Last().ServerNowUnixMs.ShouldBe(UnixMs(Base.AddMinutes(1)));
    }

    [Fact]
    public void 초기화해도_채취는_실제로_흐른_시간만큼만_쌓인다()
    {
        // 하루 넘겼다 되돌린 사이 실제로는 5분이 흘렀다 — 슬롯도 함께 되돌아가야 그 5분만 남는다.
        var (user, b) = Fishing();
        user.ExecuteCheat(Req(ECheatCommand.AdvanceTime, OneDay), Base);

        user.ExecuteCheat(Req(ECheatCommand.ResetTime), Base.AddDays(1).AddMinutes(5));
        user.SettleWorkStation(Base.AddMinutes(5));

        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount.ShouldBe(10);
    }

    [Fact]
    public void 오프셋이_0이면_초기화는_아무것도_보내지_않는다()
    {
        var (user, b) = Fishing();

        user.ExecuteCheat(Req(ECheatCommand.ResetTime), Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.Channel.SentOf<S_ServerTimeResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 로그인하면_서버_시각이_로그인_응답_바로_뒤에_나간다()
    {
        // 클라는 남은 시간·진행도를 그리기 전에 서버 시각을 알아야 한다.
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.OnLoginDataLoaded(new PlayerLoginData(
            new List<InventoryRow>(),
            null,
            new List<CharacterRow> { new() { character_id = 1, character_tid = User.DefaultCharacterTid, level = 1, exp = 0 } },
            new List<WorkStationSlotRow>(),
            new List<UserUnlockRow>(),
            new List<UserEquipRow>(),
            new List<CharacterEquipRow>()), Base);

        b.Channel.Sent[0].ShouldBeOfType<S_LoginResponse>();
        b.Channel.Sent[1].ShouldBeOfType<S_ServerTimeResponse>().ServerNowUnixMs.ShouldBe(UnixMs(Base));
    }
}
