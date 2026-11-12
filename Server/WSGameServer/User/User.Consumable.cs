using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 소모품 사용(T-195) — 비약·영약·묘약·지도·나침반. 효과는 <c>ItemUseTable</c>(아이템 하나 = 효과 하나)이 정하고,
/// 여기는 효과 종류마다 처리기 하나만 둔다. 규칙: 아이템 기획 4.4 「사용 규칙」. 실패하면 아무것도 바뀌지 않는다.
/// </summary>
public partial class User
{
    /// <summary>
    /// <c>ItemUseTable</c>에 있는 아이템이면 소모품으로 쓰고 true. 없으면 false — 호출자가 상자 경로로 넘긴다.
    /// 결과는 항상 <see cref="S_ItemUseResponse"/>로 답한다.
    /// </summary>
    public bool TryUseConsumable(int itemTid, int count, long targetCharacterId, IReadOnlyList<int>? targetSlots, DateTime now)
    {
        if (!_itemUses.TryGet(itemTid, out var use))
        {
            return false;
        }

        var maxCount = use.UseEffect == ItemUseEffect.TraitReset ? 1 : GachaService.MaxOpenCount;
        if (count < 1 || count > maxCount)
        {
            RejectUse(itemTid, EResultCode.InvalidUseCount, $"개수 {count}");
            return true;
        }

        if (GetItemCount(itemTid) < count)
        {
            RejectUse(itemTid, EResultCode.NotEnoughItem, $"보유 {GetItemCount(itemTid)} < {count}");
            return true;
        }

        switch (use.UseEffect)
        {
            case ItemUseEffect.CharacterExp:
                UseExpPotion(use, count, targetCharacterId, now);
                break;
            case ItemUseEffect.TraitReset:
                UseTraitReset(use, now);
                break;
            case ItemUseEffect.InstantJudge:
                UseInstantJudge(use, count, targetSlots, now);
                break;
            default:
                UseSlotBuff(use, count, targetSlots, now);
                break;
        }

        return true;
    }

    // 성장의 비약 — 만렙을 넘기는 개수면 필요한 개수만 빠진다. 계정 경험치는 오르지 않는다(작업으로만).
    private void UseExpPotion(ItemUseTableRow use, int count, long characterId, DateTime now)
    {
        if (!TryGetCharacter(characterId, out var character))
        {
            RejectUse(use.ItemTID, EResultCode.InvalidUseTarget, $"미보유 캐릭터 {characterId}");
            return;
        }

        if (character.IsMaxLevel(_characterLevels))
        {
            RejectUse(use.ItemTID, EResultCode.CharacterMaxLevel, $"만렙 캐릭터 {characterId}");
            return;
        }

        var used = Math.Min(count, PotionsToMaxLevel(character, use.EffectValue));
        if (!TryConsumeItems(new Dictionary<int, int> { [use.ItemTID] = used }, out var consumed))
        {
            RejectUse(use.ItemTID, EResultCode.NotEnoughItem, $"차감 실패 {used}");
            return;
        }

        // 레벨이 오르면 속도 가산이 바뀐다 — 먼저 정산해야 새 속도가 지난 구간에 소급되지 않는다.
        SettleWorkStation(now);
        if (GrantCharacterExp(character, use.EffectValue * used, notify: true, accountExp: false))
        {
            ApplyWorkStationSpeed(notify: true);
        }

        ReplyUsed(use.ItemTID, used, consumed);
    }

    // 만렙까지 남은 경험치를 비약 몇 개로 채우는가(올림). 곡선이 끊겨 있으면 그 끝까지만 센다.
    private int PotionsToMaxLevel(Character character, int expPerPotion)
    {
        long need = -character.Exp;
        for (var level = character.Level + 1; level <= _characterLevels.MaxLevel; level++)
        {
            if (!_characterLevels.TryGetRequiredExp(level, out var required))
            {
                break;
            }

            need += required;
        }

        if (need <= 0 || expPerPotion <= 0)
        {
            return 1;
        }

        return (int)Math.Min(int.MaxValue, (need + expPerPotion - 1) / expPerPotion);
    }

