using GameData;
using MikaProtocol;
using WSGameServer.Common;

namespace WSGameServer.User.WorkStation;

/// <summary>한 슬롯의 정산 결과. 무엇이 몇 개 나왔는지와 몇 번 판정했는지를 담는다.</summary>
public sealed record SlotHarvest(int SlotIndex, int JudgeCount, Dictionary<int, int> Gained);

/// <summary>
/// 플레이어가 가진 작업슬롯 전체. 슬롯 목록을 들고 <b>정산을 모아서 수행</b>한다.
///
/// <para>
/// 정산은 <b>로그인 · 슬롯 변경 · 상태 조회 · 30초 주기 푸시</b> 네 시점에서 같은 함수로 이루어진다.
/// 온라인/오프라인 경로가 갈리지 않는 이유는 진행도가 시각 하나로 표현되기 때문이다.
/// </para>
/// </summary>
public sealed class WorkStation
{
    private readonly Dictionary<int, WorkStationSlot> _slots = new();

    /// <summary>해금된 슬롯 수.</summary>
    public int Count => _slots.Count;

    public IEnumerable<WorkStationSlot> Slots => _slots.Values;

    /// <summary>DB에서 읽은 슬롯을 적재한다(로그인 시 1회).</summary>
    public void Load(IEnumerable<WorkStationSlot> slots)
    {
        _slots.Clear();
        foreach (var slot in slots)
            _slots[slot.SlotIndex] = slot;
    }

    public bool TryGet(int slotIndex, out WorkStationSlot slot) => _slots.TryGetValue(slotIndex, out slot!);

    /// <summary>슬롯을 해금한다. 이미 있으면 그대로 둔다.</summary>
    public WorkStationSlot Unlock(int slotIndex, DateTime now)
    {
        if (_slots.TryGetValue(slotIndex, out var existing))
            return existing;

        var slot = new WorkStationSlot(slotIndex, ItemType.None, characterId: 0, lastTickAt: now);
        _slots[slotIndex] = slot;
        return slot;
    }

    /// <summary>
    /// 모든 슬롯을 정산한다. <b>수확이 있는 슬롯만</b> 결과에 담는다.
    /// </summary>
    /// <remarks>
    /// 슬롯마다 독립적으로 계산한다 — 배치 시각이 달라 판정 횟수도 슬롯마다 다르다.
    /// 이전 구조(산업 택 1)에서는 접속당 한 번만 계산하면 됐지만, 이제 슬롯 수에 비례한다.
    /// </remarks>
    /// <param name="catalog">테스트에서 독립 카탈로그를 넣기 위한 인자. 생략하면 전역 인스턴스를 쓴다.</param>
    public List<SlotHarvest> Settle(DateTime now, DropTableCatalog? catalog = null)
    {
        catalog ??= DropTableCatalog.Instance;
        var harvests = new List<SlotHarvest>();

        foreach (var slot in _slots.Values)
        {
            var judgeCount = slot.ConsumeJudgeCount(now);
            if (judgeCount <= 0)
                continue;

            // 산업별 드롭 테이블이 아직 없을 수 있다(시트 미작성). 그때는 조용히 건너뛴다 —
            // 여기서 예외를 던지면 다른 슬롯의 정산까지 함께 죽는다.
            if (!catalog.TryGet(slot.Industry, out var table))
            {
                Console.WriteLine($"[채취] 드롭 테이블 없음, 건너뜀: {slot.Industry} (슬롯 {slot.SlotIndex})");
                continue;
            }

            // 회당 산출은 1개 고정이다. 늘어나면 여기서 judgeCount에 곱한다.
            // 효율배수(캐릭터 스탯)도 확정되면 이 자리에 들어간다.
            var rollCount = judgeCount * WorkStationSlot.YieldPerJudge;
            harvests.Add(new SlotHarvest(slot.SlotIndex, judgeCount, table.RollMany(rollCount)));
        }

        return harvests;
    }

    public List<WorkStationSlotInfo> Snapshot()
        => _slots.Values.OrderBy(s => s.SlotIndex).Select(s => s.ToInfo()).ToList();
}
