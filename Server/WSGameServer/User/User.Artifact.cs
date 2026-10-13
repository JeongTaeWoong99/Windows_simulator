using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트 보관함(T-150). 인벤토리가 아니라 번호(<c>ArtifactTID</c>)별 칸에 수량으로 쌓인다.
/// <b>수량이 1 이상인 번호만 효과가 켜지고, 같은 번호는 몇 개든 효과 1개분이다.</b> 효과는 특성과 같은 자리에 더한다.
/// </summary>
public partial class User
{
    /// <summary>감정 비용 할인 합계의 상한(천분율) — 감정이 공짜가 되지 않게 50%에서 자른다.</summary>
    public const int MaxAppraiseDiscountPermille = 500;

    // 얻은 적 있는 번호 → 수량. 수량 0도 남긴다(도감에서 사라지지 않게).
    private readonly Dictionary<int, int> _artifacts = new();

    /// <summary>DB에서 읽은 보관함을 적재한다(로그인 시 1회). 표에 없는 번호는 경고 후 버린다.</summary>
    public void LoadArtifacts(IReadOnlyList<UserArtifactRow> rows)
    {
        _artifacts.Clear();
        foreach (var row in rows)
        {
            if (!GameTable.ArtifactTable.TryGet(row.artifact_tid, out _))
            {
                ServerLog.Warn("아티팩트", $"표에 없는 번호 — 버린다. Uid={Uid} ArtifactTID={row.artifact_tid}");
                continue;
            }

            _artifacts[row.artifact_tid] = Math.Max(0, row.count);
        }
    }

    /// <summary>그 번호의 보유 수량. 얻은 적 없으면 0.</summary>
    public int GetArtifactCount(int artifactTid) => _artifacts.GetValueOrDefault(artifactTid);

    /// <summary>보관함 스냅샷을 보낸다(로그인 직후 · 수량이 바뀐 뒤).</summary>
    public void SendArtifactList()
    {
        Send(new S_ArtifactListResponse
        {
            Artifacts = _artifacts.OrderBy(p => p.Key)
                .Select(p => new ArtifactInfo { ArtifactTid = p.Key, Count = p.Value })
                .ToList(),
        });
    }

    /// <summary>
    /// 아티팩트를 보관함에 넣는다(감정 보상). 인벤토리 칸을 먹지 않아 가득 차도 막히지 않는다.
    /// 처음 켜지는 번호가 속도·산출량을 바꾸면 <b>넣기 전에 정산</b>한다 — 새 효과가 지난 구간에 소급되지 않게.
    /// </summary>
    public void GainArtifacts(IReadOnlyDictionary<int, int> gained, DateTime now)
    {
        var valid = gained.Where(kv => kv.Value > 0 && GameTable.ArtifactTable.TryGet(kv.Key, out _)).ToList();
        if (valid.Count == 0)
        {
            return;
        }

        var turningOn = valid.Where(kv => GetArtifactCount(kv.Key) == 0)
            .Select(kv => GameTable.ArtifactTable[kv.Key])
            .ToList();
        var touchesWork = turningOn.Any(a => a.EffectType is UserTraitEffect.SpeedAdd or UserTraitEffect.YieldAdd or UserTraitEffect.RelicChanceAdd);
        if (touchesWork)
        {
            SettleWorkStation(now);
        }

        var changes = new List<(int ArtifactTid, int Count)>(valid.Count);
        foreach (var (artifactTid, count) in valid)
        {
            var next = GetArtifactCount(artifactTid) + count;
            _artifacts[artifactTid] = next;
            changes.Add((artifactTid, next));
            ServerLog.Info("아티팩트", $"+{count} → {next} Uid={Uid} ArtifactTID={artifactTid}");
        }

        PostDBTask(new SaveUserArtifactRepository(this, changes));

        if (turningOn.Any(a => a.EffectType == UserTraitEffect.SpeedAdd))
        {
            ApplyWorkStationSpeed(notify: true);
        }

        SendArtifactList();
    }

    /// <summary>
    /// 아티팩트를 보관함에서 뺀다(거래소 등록 — 즉시 판매는 없다). 호출자가 수량을 먼저 확인한다.
    /// 마지막 1개면 효과가 꺼진다 — <b>빼기 전에 정산</b>하고, 속도면 다시 매긴다. 저장은 호출자가 같은 트랜잭션으로 한다.
    /// </summary>
    /// <returns>뺀 뒤 남은 수량(확정값).</returns>
    public int RemoveArtifact(int artifactTid, int count, DateTime now)
    {
        var next = GetArtifactCount(artifactTid) - count;
        if (count <= 0 || next < 0)
        {
            throw new InvalidOperationException($"아티팩트를 가진 것보다 많이 뺀다 — 번호 {artifactTid} 보유 {GetArtifactCount(artifactTid)} 요청 {count}");
        }

        var turningOff = next == 0 && GameTable.ArtifactTable.TryGet(artifactTid, out var artifact) ? artifact : null;
        if (turningOff?.EffectType is UserTraitEffect.SpeedAdd or UserTraitEffect.YieldAdd or UserTraitEffect.RelicChanceAdd)
        {
            SettleWorkStation(now);
        }

        _artifacts[artifactTid] = next;
        ServerLog.Info("아티팩트", $"-{count} → {next} Uid={Uid} ArtifactTID={artifactTid}");

        if (turningOff?.EffectType == UserTraitEffect.SpeedAdd)
        {
            ApplyWorkStationSpeed(notify: true);
        }

        SendArtifactList();
        return next;
    }

    /// <summary>켜진 아티팩트의 효과 합(천분율). 대상 산업이 <c>None</c>인 아티팩트는 전 산업에 붙는다 — 특성과 같은 규약.</summary>
    public int GetArtifactEffect(UserTraitEffect effect, IndustryType industry)
    {
        var sum = 0;
        foreach (var (artifactTid, count) in _artifacts)
        {
            if (count <= 0 || !GameTable.ArtifactTable.TryGet(artifactTid, out var artifact) || artifact.EffectType != effect)
            {
                continue;
            }

            if (artifact.Industry == IndustryType.None || artifact.Industry == industry)
            {
                sum += artifact.EffectValue;
            }
        }

        return sum;
    }

    /// <summary>감정 1회 비용. 아티팩트 할인을 붙이고 50%에서 자른다.</summary>
    public long GetAppraiseCost(long baseCost)
        => DiscountAppraiseCost(baseCost, GetArtifactEffect(UserTraitEffect.AppraiseCostDiscount, IndustryType.None));

    /// <summary>할인(천분율)을 붙인 감정 비용. 할인은 <see cref="MaxAppraiseDiscountPermille"/>에서 자르고, 소수는 버린다.</summary>
    public static long DiscountAppraiseCost(long baseCost, int discountPermille)
    {
        var discount = Math.Clamp(discountPermille, 0, MaxAppraiseDiscountPermille);
        return baseCost * (WorkStationSlot.WorkSpeedScale - discount) / WorkStationSlot.WorkSpeedScale;
    }
}