    // 망각의 묘약 — 모든 건물 트리 노드를 기본 레벨로. 포인트는 건물 레벨에서 다시 센다(T-183).
    private void UseTraitReset(ItemUseTableRow use, DateTime now)
    {
        if (_traitLevels.Count == 0)
        {
            RejectUse(use.ItemTID, EResultCode.NothingToReset, "올린 노드 없음");
            return;
        }

        if (!TryConsumeItems(new Dictionary<int, int> { [use.ItemTID] = 1 }, out var consumed))
        {
            RejectUse(use.ItemTID, EResultCode.NotEnoughItem, "차감 실패");
            return;
        }

        SetTraitLevels(_traitCatalog.All.ToList(), 0, now, "망각의 묘약");
        ReplyUsed(use.ItemTID, 1, consumed);
    }

    // 보물 지도 — 고른 슬롯마다 판정 EffectValue × 개수번을 즉시 끝낸다. 돌고 있는 슬롯에만 쓴다.
    private void UseInstantJudge(ItemUseTableRow use, int count, IReadOnlyList<int>? targetSlots, DateTime now)
    {
        if (!TryResolveTargetSlots(use, targetSlots, requireActive: true, out var slots))
        {
            return;
        }

        if (!TryConsumeItems(new Dictionary<int, int> { [use.ItemTID] = count }, out var consumed))
        {
            RejectUse(use.ItemTID, EResultCode.NotEnoughItem, "차감 실패");
            return;
        }

        foreach (var slot in slots)
        {
            slot.AdvanceJudges(use.EffectValue * count);
        }

        // 얹은 작업량은 정산이 꺼내 간다 — 수확·경험치·버프 차감·푸시가 평소 판정과 같은 길을 탄다.
        SettleWorkStation(now);
        ReplyUsed(use.ItemTID, count, consumed);
    }

    // 영약·나침반 — 고른 슬롯마다 EffectCount × 개수회 버프를 건다. 캐릭터 없는·쉬는 슬롯도 된다.
    private void UseSlotBuff(ItemUseTableRow use, int count, IReadOnlyList<int>? targetSlots, DateTime now)
    {
        if (!TryResolveTargetSlots(use, targetSlots, requireActive: false, out var slots))
        {
            return;
        }

        if (!TryConsumeItems(new Dictionary<int, int> { [use.ItemTID] = count }, out var consumed))
        {
            RejectUse(use.ItemTID, EResultCode.NotEnoughItem, "차감 실패");
            return;
        }

        // 속도 버프는 정산 뒤에 건다 — 안 그러면 지난 구간까지 빨라진다.
        var isSpeed = use.UseEffect == ItemUseEffect.SlotWorkSpeed;
        if (isSpeed)
        {
            SettleWorkStation(now);
        }

        var changes = new List<SlotBuffChange>(slots.Count);
        foreach (var slot in slots)
        {
            slot.AddBuff(use.ItemTID, use.EffectCount * count);
            changes.Add(new SlotBuffChange(slot.SlotIndex, use.ItemTID, slot.Buffs[use.ItemTID]));

            if (isSpeed)
            {
                slot.ApplyWorkSpeed(ResolveSlotSpeed(slot));
            }

            Send(new S_WorkStationSlotSyncResponse { Slot = slot.ToInfo() });
        }

        PostDBTask(new SaveSlotBuffRepository(this, changes));
        ReplyUsed(use.ItemTID, count, consumed);
    }

    // 대상 슬롯 검사 — 1~SlotCount개, 중복 없이, 있는 칸. 거절했으면 이미 답했다.
    private bool TryResolveTargetSlots(ItemUseTableRow use, IReadOnlyList<int>? targetSlots, bool requireActive, out List<WorkStationSlot> slots)
    {
        slots = new List<WorkStationSlot>();
        if (targetSlots == null || targetSlots.Count < 1 || targetSlots.Count > use.SlotCount ||
            targetSlots.Distinct().Count() != targetSlots.Count)
        {
            RejectUse(use.ItemTID, EResultCode.InvalidUseTarget, $"슬롯 {targetSlots?.Count ?? 0}개 (최대 {use.SlotCount} · 중복 불가)");
            return false;
        }

        foreach (var index in targetSlots)
        {
            if (!WorkStation.TryGet(index, out var slot))
            {
                RejectUse(use.ItemTID, EResultCode.InvalidUseTarget, $"없는 슬롯 {index}");
                return false;
            }

            if (requireActive && !slot.IsActive)
            {
                RejectUse(use.ItemTID, EResultCode.UseTargetIdle, $"돌지 않는 슬롯 {index}");
                return false;
            }

            slots.Add(slot);
        }

        return true;
    }

