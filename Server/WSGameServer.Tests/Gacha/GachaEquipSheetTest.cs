using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 <c>GachaEquipTable</c> 시트 검증. 장비 뽑기는 <b>무기 · 장신구 · 보석을 풀로 나눈다</b>(T-067).
/// 깨지면: 엑셀에서 한 풀에 다른 종류가 섞였거나, 장비 풀인데 비용 메타(<c>GachaInfoTable</c>)가 빠진 것이다.
/// </summary>
public class GachaEquipSheetTest
{
    public GachaEquipSheetTest() => GameTableFixture.EnsureLoaded();

    [Fact]
    public void 장비_뽑기_풀은_풀마다_장비_종류가_하나다()
    {
        // 골드로 사는 풀만 본다 — 상자 풀(비용 재화 None)은 여러 종류를 섞어 준다(T-029).
        var kindsByPool = GameTable.GachaEquipTable.All
            .Where(r => GameTable.GachaInfoTable.TryGet(r.GachaId, out var info) && info.CostCurrency != CurrencyType.None)
            .GroupBy(r => r.GachaId)
            .ToDictionary(g => g.Key, g => g.Select(r => KindOf(r.EquipTID)).Distinct().ToList());

        kindsByPool.ShouldNotBeEmpty();
        kindsByPool.ShouldAllBe(pool => pool.Value.Count == 1);
    }

    [Fact]
    public void 무기_장신구_보석이_각자_풀을_갖는다()
    {
        var kinds = GameTable.GachaEquipTable.All
            .Where(r => GameTable.GachaInfoTable.TryGet(r.GachaId, out var info) && info.CostCurrency != CurrencyType.None)
            .GroupBy(r => r.GachaId)
            .Where(g => g.Select(r => KindOf(r.EquipTID)).Distinct().Count() == 1)
            .SelectMany(g => g)
            .Select(r => KindOf(r.EquipTID))
            .Distinct()
            .OrderBy(k => k);

        kinds.ShouldBe(new[] { EquipKind.Weapon, EquipKind.Accessory, EquipKind.Gem });
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

    private static EquipKind KindOf(int equipTid)
    {
        GameTable.EquipTable.TryGet(equipTid, out var row).ShouldBeTrue();
        return row.EquipKind;
    }
}
