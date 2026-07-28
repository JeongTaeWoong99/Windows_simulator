using GameData;
using WSGameServer.Common;
using WSGameServer.User.WorkStation;

namespace WSGameServer.Tests.WorkStation;

/// <summary>
/// 작업슬롯 정산 검증.
///
/// 방치형의 진행은 <b>시각의 함수</b>라 여기서 실수가 나면 재화가 새거나 사라진다.
/// 특히 <b>자투리 시간 이월</b>과 <b>비활성 슬롯의 시간 누적</b>을 집중적으로 본다.
/// </summary>
public class WorkStationSlotTest
{
    private static readonly DateTime Base = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

    private static WorkStationSlot ActiveSlot(DateTime lastTickAt)
        => new(slotIndex: 0, ItemType.Fishing, characterId: 100, lastTickAt);

    [Fact]
    public void 주기가_안_찼으면_판정하지_않는다()
    {
        var slot = ActiveSlot(Base);

        slot.ConsumeJudgeCount(Base.AddSeconds(29)).ShouldBe(0);
        slot.LastTickAt.ShouldBe(Base);   // 전진하지 않는다
    }

    [Theory]
    [InlineData(30, 1)]
    [InlineData(59, 1)]
    [InlineData(60, 2)]
    [InlineData(90, 3)]
    [InlineData(3600, 120)]        // 1시간
    [InlineData(86400, 2880)]      // 24시간 = 하루 판정 수
    public void 경과_시간을_주기로_나눈_만큼_판정한다(int elapsedSeconds, int expected)
    {
        ActiveSlot(Base).ConsumeJudgeCount(Base.AddSeconds(elapsedSeconds)).ShouldBe(expected);
    }

    [Fact]
    public void 자투리_시간은_다음으로_이월된다()
    {
        var slot = ActiveSlot(Base);

        // 89초 = 2회 판정 + 29초 남음
        slot.ConsumeJudgeCount(Base.AddSeconds(89)).ShouldBe(2);
        slot.LastTickAt.ShouldBe(Base.AddSeconds(60));   // 90초가 아니라 60초만 전진

        // 남은 29초 + 1초 = 30초 → 곧바로 1회 더
        slot.ConsumeJudgeCount(Base.AddSeconds(90)).ShouldBe(1);
    }

    [Fact]
    public void 여러_번_나눠_정산해도_총_판정_수는_같다()
    {
        // 30초마다 푸시하든 5분 만에 한 번 정산하든 결과가 같아야 한다.
        var stepwise = ActiveSlot(Base);
        var total = 0;
        for (var i = 1; i <= 10; i++)
            total += stepwise.ConsumeJudgeCount(Base.AddSeconds(i * 30));

        var atOnce = ActiveSlot(Base).ConsumeJudgeCount(Base.AddSeconds(300));

        total.ShouldBe(atOnce);
        total.ShouldBe(10);
    }

    [Fact]
    public void 캐릭터가_없으면_시간이_쌓이지_않는다()
    {
        var empty = new WorkStationSlot(0, ItemType.Fishing, characterId: 0, Base);

        empty.IsActive.ShouldBeFalse();

        // 하루가 지나도 판정은 0이고, LastTickAt은 현재를 따라간다.
        // 따라가지 않으면 캐릭터를 꽂는 순간 밀린 하루치가 한꺼번에 터진다.
        var now = Base.AddDays(1);
        empty.ConsumeJudgeCount(now).ShouldBe(0);
        empty.LastTickAt.ShouldBe(now);
    }

    [Fact]
    public void 산업이_미지정이면_돌지_않는다()
    {
        var slot = new WorkStationSlot(0, ItemType.None, characterId: 100, Base);

        slot.IsActive.ShouldBeFalse();
        slot.ConsumeJudgeCount(Base.AddHours(1)).ShouldBe(0);
    }

    [Fact]
    public void 배치를_바꾸면_진행_중이던_조각은_버린다()
    {
        var slot = ActiveSlot(Base);
        var now  = Base.AddSeconds(29);   // 29초 진행 중

        slot.Assign(ItemType.Mining, characterId: 200, now);

        slot.Industry.ShouldBe(ItemType.Mining);
        slot.CharacterId.ShouldBe(200);
        slot.LastTickAt.ShouldBe(now);

        // 이월하면 산업을 계속 갈아타며 29초 조각을 모으는 악용이 가능해진다.
        slot.ConsumeJudgeCount(now.AddSeconds(29)).ShouldBe(0);
    }

