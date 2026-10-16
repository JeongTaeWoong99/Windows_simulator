using GameData;

namespace WSGameServer;

/// <summary>
/// 섬 건물(T-166). 건물 레벨·노드는 해금 행이라 상태를 따로 갖지 않는다 — <b>열린 해금 목록이 곧 건물 상태다.</b>
/// 노드 효과는 특성·아티팩트와 같은 가산 항에 더한다 → GameDesign/design/building/README.md 2.1
/// </summary>
public partial class User
{
    /// <summary>열린 건물 노드의 효과 합(천분율). 노드는 자기 건물의 산업에만 붙는다.</summary>
    public int GetBuildingEffect(BuildingEffectType effect, IndustryType industry)
    {
        var sum = 0;
        foreach (var unlockTid in _unlocks)
        {
            if (!_unlockCatalog.TryGetBuildingNode(unlockTid, out var node))
            {
                continue;
            }

            if (node.EffectType == effect && node.Industry == industry)
            {
                sum += node.EffectValue;
            }
        }

        return sum;
    }
}
