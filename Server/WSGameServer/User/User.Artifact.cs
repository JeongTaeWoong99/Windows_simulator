using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 아티팩트(T-150 · T-181). 감정한 아티팩트는 <b>인벤토리 아티팩트 탭</b>에 번호별로 쌓이고, 번호마다 1개를 <b>도감에 장착</b>한다.
/// <b>도감에 장착하고 켜 둔 번호만 효과가 붙는다</b> — 인벤토리에 몇 개가 있든 효과는 없다. 효과는 특성과 같은 자리에 더한다.
/// 장착은 되돌릴 수 없다 — 빼는 대신 끈다.
/// </summary>
public partial class User
{
    /// <summary>감정 비용 할인 합계의 상한(천분율) — 감정이 공짜가 되지 않게 50%에서 자른다.</summary>
    public const int MaxAppraiseDiscountPermille = 500;

    /// <summary>아티팩트 칸 하나에 쌓이는 같은 번호의 상한 — Constants.xlsx.</summary>
    public static int ArtifactMaxStack => (int)Constants.ArtifactMaxStack;

    // 도감에 장착한 번호 → 켜짐.
    private readonly Dictionary<int, bool> _codex = new();

    /// <summary>인벤토리 아티팩트 탭. 키(<c>Item.Id</c>)는 아티팩트 번호다. 창고에는 아티팩트 탭이 없다.</summary>
    public Inventory Artifacts { get; } = new(EContainer.Inventory, () => StorageCapacity);

    /// <summary>DB에서 읽은 도감·아티팩트 칸을 적재한다(로그인 시 1회). 표에 없는 번호는 경고 후 버린다.</summary>
    public void LoadArtifacts(IReadOnlyList<UserArtifactRow> codexRows, IReadOnlyList<UserArtifactStackRow> stackRows)
    {
        _codex.Clear();
        foreach (var row in codexRows)
        {
            if (IsKnownArtifact(row.artifact_tid, "도감"))
            {
                _codex[row.artifact_tid] = row.is_active != 0;
            }
        }

        Artifacts.Load(stackRows
            .Where(r => r.count > 0 && IsKnownArtifact(r.artifact_tid, "칸"))
            .Select(r => new Item(r.artifact_tid, r.count, r.slot)));
    }

    private bool IsKnownArtifact(int artifactTid, string where)
    {
        if (GameTable.ArtifactTable.TryGet(artifactTid, out _))
        {
            return true;
        }

        ServerLog.Warn("아티팩트", $"표에 없는 번호 — {where}에서 버린다. Uid={Uid} ArtifactTID={artifactTid}");
        return false;
    }

    /// <summary>인벤토리에 있는 그 번호의 수량(칸 합계). 도감에 장착한 1개는 세지 않는다.</summary>
    public int GetArtifactCount(int artifactTid) => Artifacts.GetCount(artifactTid);

    public bool IsArtifactEquipped(int artifactTid) => _codex.ContainsKey(artifactTid);

    public bool IsArtifactActive(int artifactTid) => _codex.GetValueOrDefault(artifactTid);

    /// <summary>도감 스냅샷을 보낸다(로그인 직후 · 장착 · 켜고 끄기).</summary>
    public void SendArtifactList()
    {
        Send(new S_ArtifactListResponse
        {
            Artifacts = _codex.OrderBy(p => p.Key)
                .Select(p => new ArtifactCodexInfo { ArtifactTid = p.Key, IsActive = p.Value })
                .ToList(),
        });
    }

    /// <summary>인벤토리 아티팩트 탭 스냅샷을 보낸다(로그인 직후 · 칸이 바뀐 뒤).</summary>
    public void SendArtifactInventory()
    {
        Send(new S_ArtifactInventoryResponse { Stacks = Artifacts.Snapshot() });
    }