    [Fact]
    public void 시각이_거꾸로_가도_판정하지_않는다()
    {
        var slot = ActiveSlot(Base);

        slot.ConsumeJudgeCount(Base.AddSeconds(-100)).ShouldBe(0);
        slot.LastTickAt.ShouldBe(Base);
    }

    [Fact]
    public void 다음_판정까지_남은_시간을_알려준다()
    {
        var slot = ActiveSlot(Base);

        slot.TimeUntilNextJudge(Base).ShouldBe(TimeSpan.FromSeconds(30));
        slot.TimeUntilNextJudge(Base.AddSeconds(29)).ShouldBe(TimeSpan.FromSeconds(1));
        slot.TimeUntilNextJudge(Base.AddSeconds(45)).ShouldBe(TimeSpan.Zero);   // 음수가 되지 않는다
    }

    // ─────────────────────────── WorkStation ───────────────────────────

    /// <summary>낚시 테이블 하나만 등록된 독립 카탈로그.</summary>
    private static DropTableCatalog BuildCatalog()
    {
        var catalog = new DropTableCatalog();
        catalog.Register(ItemType.Fishing, DropTable.From(
            "FishingBasicTable",
            new[] { (ItemTID: 1001, Weight: 100) },
            r => r.ItemTID, r => r.Weight));
        return catalog;
    }

    [Fact]
    public void 슬롯마다_독립적으로_정산한다()
    {
        var station = new global::WSGameServer.User.WorkStation.WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base),                  // 5분 경과 → 10회
            new WorkStationSlot(1, ItemType.Fishing, 200, Base.AddSeconds(240)),  // 1분 경과 → 2회
        });

        var harvests = station.Settle(Base.AddSeconds(300), BuildCatalog());

        harvests.Count.ShouldBe(2);
        harvests.Single(h => h.SlotIndex == 0).JudgeCount.ShouldBe(10);
        harvests.Single(h => h.SlotIndex == 1).JudgeCount.ShouldBe(2);

        // 회당 산출 1개 고정 → 판정 수와 획득 수가 같다
        harvests.Single(h => h.SlotIndex == 0).Gained[1001].ShouldBe(10);
    }

    [Fact]
    public void 수확이_없는_슬롯은_결과에_담기지_않는다()
    {
        var station = new global::WSGameServer.User.WorkStation.WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base),
            new WorkStationSlot(1, ItemType.Fishing, 0,   Base),   // 캐릭터 없음
        });

        var harvests = station.Settle(Base.AddSeconds(60), BuildCatalog());

        harvests.Count.ShouldBe(1);
        harvests[0].SlotIndex.ShouldBe(0);
    }

    [Fact]
    public void 드롭_테이블이_없는_산업은_건너뛴다()
    {
        var station = new global::WSGameServer.User.WorkStation.WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base),
            new WorkStationSlot(1, ItemType.Mining,  200, Base),   // 시트 미작성
        });

        // 예외로 죽지 않고, 등록된 슬롯만 정산된다.
        var harvests = station.Settle(Base.AddSeconds(60), BuildCatalog());

        harvests.Count.ShouldBe(1);
        harvests[0].SlotIndex.ShouldBe(0);
    }

    [Fact]
    public void 슬롯을_해금하면_현재_시각부터_시작한다()
    {
        var station = new global::WSGameServer.User.WorkStation.WorkStation();

        var slot = station.Unlock(3, Base);

        slot.SlotIndex.ShouldBe(3);
        slot.LastTickAt.ShouldBe(Base);
        slot.IsActive.ShouldBeFalse();   // 산업·캐릭터가 아직 없다
        station.Count.ShouldBe(1);

        // 이미 있으면 덮어쓰지 않는다 — 진행도가 날아가면 안 된다
        station.Unlock(3, Base.AddHours(1)).LastTickAt.ShouldBe(Base);
    }
}
