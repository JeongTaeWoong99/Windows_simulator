using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>의 작업슬롯 경로 검증 — <b>부품이 아니라 조립부</b>를 본다.
///
/// <para>
/// <see cref="WorkStationSlotTest"/>가 슬롯 하나의 계산을 잠갔다면, 여기는 그 위층이다:
/// 정산 → 지급 → 저장 → 푸시가 <b>어떤 순서로 어떻게 엮이는가</b>. 재화가 실제로 생기는 경로라
/// 여기서 순서 하나가 뒤집히면 소급 지급이 되고, 거절 경로 하나가 새면 상태가 오염된다.
/// </para>
/// </summary>
public class UserWorkStationTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    /// <summary>낚시 적성 1. 시작 캐릭터라 전 산업을 다룰 수 있다.</summary>
    private const int AllRounderTid = 1001;

    /// <summary>낚시 적성 0. 배치가 막혀야 하는 쪽이다.</summary>
    private const int NoFishingTid = 1002;

    private const long CharacterId = 500;

    public UserWorkStationTest() => GameTableFixture.EnsureLoaded();

    /// <summary>캐릭터 1장을 보유한 유저를 만든다.</summary>
    private static (User User, TestUserBuilder B) UserWith(int characterTid)
    {
        var b    = new TestUserBuilder().WithFishingDrops();
        var user = b.Build();

        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = characterTid, level = 1, exp = 0 },
        });

        return (user, b);
    }

    /// <summary>슬롯 하나를 원하는 속도로 직접 얹는다. 전역 배수·적성표를 타지 않아 기대값이 흔들리지 않는다.</summary>
    private static void GiveSlot(
        User user,
        DateTime startedAt,
        long characterId = CharacterId,
        int workSpeed = WorkStationSlot.DefaultWorkSpeed,
        IndustryType industry = IndustryType.Fishing)
    {
        user.WorkStation.Load(new[]
        {
            new WorkStationSlot(0, industry, characterId, startedAt, workSpeed),
        });
    }

    // ─────────────────────── 데이터 전제 ───────────────────────

    [Fact]
    public void 테스트가_기대는_캐릭터_적성이_실제_테이블과_같다()
    {
        // 아래 테스트들이 "1001은 낚시 가능 / 1002는 불가"를 전제로 한다.
        // 엑셀이 바뀌면 원인 불명의 실패 대신 여기가 먼저 빨개진다.
        GameTable.CharacterTable.TryGet(AllRounderTid, out var allRounder).ShouldBeTrue();
        GameTable.CharacterTable.TryGet(NoFishingTid,  out var noFishing).ShouldBeTrue();

        allRounder.Fishing.ShouldBeGreaterThan(0);
        noFishing.Fishing.ShouldBe(0);
    }

    // ─────────────────────── 배치 거절 경로 ───────────────────────

    [Fact]
    public void 없는_슬롯에_배치하면_InvalidSlotIndex로_거절한다()
    {
        var (user, b) = UserWith(AllRounderTid);

        user.AssignWorkStation(slotIndex: 7, IndustryType.Fishing, CharacterId, Base);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidSlotIndex);
    }

    [Fact]
    public void 미보유_캐릭터_배치는_CharacterNotOwned로_거절한다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.AssignWorkStation(0, IndustryType.Fishing, characterId: 999, Base);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.CharacterNotOwned);
    }

    [Fact]
    public void 적성_0인_산업_배치는_NoAptitude로_거절한다()
    {
        // 미보유와 적성 0은 클라 조치가 다르다(id 오류 vs 기획상 불가). 코드로 구분돼야 한다.
        var (user, b) = UserWith(NoFishingTid);
        GiveSlot(user, Base);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NoAptitude);
    }

    [Fact]
    public void 배치하면_판정_비용이_산업_레벨_테이블_값이_된다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        // 기본 상수(30초)와 다른 값을 넣는다 — 테이블을 안 읽고 상수를 쓰면 여기서 드러난다.
        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow
            {
                IndustryLevelTID = 201, IndustryType = IndustryType.Fishing, Level = 1,
                Name = "개울", RequiredScore = 60_000,
            },
        });

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base);

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.JudgeCostUnits.ShouldBe(60_000_000L);   // 60,000점 × 1000(초→ms 환산)
    }

    [Fact]
    public void 거절된_배치는_저장하지_않는다()
    {
        // 거절인데 저장이 나가면 다음 로그인에 잘못된 배치가 되살아난다.
        var (user, b) = UserWith(NoFishingTid);
        GiveSlot(user, Base);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base);

        b.DB.PostedOf<SaveWorkStationSlotRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 거절된_배치는_슬롯_상태를_건드리지_않는다()
    {
        var (user, b) = UserWith(NoFishingTid);
        GiveSlot(user, Base, industry: IndustryType.Mining);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base);

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.Industry.ShouldBe(IndustryType.Mining);
    }

    // ─────────────────────── 산업 레벨 ───────────────────────
    //
    // 채취는 재화가 생성되는 지점이라 레벨은 클라가 말한 값을 그대로 믿으면 안 된다(게임기획코어 P4).
    // 서버가 보관한 해금 레벨과 대조하는 것이 이 묶음의 전부다.

    [Fact]
    public void 해금하지_않은_레벨_배치는_IndustryLevelLocked로_거절한다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        // 해금 기록이 없으면 기본 레벨(Lv1)까지만 열려 있다.
        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base, industryLevel: 3);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.IndustryLevelLocked);
    }

    [Fact]
    public void 해금하지_않은_레벨_배치는_슬롯을_건드리지_않는다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base, industry: IndustryType.Mining);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base, industryLevel: 3);

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.Industry.ShouldBe(IndustryType.Mining);
        slot.IndustryLevel.ShouldBe(WorkStationSlot.DefaultIndustryLevel);
        b.DB.PostedOf<SaveWorkStationSlotRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 해금한_레벨은_배치되고_슬롯에_남는다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);
        user.LoadIndustryLevels(new[]
        {
            new UserIndustryLevelRow { industry = (int)IndustryType.Fishing, unlocked_level = 3 },
        });

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base, industryLevel: 3);

        var res = b.Channel.SentOf<S_WorkStationAssignResponse>().ShouldHaveSingleItem();
        res.Result.ShouldBe(EResultCode.Ok);
        res.Slot!.IndustryLevel.ShouldBe((byte)3);

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.IndustryLevel.ShouldBe(3);
    }

    [Fact]
    public void 해금한_레벨보다_낮은_레벨도_고를_수_있다()
    {
        // 하향 선택 — 상위를 열어도 하위가 죽지 않는다(산업레벨.md 1장 #7).
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);
        user.LoadIndustryLevels(new[]
        {
            new UserIndustryLevelRow { industry = (int)IndustryType.Fishing, unlocked_level = 5 },
        });

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base, industryLevel: 2);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 해금은_산업마다_따로다()
    {
        // 낚시를 5까지 열어도 채굴은 여전히 Lv1이다 (산업레벨.md 1장 #1).
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);
        user.LoadIndustryLevels(new[]
        {
            new UserIndustryLevelRow { industry = (int)IndustryType.Fishing, unlocked_level = 5 },
        });

        user.AssignWorkStation(0, IndustryType.Mining, CharacterId, Base, industryLevel: 5);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.IndustryLevelLocked);
    }

    [Fact]
    public void 레벨이_1_미만이면_거절한다()
    {
        // 0 이하는 해금 비교(level <= 열린 레벨)를 그냥 통과해 버린다. 하한을 따로 막아야 한다.
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base, industryLevel: 0);

        b.Channel.SentOf<S_WorkStationAssignResponse>()
            .ShouldHaveSingleItem().Result.ShouldBe(EResultCode.IndustryLevelLocked);
    }

    [Fact]
    public void 레벨을_바꾸기_전_구간은_이전_레벨의_비용으로_정산된다()
    {
        // 산업레벨.md 2.3 — 정산을 빼먹으면 싸게 쌓은 점수가 비싼 판정에 쓰인다.
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);
        user.LoadIndustryLevels(new[]
        {
            new UserIndustryLevelRow { industry = (int)IndustryType.Fishing, unlocked_level = 2 },
        });
        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow
            {
                IndustryLevelTID = 201, IndustryType = IndustryType.Fishing, Level = 1,
                Name = "개울", RequiredScore = 30_000,      // 30초
            },
            new IndustryLevelTableRow
            {
                IndustryLevelTID = 202, IndustryType = IndustryType.Fishing, Level = 2,
                Name = "저수지", RequiredScore = 90_000,     // 90초
            },
        });

        // 60초 경과 후 Lv2로 변경. Lv1 비용(30초)으로 정산하면 2회,
        // 새 Lv2 비용(90초)으로 계산해 버리면 0회가 된다.
        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base.AddSeconds(60), industryLevel: 2);

        b.Channel.SentOf<S_GatherResultResponse>()
            .ShouldHaveSingleItem().JudgeCount.ShouldBe(2);

        // 바뀐 뒤에는 새 레벨의 비용으로 돈다.
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.JudgeCostUnits.ShouldBe(90_000_000L);
    }

    [Fact]
    public void 저장된_슬롯_레벨과_해금_레벨은_로그인_때_되살아난다()
    {
        var user = new TestUserBuilder().WithFishingDrops().Build();

        // 실제 로그인 경로로 넣는다 — DB에서 온 값이 도메인까지 도달하는지가 이 테스트의 전부다.
        user.OnLoginDataLoaded(new PlayerLoginData(
            new List<InventoryRow>(),
            new List<CurrencyRow>(),
            new List<CharacterRow>
            {
                new() { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
            },
            new List<WorkStationSlotRow>
            {
                new() { slot_index = 0, industry = (int)IndustryType.Fishing, character_id = CharacterId, industry_level = 4 },
            },
            new List<UserIndustryLevelRow>
            {
                new() { industry = (int)IndustryType.Fishing, unlocked_level = 4 },
            }), Base);

        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.IndustryLevel.ShouldBe(4);
        user.GetUnlockedIndustryLevel(IndustryType.Fishing).ShouldBe(4);

        // 기록이 없는 산업은 기본 레벨까지만 열려 있다.
        user.GetUnlockedIndustryLevel(IndustryType.Mining).ShouldBe(WorkStationSlot.DefaultIndustryLevel);
    }

    // ─────────────────────── 배치 성공 경로 ───────────────────────

    [Fact]
    public void 배치에_성공하면_Ok와_슬롯_스냅샷을_함께_보낸다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base, characterId: 0, industry: IndustryType.None);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base);

        var res = b.Channel.SentOf<S_WorkStationAssignResponse>().ShouldHaveSingleItem();
        res.Result.ShouldBe(EResultCode.Ok);
        res.Slot.ShouldNotBeNull();
        res.Slot!.Industry.ShouldBe(EIndustryType.Fishing);
        res.Slot!.CharacterId.ShouldBe(CharacterId);
    }

    [Fact]
    public void 배치에_성공하면_그_슬롯을_저장한다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base, characterId: 0, industry: IndustryType.None);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base);

        b.DB.PostedOf<SaveWorkStationSlotRepository>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// <b>이 테스트가 이번 리팩터링의 목적이다.</b>
    ///
    /// <para>
    /// 기획 규칙 <i>"바꾸기 전에 반드시 정산한다"</i>를 처음으로 코드로 잠근다.
    /// <see cref="User.AssignWorkStation"/>의 세 줄(정산 → <c>Assign</c> → <c>ApplyWorkSpeed</c>)은
    /// 순서를 어기는 방향마다 결과가 다르게 망가진다.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>정산이 <c>Assign</c> 뒤로 가면</b> — <c>Assign</c>이 진행도를 버리므로
    ///       완성된 판정이 <b>통째로 사라진다</b>(판정 0회).</item>
    /// <item><b><c>ApplyWorkSpeed</c>가 정산 앞으로 가면</b> — 아직 정산되지 않은 구간까지
    ///       새 속도로 계산돼 <b>재화가 샌다</b>(판정 20회 초과).</item>
    /// </list>
    ///
    /// <para>둘 다 이 한 줄의 기대값에서 걸린다.</para>
    /// </summary>
    [Fact]
    public void 배치를_바꾸기_전에_먼저_정산한다()
    {
        var (user, b) = UserWith(AllRounderTid);

        // 기본 속도(1.0배) = 30초에 1판정. 10분이면 정확히 20판정이다.
        GiveSlot(user, Base, workSpeed: WorkStationSlot.DefaultWorkSpeed);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base.AddMinutes(10));

        b.Channel.SentOf<S_GatherResultResponse>()
            .ShouldHaveSingleItem().JudgeCount.ShouldBe(20);
    }

    [Fact]
    public void 정산_결과가_배치_응답보다_먼저_나간다()
    {
        // 클라이언트는 정산분을 먼저 받아야 인벤토리와 슬롯 상태가 어긋나지 않는다.
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.AssignWorkStation(0, IndustryType.Fishing, CharacterId, Base.AddMinutes(10));

        var kinds = b.Channel.Sent.Select(p => p.GetType()).ToList();
        kinds.IndexOf(typeof(S_GatherResultResponse))
            .ShouldBeLessThan(kinds.IndexOf(typeof(S_WorkStationAssignResponse)));
    }

    // ─────────────────────── 정산 ───────────────────────

    [Fact]
    public void 판정한_만큼_아이템을_지급하고_결과를_밀어_준다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.SettleWorkStation(Base.AddMinutes(5)).ShouldBe(1);   // 정산된 슬롯 수

        var result = b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem();
        result.SlotIndex.ShouldBe(0);
        result.JudgeCount.ShouldBe(10);                           // 5분 / 30초

        // 후보가 1종이고 회당 산출 1개 고정이라 판정 수와 획득 수가 같다.
        var change = result.ItemChanges!.ShouldHaveSingleItem();
        change.ItemId.ShouldBe(TestUserBuilder.FishItemTid);
    }

    [Fact]
    public void 지급된_아이템은_한_번만_저장한다()
    {
        // 판정 10회라고 UPSERT를 10번 하면 안 된다 — 아이템별로 한 번이다.
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.SettleWorkStation(Base.AddMinutes(5));

        b.DB.PostedOf<AddItemRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 판정이_안_찼으면_아무것도_하지_않는다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.SettleWorkStation(Base.AddSeconds(29)).ShouldBe(0);

        b.Channel.Sent.ShouldBeEmpty();
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void notify가_false면_지급과_저장은_하되_푸시하지_않는다()
    {
        // 접속 종료 정산 경로다. 세션이 닫혀 보낼 곳이 없을 뿐,
        // 접속 중 완성된 판정은 정당하게 번 것이므로 버리지 않는다.
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.SettleWorkStation(Base.AddMinutes(5), notify: false);

        b.Channel.Sent.ShouldBeEmpty();
        b.DB.PostedOf<AddItemRepository>().ShouldHaveSingleItem();
    }

    [Fact]
    public void 슬롯마다_따로_결과를_보낸다()
    {
        var (user, b) = UserWith(AllRounderTid);
        user.WorkStation.Load(new[]
        {
            new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base),
            new WorkStationSlot(1, IndustryType.Fishing, CharacterId, Base.AddMinutes(4)),
        });

        user.SettleWorkStation(Base.AddMinutes(5)).ShouldBe(2);

        var results = b.Channel.SentOf<S_GatherResultResponse>();
        results.Count.ShouldBe(2);
        results.Single(r => r.SlotIndex == 0).JudgeCount.ShouldBe(10);   // 5분
        results.Single(r => r.SlotIndex == 1).JudgeCount.ShouldBe(2);    // 1분
    }

    // ─────────────────────── 접속 종료 ───────────────────────

    [Fact]
    public void 접속이_끊겨도_완성된_판정은_지급된다()
    {
        var (user, b) = UserWith(AllRounderTid);
        GiveSlot(user, Base);

        user.Disconnect(Base.AddMinutes(5));

        b.DB.PostedOf<AddItemRepository>().ShouldHaveSingleItem();
        b.Channel.Sent.ShouldBeEmpty();   // 세션이 닫혔으므로 푸시는 없다
    }

    // ─────────────────────── 주기 정산 루프 ───────────────────────

    [Fact]
    public void 스케줄러는_모든_유저를_같은_시각으로_정산한다()
    {
        var first  = UserWith(AllRounderTid);
        var second = UserWith(AllRounderTid);
        GiveSlot(first.User,  Base);
        GiveSlot(second.User, Base);

        GatheringScheduler.Tick(Base.AddMinutes(5), new[] { first.User, second.User });

        first.B.Channel.SentOf<S_GatherResultResponse>()
            .ShouldHaveSingleItem().JudgeCount.ShouldBe(10);
        second.B.Channel.SentOf<S_GatherResultResponse>()
            .ShouldHaveSingleItem().JudgeCount.ShouldBe(10);
    }
}
