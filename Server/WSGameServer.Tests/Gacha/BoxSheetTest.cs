using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 상자 데이터 검증 — <c>Item.xlsx</c>의 상자와 <c>Gacha.xlsx</c>의 상자 풀이 맞물리는지 본다.
/// <b>레벨 상자</b>는 <c>ItemType = Box</c> 15종이고, <b>열 수 있는 아이템</b>은 <c>OpenGachaId</c>가 있는 것 전부다
/// (레벨 상자 + 황금 열쇠 + 미감정 유물 — T-153 · T-150).
/// 깨지면: 열 수 없는 상자가 생기거나, 상자에서 상자가 나와 무한히 까지거나, 상자 풀을 골드로 사게 된다.
/// </summary>
public class BoxSheetTest
{
    public BoxSheetTest() => GameTableFixture.EnsureLoaded();

    private static IEnumerable<ItemTableRow> Boxes => GameTable.ItemTable.All.Where(r => r.ItemType == ItemType.Box);

    private static IEnumerable<ItemTableRow> Openables => GameTable.ItemTable.All.Where(r => r.OpenGachaId != 0);

    private static IEnumerable<ItemTableRow> Relics => GameTable.ItemTable.All.Where(r => r.ItemType == ItemType.Relic);

    [Fact]
    public void 상자는_산업_레벨마다_세_등급이고_최대_1000개다()
    {
        // 나무·은·황금 × Lv1~5 = 15종.
        Boxes.Count().ShouldBe(15);
        Boxes.GroupBy(r => r.GlobalRarity).Select(g => (g.Key, g.Count())).OrderBy(g => g.Key)
            .ShouldBe(new[] { (GlobalRarity.Common, 5), (GlobalRarity.Uncommon, 5), (GlobalRarity.Rare, 5) });
        Boxes.ShouldAllBe(r => r.OpenGachaId != 0 && r.MaxStack == 1000);
    }

    [Fact]
    public void 열_수_있는_아이템의_풀은_비용_재화가_None이다()
    {
        // None이면 GachaService.Draw가 거절한다 — 상자를 거치지 않고 내용물을 사는 길을 막는다.
        foreach (var openable in Openables)
        {
            GameTable.GachaInfoTable.TryGet(openable.OpenGachaId, out var info).ShouldBeTrue();
            info.CostCurrency.ShouldBe(CurrencyType.None);
        }
    }

    [Fact]
    public void 열어서_나온_것을_계속_열어도_처음_것으로_돌아오지_않는다()
    {
        // 열쇠 → 금고, 유물 → Special 유물처럼 한 단계 더 열리는 것은 된다. 자기 자신으로 돌아오면 무한히 까진다.
        var poolOf = Openables.ToDictionary(r => r.ItemTID, r => r.OpenGachaId);
        var next = poolOf.ToDictionary(
            kv => kv.Key,
            kv => GameTable.GachaItemTable.All.Where(r => r.GachaId == kv.Value && poolOf.ContainsKey(r.ItemTID)).Select(r => r.ItemTID).ToList());

        foreach (var start in poolOf.Keys)
        {
            var seen = new HashSet<int>();
            var stack = new Stack<int>(next[start]);
            while (stack.Count > 0)
            {
                var tid = stack.Pop();
                tid.ShouldNotBe(start, $"{start}을(를) 열다 보면 다시 {start}이(가) 나온다");
                if (seen.Add(tid))
                {
                    foreach (var n in next[tid])
                    {
                        stack.Push(n);
                    }
                }
            }
        }
    }

    [Fact]
    public void 열_수_있는_아이템의_풀에서_캐릭터가_나오지_않는다()
    {
        var pools = Openables.Select(r => r.OpenGachaId).ToHashSet();

        GameTable.GachaCharacterTable.All.Where(r => pools.Contains(r.GachaId)).ShouldBeEmpty();
    }

    [Fact]
    public void 산업_레벨마다_그_레벨_상자_세_종류가_있다()
    {
        // 공통 행이 레벨마다 있어야 한다 — 빠진 레벨은 덮어쓰기가 없는 산업에서 상자를 얻을 길이 없다.
        var common = GameTable.CommonRewardTable.All;
        var boxOfLevel = Boxes.ToDictionary(r => r.ItemTID, r => (r.ItemTID - 100019) / 3 + 1);

        common.Count.ShouldBe(15);
        common.GroupBy(r => r.IndustryLevel).Select(g => g.Key).OrderBy(l => l).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        common.GroupBy(r => r.IndustryLevel).ShouldAllBe(g => g.Select(r => r.ItemTID).Distinct().Count() == 3);
        common.ShouldAllBe(r => boxOfLevel[r.ItemTID] == r.IndustryLevel);

        // 덮어쓰기는 그 레벨 상자이거나, 레벨이 없는 유물이다(발굴 — T-150).
        GameTable.CommonRewardOverrideTable.All.ShouldAllBe(r =>
            (boxOfLevel.ContainsKey(r.ItemTID) && boxOfLevel[r.ItemTID] == r.IndustryLevel) ||
            GameTable.ItemTable[r.ItemTID].ItemType == ItemType.Relic);
    }

    [Fact]
    public void 공통_보상은_상자나_유물만_주고_드롭_테이블에는_없다()
    {
        // 상자·유물은 자원 롤과 따로 굴린다 — 드롭 테이블에 섞이면 자원을 대신해 두 번 나온다.
        var boxTids   = Boxes.Select(r => r.ItemTID).ToHashSet();
        var rewardTids = boxTids.Concat(Relics.Select(r => r.ItemTID)).ToHashSet();

        GameTable.CommonRewardTable.All.ShouldAllBe(r => boxTids.Contains(r.ItemTID));
        GameTable.CommonRewardOverrideTable.All.ShouldAllBe(r => rewardTids.Contains(r.ItemTID));
        GameTable.FarmingBasicTable.All.Select(r => r.ItemTID)
            .Concat(GameTable.ExcavationBasicTable.All.Select(r => r.ItemTID))
            .Concat(GameTable.MiningBasicTable.All.Select(r => r.ItemTID))
            .Concat(GameTable.LoggingBasicTable.All.Select(r => r.ItemTID))
            .Concat(GameTable.HuntingBasicTable.All.Select(r => r.ItemTID))
            .Where(rewardTids.Contains)
            .ShouldBeEmpty();
    }
}