    private void ReplyUsed(int itemTid, int used, List<ItemChangeInfo> consumed)
    {
        ServerLog.Info("소모품", $"사용 Uid={Uid} ItemTID={itemTid} Count={used}");

        // ⚠️ consumed는 차감 저장 작업이 DB 스레드에서 순회 중이다 — 새 목록에 담아 보낸다.
        Send(new S_ItemUseResponse
        {
            Result          = EResultCode.Ok,
            ItemTID         = itemTid,
            ItemChangeInfos = new List<ItemChangeInfo>(consumed),
            UsedCount       = used,
        });
    }

    private void RejectUse(int itemTid, EResultCode code, string reason)
    {
        ServerLog.Warn("소모품", $"거절 — {code} {reason}. Uid={Uid} ItemTID={itemTid}");
        Send(new S_ItemUseResponse { Result = code, ItemTID = itemTid });
    }

    // ───────────────────────── 슬롯 버프 ─────────────────────────

    /// <summary>DB의 버프 행을 슬롯에 얹는다(로그인 시 1회). 열린 칸이 전부 생긴 뒤에 부른다. 없는 칸·표에 없는 아이템은 경고 후 버린다.</summary>
    private void LoadSlotBuffs(IReadOnlyList<SlotBuffRow> rows)
    {
        foreach (var row in rows)
        {
            if (!WorkStation.TryGet(row.slot_index, out var slot) || !_itemUses.TryGet(row.item_tid, out var use) ||
                !ItemUseCatalog.IsSlotBuff(use.UseEffect))
            {
                ServerLog.Warn("소모품", $"쓸 수 없는 버프 행 — 버린다. Uid={Uid} Slot={row.slot_index} ItemTID={row.item_tid}");
                continue;
            }

            slot.AddBuff(row.item_tid, row.remaining);
        }
    }

    /// <summary>이 슬롯에 걸린 속도 버프(신속의 영약)의 가산 합(천분율).</summary>
    private int GetSlotBuffSpeedAdd(WorkStationSlot slot)
    {
        var sum = 0;
        foreach (var itemTid in slot.Buffs.Keys)
        {
            if (_itemUses.TryGet(itemTid, out var use) && use.UseEffect == ItemUseEffect.SlotWorkSpeed)
            {
                sum += use.EffectValue;
            }
        }

        return sum;
    }

    // 정산한 판정마다 붙는 버프 가산(천분율). 버프는 판정 앞쪽부터 깎이므로 i번째 판정에는 아직 남아 있던 버프만 붙는다.
    private int[] SlotBuffAddsPerJudge(IReadOnlyDictionary<int, int> applied, int judgeCount, Func<ItemUseEffect, bool> match)
    {
        var adds = new int[judgeCount];
        foreach (var (itemTid, used) in applied)
        {
            if (!_itemUses.TryGet(itemTid, out var use) || !match(use.UseEffect))
            {
                continue;
            }

            for (var i = 0; i < used && i < judgeCount; i++)
            {
                adds[i] += use.EffectValue;
            }
        }

        return adds;
    }

    // 나침반이 이 산업의 공통 보상에 붙는가. 발굴은 상자 대신 유물이 나와 상자·유물 나침반(황금)만 붙는다.
    private static Func<ItemUseEffect, bool> BoxBuffMatcher(IndustryType industry)
    {
        if (industry == IndustryType.Excavation)
        {
            return effect => effect == ItemUseEffect.SlotBoxRelicChance;
        }

        return effect => effect is ItemUseEffect.SlotBoxChance or ItemUseEffect.SlotBoxRelicChance;
    }
}
