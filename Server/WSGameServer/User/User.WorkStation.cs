using GameData;
using MikaProtocol;
using WSGameServer.Repository;

// 클래스 안에 WorkStation 프로퍼티가 있어 같은 이름의 네임스페이스가 가려진다. 별칭으로 우회한다.
using Slot = WSGameServer.User.WorkStation.WorkStationSlot;
using WorkSpeed = WSGameServer.User.WorkStation.WorkSpeed;

namespace WSGameServer.User;

public partial class User
{
    /// <summary>플레이어의 작업슬롯 전체. 채취는 여기서 시작된다.</summary>
    public WorkStation.WorkStation WorkStation { get; } = new();

    /// <summary>슬롯 전체 스냅샷을 보낸다(로그인 직후).</summary>
    public void SendWorkStationSlots()
    {
        Send(new S_WorkStationSlotsResponse { Slots = WorkStation.Snapshot() });
    }

    /// <summary>
    /// 모든 슬롯을 정산하고, 수확이 있으면 인벤토리에 넣은 뒤 <b>슬롯별로 결과를 밀어 준다.</b>
    ///
    /// <para>
    /// 슬롯 변경 · 주기 푸시 · 접속 종료가 전부 이 함수를 부른다.
    /// <b>로그인은 부르지 않는다</b> — 오프라인 진행이 없으므로 정산할 구간이 없다.
    /// </para>
    /// </summary>
    /// <param name="notify">
    /// 결과 패킷을 보낼지 여부. 접속 종료 정산에서는 false로 준다 —
    /// 세션이 이미 끊겨 보낼 곳이 없다. <b>아이템 지급과 DB 저장은 그대로 수행된다</b>
    /// (접속 중에 완성된 판정은 정당하게 번 것이므로 버리지 않는다).
    /// </param>
    /// <returns>정산된 슬롯 수.</returns>
    public int SettleWorkStation(DateTime now, bool notify = true)
    {
        var harvests = WorkStation.Settle(now);
        if (harvests.Count == 0)
            return 0;

        foreach (var harvest in harvests)
        {
            // 아이템별로 한 번씩만 인벤토리를 갱신한다(판정 횟수만큼 UPSERT하지 않는다).
            var changes = new List<ItemChangeInfo>(harvest.Gained.Count);
            foreach (var (itemTid, count) in harvest.Gained)
                changes.Add(GainItem(itemTid, count));

            if (!notify)
                continue;

            Send(new S_GatherResultResponse
            {
                SlotIndex   = harvest.SlotIndex,
                JudgeCount  = harvest.JudgeCount,
                ItemChanges = changes,
            });
        }

        // 슬롯 자체는 저장하지 않는다. 진행도(LastTickAt·ProgressUnits)가 세션 지역 상태가 되면서
        // 정산으로 바뀌는 영속 상태가 없어졌다 — 지급된 아이템은 GainItem이 각자 저장한다.
        return harvests.Count;
    }

    /// <summary>
    /// 슬롯에 산업과 캐릭터를 배치한다.
    /// <b>바꾸기 전에 먼저 정산한다</b> — 이전 구간은 이전 설정으로 계산돼야 한다.
    /// </summary>
    public void AssignWorkStation(int slotIndex, ItemType industry, long characterId)
    {
        var now = DateTime.UtcNow;

        if (!WorkStation.TryGet(slotIndex, out var slot))
        {
            Send(new S_WorkStationAssignResponse { Success = false });
            return;
        }

        // 적성 0인 산업에는 배치할 수 없다. 정산보다 먼저 막아야 상태를 건드리지 않고 끝난다.
        if (!CanAssignCharacter(characterId, industry))
        {
            Send(new S_WorkStationAssignResponse { Success = false });
            return;
        }

        // 배치 변경 전 구간 정산 (해당 슬롯뿐 아니라 전체를 정리해 둔다)
        SettleWorkStation(now);

        slot.Assign(industry, characterId, now);

        // 배치가 바뀌면 그 캐릭터의 속도로 갈아탄다. Assign이 방금 구간을 끊었으므로
        // 여기서 바꾸는 것은 소급되지 않는다.
        slot.ApplyWorkSpeed(ResolveSlotSpeed(slot));

        SaveWorkStationSlots(new[] { slotIndex });

        Send(new S_WorkStationAssignResponse { Success = true, Slot = slot.ToInfo() });
    }

