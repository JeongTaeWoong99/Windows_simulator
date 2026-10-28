using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 거점 건물(T-183). 건물 레벨은 골드 + 자원으로 올리고, 그 레벨이 곧 건물 트리의 포인트다 → GameDesign/design/building/README.md
/// 건물 레벨 자체는 효과가 없다 — 효과는 트리 노드(특성)가 낸다.
/// </summary>
public partial class User
{
    // 지은(Lv1 이상) 건물만 담는다. 없으면 Lv0이다.
    private readonly Dictionary<int, int> _buildingLevels = new();

    /// <summary>DB에서 읽은 건물 레벨을 적재한다(로그인 시 1회). 표에 없는 건물은 경고 후 버린다.</summary>
    public void LoadBuildings(IEnumerable<UserBuildingRow> rows)
    {
        _buildingLevels.Clear();
        foreach (var row in rows)
        {
            if (!_traitCatalog.TryGetBuilding(row.building_tid, out var building))
            {
                ServerLog.Warn("건물", $"표에 없는 건물 — 버린다. Uid={Uid} BuildingTID={row.building_tid}");
                continue;
            }

            var level = Math.Clamp(row.level, 0, building.MaxLevel);
            if (level > 0)
            {
                _buildingLevels[row.building_tid] = level;
            }
        }
    }

    public int GetBuildingLevel(int buildingTid) => _buildingLevels.GetValueOrDefault(buildingTid);

    /// <summary>이 건물 트리에 남은 포인트 = 건물 레벨 − Σ(노드의 올린 레벨 × 레벨당 포인트). 치트로 레벨을 내리면 음수일 수 있다.</summary>
    public int GetBuildingPoints(int buildingTid)
    {
        var spent = 0;
        foreach (var node in _traitCatalog.NodesOf(buildingTid))
        {
            spent += (GetTraitLevel(node.UserTraitTID) - node.BaseLevel) * node.TraitPoint;
        }

        return GetBuildingLevel(buildingTid) - spent;
    }

    /// <summary>건물 레벨 스냅샷을 보낸다(로그인 직후 · 치트 뒤).</summary>
    public void SendBuildingList()
    {
        Send(new S_BuildingListResponse
        {
            Buildings = _buildingLevels.OrderBy(p => p.Key)
                .Select(p => new BuildingInfo { BuildingTID = p.Key, Level = p.Value })
                .ToList(),
        });
    }

    /// <summary>
    /// 건물을 1레벨 올린다. 판정 순서는 해금과 같다 — <b>차감이 맨 뒤다.</b>
    /// 상한(최대 · 계정 레벨)과 보유량(골드 · 자원)을 전부 본 뒤에만 뺀다. 자원은 인벤토리 먼저, 모자란 만큼 창고에서 뺀다.
    /// </summary>
    public void TryUpgradeBuilding(int buildingTid)
    {
        var current = GetBuildingLevel(buildingTid);
        if (!_traitCatalog.TryGetBuilding(buildingTid, out var building))
        {
            Reject(EResultCode.InvalidBuildingTID, "없는 TID");
            return;
        }

        var next = current + 1;
        if (next > building.MaxLevel || !_traitCatalog.TryGetBuildingLevel(buildingTid, next, out var cost))
        {
            Reject(EResultCode.BuildingMaxLevel, $"최대 레벨 {building.MaxLevel}");
            return;
        }

        if (next > AccountLevel)
        {
            Reject(EResultCode.BuildingAccountLevelCap, $"계정 Lv{AccountLevel} < 건물 Lv{next}");
            return;
        }

        if (!CanAffordGold(cost.Gold))
        {
            Reject(EResultCode.NotEnoughCurrency, $"골드 {Gold} < {cost.Gold}");
            return;
        }

        var itemCost = ItemCostOf(cost.ItemTIDs, cost.ItemCounts);
        var lacking  = itemCost.FirstOrDefault(kv => GetItemCount(kv.Key) < kv.Value);
        if (lacking.Value > 0)
        {
            Reject(EResultCode.NotEnoughItem, $"자원 {lacking.Key} {GetItemCount(lacking.Key)} < {lacking.Value}");
            return;
        }

        // 위에서 둘 다 확인했으므로 여기서는 실패하지 않는다.
        if (cost.Gold > 0 && !TrySpendGold(cost.Gold))
        {
            throw new InvalidOperationException($"확인한 골드를 빼지 못했다 — Uid={Uid} BuildingTID={buildingTid}");
        }

        if (itemCost.Count > 0)
        {
            if (!TryConsumeItems(itemCost, out var consumed))
            {
                throw new InvalidOperationException($"확인한 자원을 빼지 못했다 — Uid={Uid} BuildingTID={buildingTid}");
            }

            Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = consumed });
        }

        _buildingLevels[buildingTid] = next;
        PostDBTask(new SaveUserBuildingRepository(this, buildingTid, next));
        ServerLog.Info("건물", $"Lv{next} Uid={Uid} BuildingTID={buildingTid}");
        Send(new S_BuildingUpgradeResponse { Result = EResultCode.Ok, BuildingTID = buildingTid, Level = next });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("건물", $"거절 — {reason}. Uid={Uid} BuildingTID={buildingTid}");
            Send(new S_BuildingUpgradeResponse { Result = code, BuildingTID = buildingTid, Level = current });
        }
    }

    /// <summary>비용 없이 건물 레벨을 정한다(치트 전용). 건물마다 0~최대 레벨로 자르고, 바뀐 개수를 돌려준다.</summary>
    private int SetBuildingLevels(IReadOnlyList<BuildingTableRow> buildings, int level)
    {
        var changed = 0;
        foreach (var building in buildings)
        {
            var next = Math.Clamp(level, 0, building.MaxLevel);
            if (next == GetBuildingLevel(building.BuildingTID))
            {
                continue;
            }

            if (next == 0)
            {
                _buildingLevels.Remove(building.BuildingTID);
            }
            else
            {
                _buildingLevels[building.BuildingTID] = next;
            }

            PostDBTask(new SaveUserBuildingRepository(this, building.BuildingTID, next));
            ServerLog.Info("건물", $"치트 Lv{next} Uid={Uid} BuildingTID={building.BuildingTID}");
            changed++;
        }

        if (changed > 0)
        {
            SendBuildingList();
        }

        return changed;
    }
}
