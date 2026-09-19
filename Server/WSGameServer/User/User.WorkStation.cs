using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>플레이어의 작업슬롯 전체. 채취는 여기서 시작된다. 칸 수·상한은 <c>WorkSlotTable</c>과 해금이 정한다.</summary>
    public WorkStation WorkStation { get; } = new();

    /// <summary>
    /// 이 산업 레벨이 열려 있는가. 원본은 <c>IndustryLevelTable.UnlockTID</c> → <c>t_user_unlock</c> 하나다(특성 노드로 연다).
    /// 기본 레벨(Lv1)은 늘 열려 있고, 표에 없는 레벨은 닫혀 있다. <b>해금은 계정 단위·영구다</b>(산업레벨.md 3.2).
    /// </summary>
    public bool IsIndustryLevelUnlocked(IndustryType industry, int level)
    {
        if (level == WorkStationSlot.DefaultIndustryLevel)
        {
            return true;
        }

        return level > WorkStationSlot.DefaultIndustryLevel &&
               _industryLevels.TryGet(industry, level, out var row) &&
               IsUnlocked(row.UnlockTID);
    }

    /// <summary>이 칸이 열려 있는가. <c>WorkSlotTable</c>에 없는 번호는 없는 칸이라 false다.</summary>
    private bool IsWorkSlotOpen(int slotIndex)
    {
        foreach (var slot in _unlockCatalog.WorkSlots)
        {
            if (slot.WorkSlotTID == slotIndex)
            {
                return IsUnlocked(slot.UnlockTID);
            }
        }

        return false;
    }

    /// <summary>해금이 작업슬롯 칸이면 그 칸을 만들고 스냅샷을 밀어 준다. 슬롯이 아닌 해금이면 아무것도 안 한다.</summary>
    private void OnWorkSlotUnlocked(int unlockTid, DateTime now)
    {
        if (!_unlockCatalog.TryGetWorkSlotOf(unlockTid, out var slotIndex))
        {
            return;
        }

        var slot = WorkStation.Unlock(slotIndex, now);
        Send(new S_WorkStationSlotSyncResponse { Slot = slot.ToInfo() });
    }

    /// <summary>DB에서 읽은 슬롯 Row를 도메인으로 변환해 적재한다(로그인 시 1회). 캐릭터·해금 적재가 먼저다.</summary>
    private void LoadWorkStation(IReadOnlyList<WorkStationSlotRow> rows, DateTime startedAt)
    {
        // 잠긴(또는 없는) 칸의 배치 행은 만들지 않는다 — "열렸다"의 원본은 t_user_unlock 하나다(해금 #18).
        // 행은 지우지 않는다. 미보유 캐릭터 경고와 같은 태도다.
        var openRows = new List<WorkStationSlotRow>(rows.Count);
        foreach (var r in rows)
        {
            if (!IsWorkSlotOpen(r.slot_index))
            {
                ServerLog.Warn("작업슬롯", $"잠긴 칸의 배치 행을 무시. Uid={Uid} Slot={r.slot_index}");
                continue;
            }

            openRows.Add(r);
        }

        // 보유하지 않은 캐릭터를 물고 있는 슬롯은 데이터 이상이다(방출·삭제 경로가 생기면 정상 발생 가능).
        // 배치는 유지하되 흔적을 남긴다 — 이런 슬롯은 기본 속도로 돌게 되어 조용히 어긋난다.
        foreach (var r in openRows)
        {
            if (r.character_id != 0 && !TryGetCharacter(r.character_id, out _))
            {
                ServerLog.Warn("작업슬롯",
                    $"슬롯이 미보유 캐릭터를 참조. Uid={Uid} Slot={r.slot_index} Character={r.character_id}");
            }
        }

        WorkStation.Load(openRows.Select(r =>
        {
            var industry = (IndustryType)r.industry;
            var level    = r.industry_level;
            return new WorkStationSlot(r.slot_index, industry, r.character_id, startedAt,
                                       industryLevel: level,
                                       judgeCostUnits: ResolveJudgeCost(industry, level));
        }));
    }

    /// <summary>
    /// 이 (산업, 레벨)의 판정 비용. 미지정 산업이거나 테이블 행이 없으면 기본(30초)으로 두고 경고만 남긴다 —
    /// 여기서 예외를 던지면 데이터 한 줄 누락에 로그인·배치가 통째로 죽는다 (드롭 테이블 누락과 같은 규약).
    /// </summary>
    private long ResolveJudgeCost(IndustryType industry, int level)
    {
        if (industry == IndustryType.None)
        {
            return WorkStationSlot.JudgeCost;
        }

        if (!_industryLevels.TryGet(industry, level, out _))
        {
            ServerLog.Warn("작업슬롯", $"IndustryLevelTable 행 없음, 기본 판정 비용(30초) 사용: {industry} Lv{level}");
            return WorkStationSlot.JudgeCost;
        }

        return _industryLevels.GetJudgeCostUnits(industry, level);
    }

    /// <summary>이 (산업, 레벨)의 판정 1회당 캐릭터 경험치. 행이 없으면 0 — 판정 비용과 같은 규약으로 정산을 죽이지 않는다.</summary>
    private int ResolveExpPerJudge(IndustryType industry, int level)
    {
        if (!_industryLevels.TryGet(industry, level, out var row))
        {
            return 0;
        }

        return row.ExpPerJudge;
    }

    /// <summary>슬롯 전체 스냅샷을 보낸다(로그인 직후).</summary>
    public void SendWorkStationSlots()
    {
        Send(new S_WorkStationSlotsResponse { Slots = WorkStation.Snapshot() });
    }

    /// <summary>모든 슬롯을 정산하고 수확을 인벤토리에 넣은 뒤 슬롯별로 밀어 준다. 로그인은 부르지 않는다(정산할 구간이 없다).</summary>
    /// <param name="notify">접속 종료 정산에서는 false — 보낼 곳이 없다. 지급·저장은 그대로 한다 → Server/docs/채취-정산.md 3장</param>
    /// <returns>정산된 슬롯 수.</returns>
    public int SettleWorkStation(DateTime now, bool notify = true)
    {
        var harvests = WorkStation.Settle(now, _dropTables);
        if (harvests.Count == 0)
        {
            return 0;
        }

        foreach (var harvest in harvests)
        {
            // 아이템별로 한 번씩만 인벤토리를 갱신한다(판정 횟수만큼 UPSERT하지 않는다).
            var changes = new List<ItemChangeInfo>(harvest.Gained.Count);
            foreach (var (itemTid, count) in harvest.Gained)
            {
                changes.Add(GainItem(itemTid, count));
            }

            // 판정 1회마다 배치된 캐릭터가 (산업, 레벨)의 ExpPerJudge만큼 경험치를 번다 (캐릭터 기획 5.2).
            if (WorkStation.TryGet(harvest.SlotIndex, out var slot) &&
                TryGetCharacter(slot.CharacterId, out var worker))
            {
                GrantCharacterExp(worker, harvest.JudgeCount * ResolveExpPerJudge(slot.Industry, slot.IndustryLevel), notify);
            }

            if (!notify)
            {
                continue;
            }

            Send(new S_GatherResultResponse
            {
                SlotIndex   = harvest.SlotIndex,
                JudgeCount  = harvest.JudgeCount,
                ItemChanges = changes,
            });

            if (WorkStation.TryGet(harvest.SlotIndex, out var settledSlot))
            {
                Send(new S_WorkStationSlotSyncResponse { Slot = settledSlot.ToInfo() });
            }
        }

        // 슬롯 자체는 저장하지 않는다. 진행도(LastTickAt·ProgressUnits)가 세션 지역 상태가 되면서
        // 정산으로 바뀌는 영속 상태가 없어졌다 — 지급된 아이템은 GainItem이 각자 저장한다.
        return harvests.Count;
    }

    /// <summary>슬롯에 산업·레벨·캐릭터를 배치한다. 바꾸기 전에 먼저 정산한다. 레벨은 클라가 보낸 값이라 해금 여부를 서버가 검증한다(P4).</summary>
    public void AssignWorkStation(
        int slotIndex,
        IndustryType industry,
        long characterId,
        DateTime now,
        int industryLevel = WorkStationSlot.DefaultIndustryLevel)
    {
        if (!WorkStation.TryGet(slotIndex, out var slot))
        {
            ServerLog.Warn("작업슬롯",
                $"배치 거절 — 없는 슬롯. Uid={Uid} Slot={slotIndex} Industry={industry} Character={characterId}");
            Send(new S_WorkStationAssignResponse { Result = EResultCode.InvalidSlotIndex });
            return;
        }

        // 적성 0인 산업에는 배치할 수 없다. 정산보다 먼저 막아야 상태를 건드리지 않고 끝난다.
        if (!CanAssignCharacter(characterId, industry))
        {
            // 미보유와 적성 0은 클라 조치가 다르다(id 오류 vs 기획상 불가). 결과 코드로도 구분해 준다.
            var owned = TryGetCharacter(characterId, out _);
            ServerLog.Warn("작업슬롯",
                $"배치 거절 — {(owned ? "적성 0" : "미보유 캐릭터")}. Uid={Uid} Slot={slotIndex} Industry={industry} Character={characterId}");
            Send(new S_WorkStationAssignResponse
            {
                Result = owned ? EResultCode.NoAptitude : EResultCode.CharacterNotOwned,
            });
            return;
        }

        // 해금하지 않은 레벨은 거절한다. 0·음수·표에 없는 레벨도 닫힌 것으로 본다.
        if (!IsIndustryLevelUnlocked(industry, industryLevel))
        {
            ServerLog.Warn("작업슬롯",
                $"배치 거절 — 미해금 레벨. Uid={Uid} Slot={slotIndex} Industry={industry} Level={industryLevel}");
            Send(new S_WorkStationAssignResponse { Result = EResultCode.IndustryLevelLocked });
            return;
        }

        // 배치 변경 전 구간 정산 (해당 슬롯뿐 아니라 전체를 정리해 둔다)
        SettleWorkStation(now);

        slot.Assign(industry, characterId, now, industryLevel, ResolveJudgeCost(industry, industryLevel));

        // 배치가 바뀌면 그 캐릭터의 속도로 갈아탄다. Assign이 방금 구간을 끊었으므로
        // 여기서 바꾸는 것은 소급되지 않는다.
        slot.ApplyWorkSpeed(ResolveSlotSpeed(slot));

        SaveWorkStationSlots(new[] { slotIndex });

        Send(new S_WorkStationAssignResponse { Result = EResultCode.Ok, Slot = slot.ToInfo() });
    }

    /// <summary>슬롯 속도를 다시 계산해 반영한다(버프·스탯 변화). 반드시 정산을 먼저 한다.</summary>
    /// <param name="notify">로그인 적재 중에는 false — 직후 SendWorkStationSlots가 어차피 보낸다.</param>
    public void RefreshWorkStationSpeed(DateTime now, bool notify = true)
    {
        SettleWorkStation(now);

        foreach (var slot in WorkStation.Slots)
        {
            if (slot.ApplyWorkSpeed(ResolveSlotSpeed(slot)) && notify)
            {
                Send(new S_WorkStationSlotSyncResponse { Slot = slot.ToInfo() });
            }
        }
    }

    // 이 슬롯의 채취 속도(천분율). 속도에 관여하는 것은 전부 여기로 모은다 — 정산 → ApplyWorkSpeed 순서를 타는 유일한 경로라
    // 여기서 곱하면 소급이 불가능하다. 합성 규칙은 WorkSpeed → Server/docs/채취-정산.md 5장
    private int ResolveSlotSpeed(WorkStationSlot slot)
    {
        // 비어 있는 슬롯은 어차피 돌지 않는다(IsActive=false). 값은 의미가 없으므로 기준값을 둔다.
        var baseSpeed = !slot.IsActive || !TryGetCharacter(slot.CharacterId, out var character)
            ? WorkStationSlot.DefaultWorkSpeed
            : character.GetBaseWorkSpeed(slot.Industry);

        return WorkSpeed.From(baseSpeed)
            // 착용 장비 가산(전 산업 + 슬롯 산업). 특성·부스트도 여기에 .Add(천분율)로 붙는다 — 개수가 늘어도 각 보정의 몫은 그대로다.
            .Add(GetEquipSpeedAdd(slot.CharacterId, slot.Industry))
            .Add(GetTraitSpeedAdd(slot.Industry))
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
        {
            PostDBTask(new SaveWorkStationSlotRepository(this, targets));
        }
    }
}
