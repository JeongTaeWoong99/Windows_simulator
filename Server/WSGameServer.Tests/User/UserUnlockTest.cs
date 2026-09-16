using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>의 해금 경로 검증 — 판정 순서(해금 기획 2.1)·골드 차감·기록·작업슬롯 후속·로그인 복원.
/// 골드가 새는 지점이라 <b>거절 경로에서 잔액이 움직이면 안 되고</b>, 성공 경로에서 기록·칸 생성이 빠지면 돈만 사라진다.
/// </summary>
public class UserUnlockTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    // 테스트 전용 해금 표 — 엑셀 값에 기대지 않는다.
    //   1002: 500골드            → 2번 칸
    //   1003: 1500골드, 선행 1002 → 3번 칸
    //   1004: 무료,     선행 1003 → 4번 칸
    private const int SecondSlot = 1002;
    private const int ThirdSlot  = 1003;
    private const int FreeSlot   = 1004;

    public UserUnlockTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWith(long gold)
    {
        var b = new TestUserBuilder();
        b.Unlocks.Load(
            new[]
            {
                new UnlockTableRow { UnlockTID = SecondSlot, Name = "2번", Gold = 500 },
                new UnlockTableRow { UnlockTID = ThirdSlot,  Name = "3번", Gold = 1500, RequiredUnlockTIDs = new[] { SecondSlot } },
                new UnlockTableRow { UnlockTID = FreeSlot,   Name = "4번", Gold = 0,    RequiredUnlockTIDs = new[] { ThirdSlot } },
            },
            new[]
            {
                new WorkSlotTableRow { WorkSlotTID = 0, UnlockTID = 0 },
                new WorkSlotTableRow { WorkSlotTID = 1, UnlockTID = 0 },
                new WorkSlotTableRow { WorkSlotTID = 2, UnlockTID = SecondSlot },
                new WorkSlotTableRow { WorkSlotTID = 3, UnlockTID = ThirdSlot },
                new WorkSlotTableRow { WorkSlotTID = 4, UnlockTID = FreeSlot },
            });

        var user = b.Build();
        if (gold > 0)
        {
            user.GainGold(gold);
        }

        // 지급 통지는 이 테스트의 관심사가 아니다 — 이후 단언이 요청의 결과만 보게 비운다.
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static PlayerLoginData LoginData(
        IEnumerable<int>? unlocked = null,
        IEnumerable<WorkStationSlotRow>? slotRows = null) => new(
        new List<InventoryRow>(),
        null,
        new List<CharacterRow>
        {
            new() { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        },
        (slotRows ?? Array.Empty<WorkStationSlotRow>()).ToList(),
        new List<UserIndustryLevelRow>(),
        (unlocked ?? Array.Empty<int>()).Select(t => new UserUnlockRow { unlock_tid = t }).ToList(),
        new List<UserEquipRow>(),
        new List<CharacterEquipRow>());

    // ─────────────────────── 거절 경로 ───────────────────────

    [Fact]
    public void 없는_TID는_InvalidUnlockTID다()
    {
        var (user, b) = UserWith(gold: 10_000);

        user.TryUnlock(9999, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidUnlockTID);
        user.Gold.ShouldBe(10_000);
    }

    [Fact]
    public void 선행이_안_열렸으면_UnlockLocked고_골드는_그대로다()
    {
        // 골드는 충분하다 — 선행 검사가 차감보다 먼저라는 것이 이 테스트의 전부다.
        var (user, b) = UserWith(gold: 10_000);

        user.TryUnlock(ThirdSlot, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.UnlockLocked);
        user.Gold.ShouldBe(10_000);
        user.IsUnlocked(ThirdSlot).ShouldBeFalse();
    }

    [Fact]
    public void 골드가_모자라면_NotEnoughCurrency고_잔액과_기록은_그대로다()
    {
        var (user, b) = UserWith(gold: 400);

        user.TryUnlock(SecondSlot, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NotEnoughCurrency);
        user.Gold.ShouldBe(400);
        user.IsUnlocked(SecondSlot).ShouldBeFalse();
        b.DB.PostedOf<SaveUnlockRepository>().ShouldBeEmpty();
        user.WorkStation.TryGet(2, out _).ShouldBeFalse();
    }

    [Fact]
    public void 이_해금에_없는_재화를_고르면_UnlockLocked다()
    {
        // 1002는 골드 해금이다. 다이아를 골라도 뭘 빼야 할지 없으므로 거절한다 — 다이아 잔액도 건드리지 않는다.
        var (user, b) = UserWith(gold: 10_000);
        user.GainDia(1_000);

        user.TryUnlock(SecondSlot, CurrencyType.Dia, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.UnlockLocked);
        user.Dia.ShouldBe(1_000);
        user.Gold.ShouldBe(10_000);
    }

    [Fact]
    public void 이미_열린_해금은_AlreadyUnlocked고_다시_빼지_않는다()
    {
        var (user, b) = UserWith(gold: 1_000);
        user.TryUnlock(SecondSlot, CurrencyType.Gold, Base);   // 500 차감
        b.Channel.Sent.Clear();

        user.TryUnlock(SecondSlot, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.AlreadyUnlocked);
        user.Gold.ShouldBe(500);
    }

    // ─────────────────────── 성공 경로 ───────────────────────

    [Fact]
    public void 해금하면_골드를_빼고_기록을_저장하고_칸이_생긴다()
    {
        var (user, b) = UserWith(gold: 500);

        user.TryUnlock(SecondSlot, CurrencyType.Gold, Base);

        var res = b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem();
        res.Result.ShouldBe(EResultCode.Ok);
        res.UnlockTID.ShouldBe(SecondSlot);

        user.Gold.ShouldBe(0);
        user.IsUnlocked(SecondSlot).ShouldBeTrue();
        b.DB.PostedOf<SaveUnlockRepository>().ShouldHaveSingleItem().UnlockTid.ShouldBe(SecondSlot);
        user.WorkStation.TryGet(2, out _).ShouldBeTrue();
    }

    [Fact]
    public void 해금_응답_뒤에_새_칸의_슬롯_동기화가_따라온다()
    {
        // 해금 패킷은 "열렸다"까지만 안다. 칸이 생겼다는 것은 슬롯의 기존 패킷으로 알린다(해금 2.2).
        var (user, b) = UserWith(gold: 500);

        user.TryUnlock(SecondSlot, CurrencyType.Gold, Base);

        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem().Slot!.SlotIndex.ShouldBe(2);

        var kinds = b.Channel.Sent.Select(p => p.GetType()).ToList();
        kinds.IndexOf(typeof(S_UnlockResponse))
            .ShouldBeLessThan(kinds.IndexOf(typeof(S_WorkStationSlotSyncResponse)));
    }

    [Fact]
    public void 지불_컬럼이_없는_해금은_재화_선택을_무시하고_연다()
    {
        var (user, b) = UserWith(gold: 2_000);
        user.TryUnlock(SecondSlot, CurrencyType.Gold, Base);   // 500
        user.TryUnlock(ThirdSlot,  CurrencyType.Gold, Base);   // 1500 → 잔액 0
        b.Channel.Sent.Clear();

        user.TryUnlock(FreeSlot, CurrencyType.None, Base);

        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.Gold.ShouldBe(0);
        user.WorkStation.TryGet(4, out _).ShouldBeTrue();
    }

    [Fact]
    public void 항상_열림_0은_기록_없이도_열린_것이다()
    {
        var (user, _) = UserWith(gold: 0);

        user.IsUnlocked(0).ShouldBeTrue();
    }

    // ─────────────────────── GrantUnlock ───────────────────────

    [Fact]
    public void GrantUnlock은_조건과_차감_없이_열고_같은_통지를_보낸다()
    {
        // 골드 0, 선행(1002) 미충족 — TryUnlock이면 두 번 거절될 조건이다.
        var (user, b) = UserWith(gold: 0);

        user.GrantUnlock(ThirdSlot, Base).ShouldBeTrue();

        user.IsUnlocked(ThirdSlot).ShouldBeTrue();
        b.DB.PostedOf<SaveUnlockRepository>().ShouldHaveSingleItem().UnlockTid.ShouldBe(ThirdSlot);
        b.Channel.SentOf<S_UnlockResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem().Slot!.SlotIndex.ShouldBe(3);
    }

    [Fact]
    public void GrantUnlock은_이미_열렸으면_아무것도_하지_않는다()
    {
        var (user, b) = UserWith(gold: 0);
        user.GrantUnlock(SecondSlot, Base);
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();

        user.GrantUnlock(SecondSlot, Base).ShouldBeFalse();

        b.Channel.Sent.ShouldBeEmpty();
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void GrantUnlock은_없는_TID면_열지_않는다()
    {
        var (user, b) = UserWith(gold: 0);

        user.GrantUnlock(9999, Base).ShouldBeFalse();

        b.DB.Posted.ShouldBeEmpty();
    }

    // ─────────────────────── 로그인 ───────────────────────

    [Fact]
    public void 신규_계정은_시작_2칸으로_시작한다()
    {
        var (user, _) = UserWith(gold: 0);

        user.OnLoginDataLoaded(LoginData(), Base);

        user.WorkStation.Count.ShouldBe(2);
        user.WorkStation.TryGet(0, out _).ShouldBeTrue();
        user.WorkStation.TryGet(1, out _).ShouldBeTrue();
    }

    [Fact]
    public void 재로그인하면_열린_해금의_칸이_되살아난다()
    {
        var (user, _) = UserWith(gold: 0);

        user.OnLoginDataLoaded(LoginData(unlocked: new[] { SecondSlot }), Base);

        user.IsUnlocked(SecondSlot).ShouldBeTrue();
        user.WorkStation.Count.ShouldBe(3);
        user.WorkStation.TryGet(2, out _).ShouldBeTrue();
    }

    [Fact]
    public void 잠긴_칸의_배치_행은_무시한다()
    {
        // "열렸다"의 원본은 t_user_unlock 하나다(해금 #18). 배치 행만 남은 잠긴 칸은 만들지 않는다.
        var (user, _) = UserWith(gold: 0);

        user.OnLoginDataLoaded(LoginData(slotRows: new[]
        {
            new WorkStationSlotRow { slot_index = 3, industry = (int)IndustryType.Fishing, industry_level = 1, character_id = CharacterId },
        }), Base);

        user.WorkStation.Count.ShouldBe(2);
        user.WorkStation.TryGet(3, out _).ShouldBeFalse();
    }

    [Fact]
    public void 열린_칸의_배치_행은_되살아난다()
    {
        var (user, _) = UserWith(gold: 0);

        user.OnLoginDataLoaded(LoginData(
            unlocked: new[] { SecondSlot },
            slotRows: new[]
            {
                new WorkStationSlotRow { slot_index = 2, industry = (int)IndustryType.Fishing, industry_level = 1, character_id = CharacterId },
            }), Base);

        user.WorkStation.TryGet(2, out var slot).ShouldBeTrue();
        slot.Industry.ShouldBe(IndustryType.Fishing);
        slot.CharacterId.ShouldBe(CharacterId);
    }

    [Fact]
    public void 로그인_때_열린_목록이_슬롯_스냅샷보다_먼저_나간다()
    {
        var (user, b) = UserWith(gold: 0);

        user.OnLoginDataLoaded(LoginData(unlocked: new[] { SecondSlot }), Base);

        b.Channel.SentOf<S_UnlockListResponse>().ShouldHaveSingleItem().UnlockTIDs.ShouldBe(new[] { SecondSlot });

        var kinds = b.Channel.Sent.Select(p => p.GetType()).ToList();
        kinds.IndexOf(typeof(S_UnlockListResponse))
            .ShouldBeLessThan(kinds.IndexOf(typeof(S_WorkStationSlotsResponse)));
    }

    [Fact]
    public void 잠긴_칸에_배치하면_InvalidSlotIndex다()
    {
        var (user, b) = UserWith(gold: 0);
        user.OnLoginDataLoaded(LoginData(), Base);
        b.Channel.Sent.Clear();

        user.AssignWorkStation(2, IndustryType.Fishing, CharacterId, Base);

        b.Channel.SentOf<S_WorkStationAssignResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidSlotIndex);
    }
}
