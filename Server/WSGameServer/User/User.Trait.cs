using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    // 기본 레벨보다 올린 특성만 담는다. 없으면 UserTraitTable.BaseLevel이다.
    private readonly Dictionary<int, int> _traitLevels = new();

    /// <summary>DB에서 읽은 특성 레벨을 적재한다(로그인 시 1회). 표에 없는 특성은 경고 후 버린다.</summary>
    public void LoadTraits(IReadOnlyList<UserTraitRow> rows)
    {
        _traitLevels.Clear();
        foreach (var row in rows)
        {
            if (!_traitCatalog.TryGet(row.user_trait_tid, out var trait))
            {
                ServerLog.Warn("특성", $"표에 없는 특성 — 버린다. Uid={Uid} UserTraitTID={row.user_trait_tid}");
                continue;
            }

            // 치트로 내린 특성은 기본 레벨 행으로 남는다 — 담지 않아야 목록이 내리기 직후와 같다.
            var level = Math.Clamp(row.level, trait.BaseLevel, trait.MaxLevel);
            if (level > trait.BaseLevel)
            {
                _traitLevels[row.user_trait_tid] = level;
            }
        }
    }

    /// <summary>특성의 지금 레벨. 올린 적 없으면 기본 레벨, 없는 특성이면 0.</summary>
    public int GetTraitLevel(int userTraitTid)
    {
        if (_traitLevels.TryGetValue(userTraitTid, out var level))
        {
            return level;
        }

        return _traitCatalog.TryGet(userTraitTid, out var trait) ? trait.BaseLevel : 0;
    }

    /// <summary>특성 레벨 스냅샷을 보낸다(로그인 직후 · 레벨 치트 뒤). 목록에 없는 특성은 기본 레벨이다.</summary>
    public void SendTraitList()
    {
        Send(new S_UserTraitListResponse
        {
            Traits = _traitLevels.OrderBy(p => p.Key)
                .Select(p => new UserTraitInfo { UserTraitTID = p.Key, Level = p.Value })
                .ToList(),
        });
    }

    /// <summary>
    /// 특성을 1레벨 올린다. 조건은 다음 레벨의 계정 레벨 + 특성 포인트다.
    /// <b>포인트는 조건을 전부 통과한 뒤에만 빠진다</b> — 거절 경로에서 포인트가 움직이지 않는다.
    /// </summary>
    public void TryLearnTrait(int userTraitTid, DateTime now)
    {
        if (!_traitCatalog.TryGet(userTraitTid, out var trait))
        {
            Reject(EResultCode.InvalidUserTraitTID, "없는 TID", 0);
            return;
        }

        var current = GetTraitLevel(userTraitTid);
        var next    = current + 1;
        if (next > trait.MaxLevel || !_traitCatalog.TryGetLevel(userTraitTid, next, out var levelRow))
        {
            Reject(EResultCode.TraitMaxLevel, $"최대 레벨 {trait.MaxLevel}", current);
            return;
        }

        if (AccountLevel < levelRow.AccountLevel)
        {
            Reject(EResultCode.UnlockLocked, $"계정 Lv{AccountLevel} < {levelRow.AccountLevel}", current);
            return;
        }

        if (!TrySpendTraitPoint(trait.TraitPoint))
        {
            Reject(EResultCode.NotEnoughTraitPoint, $"포인트 {TraitPoint} < {trait.TraitPoint}", current);
            return;
        }

        // 속도·산출량은 판정에 붙는다 — 레벨을 바꾸기 전에 정산해야 이전 구간에 소급되지 않는다.
        if (trait.EffectType is UserTraitEffect.SpeedAdd or UserTraitEffect.YieldAdd)
        {
            SettleWorkStation(now);
        }

        _traitLevels[userTraitTid] = next;
        PostDBTask(new SaveUserTraitRepository(this, userTraitTid, next));
        ServerLog.Info("특성", $"Lv{next} Uid={Uid} UserTraitTID={userTraitTid}");

        if (trait.EffectType == UserTraitEffect.SpeedAdd)
        {
            RefreshWorkStationSpeed(now);
        }

        Send(new S_UserTraitLearnResponse { Result = EResultCode.Ok, UserTraitTID = userTraitTid, Level = next });
        return;

        void Reject(EResultCode code, string reason, int level)
        {
            ServerLog.Warn("특성", $"거절 — {reason}. Uid={Uid} UserTraitTID={userTraitTid}");
            Send(new S_UserTraitLearnResponse { Result = code, UserTraitTID = userTraitTid, Level = level });
        }
    }

    /// <summary>조건·포인트 없이 특성 레벨을 정한다(치트 전용). 특성마다 기본~최대 레벨로 자르고, 바뀐 개수를 돌려준다.</summary>
    private int SetTraitLevels(IReadOnlyList<UserTraitTableRow> traits, int level, DateTime now)
    {
        var changes = traits
            .Select(trait => (Trait: trait, Next: Math.Clamp(level, trait.BaseLevel, trait.MaxLevel)))
            .Where(c => c.Next != GetTraitLevel(c.Trait.UserTraitTID))
            .ToList();

        // 정상 경로(TryLearnTrait)와 같다 — 레벨을 바꾸기 전에 정산해야 이전 구간에 소급되지 않는다.
        if (changes.Any(c => c.Trait.EffectType is UserTraitEffect.SpeedAdd or UserTraitEffect.YieldAdd))
        {
            SettleWorkStation(now);
        }

        foreach (var (trait, next) in changes)
        {
            if (next == trait.BaseLevel)
            {
                _traitLevels.Remove(trait.UserTraitTID);
            }
            else
            {
                _traitLevels[trait.UserTraitTID] = next;
            }

            PostDBTask(new SaveUserTraitRepository(this, trait.UserTraitTID, next));
            ServerLog.Info("특성", $"치트 Lv{next} Uid={Uid} UserTraitTID={trait.UserTraitTID}");
        }

        if (changes.Any(c => c.Trait.EffectType == UserTraitEffect.SpeedAdd))
        {
            ApplyWorkStationSpeed(notify: true);
        }

        // 여러 특성이 한꺼번에 바뀌고 내려가기도 해서 Learn 응답 대신 스냅샷 전체를 보낸다.
        if (changes.Count > 0)
        {
            SendTraitList();
        }

        return changes.Count;
    }

    /// <summary>속도 특성의 가산 합(천분율). 대상 산업이 <c>None</c>이면 전 산업에 붙는다.</summary>
    public int GetTraitSpeedAdd(IndustryType industry) => SumTraitEffect(_traitCatalog.SpeedAdds, industry);

    /// <summary>산출량 특성의 가산 합(천분율). 공통(<c>None</c>)과 그 산업을 더한다.</summary>
    public int GetTraitYieldAdd(IndustryType industry) => SumTraitEffect(_traitCatalog.YieldAdds, industry);

    // 레벨당 효과 × (지금 레벨 − 기본 레벨)
    private int SumTraitEffect(IReadOnlyList<UserTraitTableRow> traits, IndustryType industry)
    {
        var sum = 0;
        foreach (var trait in traits)
        {
            if (trait.Industry == IndustryType.None || trait.Industry == industry)
            {
                sum += trait.EffectValue * (GetTraitLevel(trait.UserTraitTID) - trait.BaseLevel);
            }
        }

        return sum;
    }
}
