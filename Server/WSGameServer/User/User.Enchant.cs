using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    // 큐브 사용. 검증 → 큐브 소모 → 등급 판정 · 칸 전부 다시 뽑기 → 저장 → 응답·싱크.
    // 착용 중인 장비를 거절하므로 정산도 속도 재확정도 없다 — 가동 중인 슬롯의 속도가 소급으로 바뀔 수 없다.
    public void TryEnchant(long equipId, int itemTid, Random? random = null)
    {
        if (!TryGetEquip(equipId, out var equip))
        {
            RejectEnchant(EResultCode.EquipNotOwned, equipId, $"미보유 장비 {equipId}");
            return;
        }

        if (equip.IsEquipped)
        {
            RejectEnchant(EResultCode.EnchantEquipped, equipId, "착용 중");
            return;
        }

        if (equip.HasPendingEnchant)
        {
            RejectEnchant(EResultCode.EnchantPending, equipId, "상급 큐브 결과를 고르기 전");
            return;
        }

        if (!_enchantCatalog.TryGetItem(itemTid, out var cube))
        {
            RejectEnchant(EResultCode.ItemNotUsable, equipId, $"큐브가 아님 {itemTid}");
            return;
        }

        var slotCount = _enchantCatalog.SlotCountOf(equip.Row.GlobalRarity);
        if (slotCount <= 0)
        {
            RejectEnchant(EResultCode.ItemNotUsable, equipId, $"칸이 없는 장비 등급 {equip.Row.GlobalRarity}");
            return;
        }

        if (!TryConsumeItems(new Dictionary<int, int> { [itemTid] = 1 }, out var consumed))
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"보유 부족 {itemTid}");
            return;
        }

        var rng         = random ?? Random.Shared;
        var beforeGrade = equip.EnchantGrade;

        // 인챈트가 없으면 일반으로 시작한다. 있으면 상승 판정만 한다 — 등급은 내려가지 않는다.
        var grade  = GlobalRarity.Common;
        var rankUp = false;
        if (beforeGrade != GlobalRarity.None)
        {
            rankUp = Rolled(_enchantCatalog.RankUpPermyriadOf(beforeGrade, cube), rng);
            grade  = rankUp ? EnchantCatalog.NextGrade(beforeGrade) : beforeGrade;
        }

        var options = _enchantCatalog.RollOptions(grade, slotCount, rng);

        // 상급 큐브는 결과를 보류한다 — 장비는 그대로이고 고르기(TryChooseEnchant)가 정한다.
        if (cube.CanKeepPrevious)
        {
            equip.SetPendingEnchant(grade, options);
        }
        else
        {
            equip.SetEnchant(grade, options);
        }

        SaveEnchant(equip);

        ServerLog.Info("인챈트", $"큐브 {itemTid} Uid={Uid} 장비 {equipId} 등급 {beforeGrade}→{grade} 상승={rankUp} 보류={cube.CanKeepPrevious}");

        Send(new S_EquipEnchantResponse
        {
            Result         = EResultCode.Ok,
            EquipId        = equipId,
            Success        = rankUp,
            BeforeGrade    = (int)beforeGrade,
            AfterGrade     = (int)grade,
            Options        = options.Select(o => o.EnchantOptionTID).ToList(),
            AwaitingChoice = cube.CanKeepPrevious,
        });

        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
        Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = consumed });
    }

    /// <summary>상급 큐브의 보류 결과를 고른다. keepNew면 새 값(등급 + 칸 전부), 아니면 이전 값이 남는다.</summary>
    public void TryChooseEnchant(long equipId, bool keepNew)
    {
        if (!TryGetEquip(equipId, out var equip))
        {
            RejectChoose(EResultCode.EquipNotOwned, $"미보유 장비 {equipId}");
            return;
        }

        if (!equip.HasPendingEnchant)
        {
            RejectChoose(EResultCode.EnchantNoPending, "보류 없음");
            return;
        }

        equip.ResolvePendingEnchant(keepNew);
        SaveEnchant(equip);

        ServerLog.Info("인챈트", $"고르기 Uid={Uid} 장비 {equipId} 새 값={keepNew} 등급 {equip.EnchantGrade}");

        Send(new S_EquipEnchantChooseResponse { Result = EResultCode.Ok, EquipId = equipId, KeepNew = keepNew });
        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
        return;

        void RejectChoose(EResultCode code, string reason)
        {
            ServerLog.Warn("인챈트", $"고르기 거절 {code} Uid={Uid} 장비={equipId} 사유={reason}");
            Send(new S_EquipEnchantChooseResponse { Result = code, EquipId = equipId, KeepNew = keepNew });
        }
    }

    /// <summary>인챈트와 보류를 함께 저장한다 — 둘은 한 행의 칸이라 늘 같이 덮어쓴다.</summary>
    private void SaveEnchant(Equip equip)
    {
        var tids    = equip.EnchantOptionTids;
        var pending = equip.PendingEnchantOptionTids;
        PostDBTask(new SaveEquipEnchantRepository(
            this, equip.Id, (int)equip.EnchantGrade, TidAt(tids, 0), TidAt(tids, 1), TidAt(tids, 2),
            (int)equip.PendingEnchantGrade, TidAt(pending, 0), TidAt(pending, 1), TidAt(pending, 2)));
    }

    /// <summary>만분율 판정. 10000이면 항상 성공, 0이면 항상 실패다.</summary>
    private static bool Rolled(int permyriad, Random random)
        => permyriad > 0 && random.Next(EnchantCatalog.PermyriadScale) < permyriad;

    /// <summary>빈 칸은 0으로 저장한다.</summary>
    private static int TidAt(IReadOnlyList<int> tids, int index)
        => index < tids.Count ? tids[index] : 0;

    private void RejectEnchant(EResultCode code, long equipId, string reason)
    {
        ServerLog.Warn("인챈트", $"거절 {code} Uid={Uid} 장비={equipId} 사유={reason}");

        Send(new S_EquipEnchantResponse { Result = code, EquipId = equipId });
    }
}
