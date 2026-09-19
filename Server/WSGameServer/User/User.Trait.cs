using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 특성 노드 하나를 찍는다. 노드 TID = <c>UnlockTID</c>라 판정은 해금과 같고(<see cref="CheckUnlockConditions"/>),
    /// 거기에 특성 포인트가 붙는다. <b>포인트는 조건을 전부 통과한 뒤에만 빠진다.</b> 찍은 기록은 <c>t_user_unlock</c> 하나다.
    /// </summary>
    public void TryLearnTrait(int userTraitTid, DateTime now)
    {
        if (!_traitCatalog.TryGet(userTraitTid, out var trait) ||
            !_unlockCatalog.TryGetUnlock(userTraitTid, out var unlock))
        {
            Reject(EResultCode.InvalidUserTraitTID, "없는 TID");
            return;
        }

        var (code, reason) = CheckUnlockConditions(userTraitTid, unlock);
        if (code != EResultCode.Ok)
        {
            Reject(code, reason);
            return;
        }

        // 특성 노드의 해금 행에는 골드를 두지 않는다 — 비용은 특성 포인트 하나다. 두면 여기서 함께 받는다.
        if (unlock.Gold > 0 && Gold < unlock.Gold)
        {
            Reject(EResultCode.NotEnoughCurrency, $"골드 {Gold} < {unlock.Gold}");
            return;
        }

        if (!TrySpendTraitPoint(trait.TraitPoint))
        {
            Reject(EResultCode.NotEnoughTraitPoint, $"포인트 {TraitPoint} < {trait.TraitPoint}");
            return;
        }

        if (unlock.Gold > 0)
        {
            TrySpendGold(unlock.Gold);
        }

        ApplyUnlock(userTraitTid, now);
        Send(new S_UserTraitLearnResponse { Result = EResultCode.Ok, UserTraitTID = userTraitTid });

        // 속도 특성은 열리는 순간부터 붙는다. 정산을 먼저 해야 이전 구간이 새 속도로 소급되지 않는다.
        if (trait.EffectType == UserTraitEffect.SpeedAdd)
        {
            RefreshWorkStationSpeed(now);
        }

        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("특성", $"거절 — {reason}. Uid={Uid} UserTraitTID={userTraitTid}");
            Send(new S_UserTraitLearnResponse { Result = code, UserTraitTID = userTraitTid });
        }
    }

    /// <summary>열린 속도 특성의 가산 합(천분율). 대상 산업이 <c>None</c>이면 전 산업에 붙는다.</summary>
    public int GetTraitSpeedAdd(IndustryType industry)
    {
        var sum = 0;
        foreach (var trait in _traitCatalog.SpeedAdds)
        {
            if ((trait.Industry == IndustryType.None || trait.Industry == industry) && IsUnlocked(trait.UserTraitTID))
            {
                sum += trait.EffectValue;
            }
        }

        return sum;
    }
}
