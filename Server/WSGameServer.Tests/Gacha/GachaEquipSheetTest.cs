using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 <c>GachaEquipTable</c> 시트 검증. 장비는 <b>상자에서만</b> 얻는다 — 장비 골드 뽑기(풀 3·4·5)는 닫았다(T-200 · #117).
/// 깨지면: 장비를 주는 풀에 골드 비용이 다시 붙었거나, 장비 풀인데 비용 메타(<c>GachaInfoTable</c>)가 빠진 것이다.
/// </summary>
public class GachaEquipSheetTest
{
    public GachaEquipSheetTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public void 장비를_주는_풀은_전부_골드로_직접_뽑을_수_없다()
    {
        // 골드 직접 풀이 남으면 상자 확률표(장비레벨 3장)를 우회해 장비를 산다.
        foreach (var gachaId in GameTable.GachaEquipTable.All.Select(r => r.GachaId).Distinct())
        {
            GameTable.GachaInfoTable[gachaId].CostCurrency.ShouldBe(CurrencyType.None, $"장비 풀 {gachaId}");
        }
    }

    [Fact]
    public void 장비_풀은_모두_비용_메타가_있다()
    {
        // 메타가 없으면 GachaService가 InvalidGachaId로 거절한다 — 시트만 있고 뽑을 수 없는 풀이 된다.
        var missing = GameTable.GachaEquipTable.All
            .Select(r => r.GachaId)
            .Distinct()
            .Where(id => !GameTable.GachaInfoTable.TryGet(id, out _));

        missing.ShouldBeEmpty();
    }
}