    /// <summary>
    /// 슬롯의 채취 속도를 다시 계산해 반영한다(버프 적용·만료, 캐릭터 스탯 상승 등).
    /// <b>반드시 정산을 먼저 한다</b> — 안 그러면 아직 정산되지 않은 구간까지 새 속도로 계산된다.
    /// </summary>
    /// <param name="notify">
    /// 바뀌었을 때 슬롯 스냅샷을 보낼지. 로그인 적재 중에는 false로 준다 —
    /// 아직 <c>S_LoginResponse</c>도 나가기 전이라, 직후 <c>SendWorkStationSlots</c>가 어차피 보낸다.
    /// </param>
    public void RefreshWorkStationSpeed(bool notify = true)
    {
        SettleWorkStation(DateTime.UtcNow);

        var changed = false;
        foreach (var slot in WorkStation.Slots)
            changed |= slot.ApplyWorkSpeed(ResolveSlotSpeed(slot));

        // 주기가 달라졌으면 클라이언트 카운트다운도 다시 맞춰야 한다.
        if (changed && notify)
            SendWorkStationSlots();
    }

    /// <summary>
    /// 이 슬롯의 채취 속도(천분율)를 구한다. <b>보정을 가산·승산으로 분류해 모은 뒤 한 번에 적용한다</b>
    /// (<see cref="WorkSpeed"/>).
    ///
    /// <para>
    /// <b>속도 = 적성기본값 × (1 + Σ가산) × Π승산</b>
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>기본값</b> — 배치된 캐릭터의 해당 산업 적성을 <c>WorkSpeedTable</c>로 변환한 값</item>
    /// <item><b>가산</b> — 특성 패시브 · 액티브 부스트 · 장비. 전부 <b>기본값 기준 비율</b>로 더한다. 아직 미작성</item>
    /// <item><b>승산</b> — 결과 전체에 얹히는 배수. 현재는 <see cref="Global.GatherSpeedMultiplier"/> 하나뿐</item>
    /// </list>
    ///
    /// <para>
    /// <b>속도에 관여하는 것은 전부 여기로 모은다.</b> 누적식(<c>ConsumeJudgeCount</c>)에 배수를 곱하면
    /// 정산할 때마다 배수가 <b>아직 정산되지 않은 구간에까지 소급</b>된다. 이 함수는 "정산 → ApplyWorkSpeed"
    /// 순서를 타는 유일한 경로라, 여기서 곱하면 소급이 원천적으로 불가능하다.
    /// </para>
    /// </summary>
    private int ResolveSlotSpeed(Slot slot)
    {
        // 비어 있는 슬롯은 어차피 돌지 않는다(IsActive=false). 값은 의미가 없으므로 기준값을 둔다.
        var baseSpeed = !slot.IsActive || !TryGetCharacter(slot.CharacterId, out var character)
            ? Slot.DefaultWorkSpeed
            : character.GetBaseWorkSpeed(slot.Industry);

        return WorkSpeed.From(baseSpeed)
            // 특성·부스트·장비는 여기에 .Add(천분율)로 붙는다 — 개수가 늘어도 각 보정의 몫은 그대로다.
            .Multiply(GatherSpeedMultiplier)
            .Resolve();
    }

    /// <summary>지정한 슬롯들의 <b>배치 설정</b>을 DB에 반영한다(진행도는 저장하지 않는다).</summary>
    private void SaveWorkStationSlots(IEnumerable<int> slotIndexes)
    {
        var targets = slotIndexes
            .Select(i => WorkStation.TryGet(i, out var s) ? s : null)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        if (targets.Count > 0)
            PostDBTask(new SaveWorkStationSlotRepository(this, targets));
    }
}