    /// <summary>아티팩트 탭에 전부 들어가는지 — 지급 전에 본다. 같은 번호의 덜 찬 칸부터 채우고 남으면 빈 칸을 쓴다.</summary>
    public bool HasArtifactRoomFor(IEnumerable<(int ArtifactTid, int Count)> gained)
    {
        var copy = Artifacts.Clone();
        foreach (var (artifactTid, count) in gained)
        {
            copy.AddItem(artifactTid, count, ArtifactMaxStack, out var added);
            if (added < count)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 아티팩트를 인벤토리 아티팩트 탭에 넣는다(감정 보상 · 우편 수령). 칸 검사는 부른 쪽이 <see cref="HasArtifactRoomFor"/>로 먼저 끝낸다.
    /// 인벤토리에 들어온 것만으로는 효과가 켜지지 않는다 — 도감 장착이 따로다.
    /// </summary>
    public void GainArtifacts(IReadOnlyDictionary<int, int> gained)
    {
        var changes = new List<ItemChangeInfo>();
        foreach (var (artifactTid, count) in gained)
        {
            if (count <= 0 || !GameTable.ArtifactTable.TryGet(artifactTid, out _))
            {
                continue;
            }

            changes.AddRange(Artifacts.AddItem(artifactTid, count, ArtifactMaxStack, out var added));
            if (added < count)
            {
                ServerLog.Error("아티팩트", $"칸이 모자라 버렸다 — 검사를 건너뛴 지급. Uid={Uid} ArtifactTID={artifactTid} 버림 {count - added}");
            }

            ServerLog.Info("아티팩트", $"인벤토리 +{added} Uid={Uid} ArtifactTID={artifactTid}");
        }

        if (changes.Count == 0)
        {
            return;
        }

        PostDBTask(new SaveArtifactStacksRepository(this, changes));
        SendArtifactInventory();
    }

    /// <summary>
    /// 인벤토리에서 아티팩트를 뺀다(경매 등록). 모자라면 아무것도 바꾸지 않고 false. 저장은 부른 쪽이 같은 트랜잭션으로 한다.
    /// 도감 장착분이 아니라 인벤토리 것이라 효과는 바뀌지 않는다.
    /// </summary>
    public bool TryRemoveArtifacts(IReadOnlyDictionary<int, int> request, out List<ItemChangeInfo> changes)
    {
        if (!Artifacts.TryRemoveItems(request, out changes))
        {
            return false;
        }

        SendArtifactInventory();
        return true;
    }

    /// <summary>
    /// 인벤토리의 아티팩트 1개를 도감에 장착한다. 번호마다 한 번이고 켜진 채로 시작한다.
    /// 새 효과가 속도·산출량을 바꾸면 <b>켜기 전에 정산</b>한다 — 지난 구간에 소급되지 않게.
    /// </summary>
    public void TryEquipArtifact(int artifactTid, DateTime now)
    {
        if (!GameTable.ArtifactTable.TryGet(artifactTid, out var artifact))
        {
            Reply(EResultCode.InvalidArtifact);
            return;
        }

        if (IsArtifactEquipped(artifactTid))
        {
            Reply(EResultCode.ArtifactAlreadyEquipped);
            return;
        }

        if (!Artifacts.TryRemoveItems(new Dictionary<int, int> { [artifactTid] = 1 }, out var changes))
        {
            Reply(EResultCode.NotEnoughItem);
            return;
        }

        SwitchArtifactEffect(artifact, now, () => _codex[artifactTid] = true);
        PostDBTask(new EquipArtifactRepository(this, artifactTid, changes));

        ServerLog.Info("아티팩트", $"도감 장착 Uid={Uid} ArtifactTID={artifactTid}");
        Reply(EResultCode.Ok);
        SendArtifactInventory();
        SendArtifactList();
        return;

        void Reply(EResultCode code)
        {
            if (code != EResultCode.Ok)
            {
                ServerLog.Warn("아티팩트", $"장착 거절 {code} Uid={Uid} ArtifactTID={artifactTid}");
            }

            Send(new S_ArtifactEquipResponse { Result = code, ArtifactTid = artifactTid });
        }
    }

    /// <summary>도감에 장착한 번호의 효과를 켜고 끈다. 지금과 같으면 아무것도 바꾸지 않고 Ok다.</summary>
    public void TrySetArtifactActive(int artifactTid, bool isActive, DateTime now)
    {
        if (!_codex.TryGetValue(artifactTid, out var current) || !GameTable.ArtifactTable.TryGet(artifactTid, out var artifact))
        {
            ServerLog.Warn("아티팩트", $"켜고 끄기 거절 — 장착 안 한 번호. Uid={Uid} ArtifactTID={artifactTid}");
            Send(new S_ArtifactActiveResponse { Result = EResultCode.ArtifactNotEquipped, ArtifactTid = artifactTid });
            return;
        }

        if (current != isActive)
        {
            SwitchArtifactEffect(artifact, now, () => _codex[artifactTid] = isActive);
            PostDBTask(new SaveArtifactActiveRepository(this, artifactTid, isActive));
            ServerLog.Info("아티팩트", $"{(isActive ? "켬" : "끔")} Uid={Uid} ArtifactTID={artifactTid}");
        }

        Send(new S_ArtifactActiveResponse { Result = EResultCode.Ok, ArtifactTid = artifactTid, IsActive = isActive });
        SendArtifactList();
    }

    /// <summary>
    /// 인벤토리의 아티팩트를 즉시 판다 — 판매가는 <c>BasePrice</c> × <c>SellRatePermille</c>.
    /// 전부 검증한 뒤에만 뺀다 — 하나라도 모자라면 아무것도 팔리지 않는다.
    /// </summary>
    public void TrySellArtifacts(IReadOnlyList<ArtifactInfo> request)
    {
        var merged = new Dictionary<int, int>();
        long gold  = 0;
        foreach (var a in request)
        {
            if (a.Count <= 0 || !GameTable.ArtifactTable.TryGet(a.ArtifactTid, out var row))
            {
                Reject(EResultCode.InvalidArtifact);
                return;
            }

            merged[a.ArtifactTid] = merged.GetValueOrDefault(a.ArtifactTid) + a.Count;
            gold += SellPrice(row.BasePrice) * a.Count;
        }

        if (merged.Count == 0)
        {
            Reject(EResultCode.InvalidArtifact);
            return;
        }

        if (!Artifacts.TryRemoveItems(merged, out var changes))
        {
            Reject(EResultCode.NotEnoughItem);
            return;
        }

        // GainGold를 쓰지 않는다 — 그쪽은 저장까지 해서 DB 작업이 칸 차감과 둘로 갈라진다.
        _gold = checked(_gold + gold);
        PostDBTask(new SellArtifactsRepository(this, changes, _gold));

        ServerLog.Info("상점", $"아티팩트 판매 Uid={Uid} {string.Join(",", merged.Select(kv => $"{kv.Key}×{kv.Value}"))} +{gold}G");
        Send(new S_ArtifactSellResponse { Result = EResultCode.Ok, GainedGold = gold });
        Send(new S_CurrencyResponse { Gold = _gold });
        SendArtifactInventory();
        return;

        void Reject(EResultCode code)
        {
            ServerLog.Warn("상점", $"아티팩트 판매 거절 {code} Uid={Uid}");
            Send(new S_ArtifactSellResponse { Result = code });
        }
    }

    // 효과가 켜지거나 꺼지는 순간 — 작업에 닿는 효과면 바꾸기 전에 정산하고, 속도면 바꾼 뒤 다시 매긴다.
    private void SwitchArtifactEffect(ArtifactTableRow artifact, DateTime now, Action apply)
    {
        if (artifact.EffectType is UserTraitEffect.SpeedAdd or UserTraitEffect.YieldAdd or UserTraitEffect.RelicChanceAdd)
        {
            SettleWorkStation(now);
        }

        apply();

        if (artifact.EffectType == UserTraitEffect.SpeedAdd)
        {
            ApplyWorkStationSpeed(notify: true);
        }
    }

    /// <summary>도감에 장착하고 켜 둔 아티팩트의 효과 합(천분율). 대상 산업이 <c>None</c>인 아티팩트는 전 산업에 붙는다 — 특성과 같은 규약.</summary>
    public int GetArtifactEffect(UserTraitEffect effect, IndustryType industry)
    {
        var sum = 0;
        foreach (var (artifactTid, isActive) in _codex)
        {
            if (!isActive || !GameTable.ArtifactTable.TryGet(artifactTid, out var artifact) || artifact.EffectType != effect)
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
