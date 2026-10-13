using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 상자 데이터 검증 — <c>Item.xlsx</c>의 여는 아이템(<c>OpenGachaId</c>)과 <c>Gacha.xlsx</c>의 상자 풀이 맞물리는지 본다.
/// 깨지면: 열 수 없는 상자가 생기거나, 열기가 돌고 돌아 무한히 까지거나, 상자 풀을 골드로 사게 된다.
/// </summary>
public class BoxSheetTest
{
    public BoxSheetTest() => GameTableFixture.EnsureLoaded();

    // 레벨 상자는 나무·은·황금 × Lv1~5 = 100019~100033. 황금 열쇠(100012 → 비밀 금고)처럼 여는 아이템이 더 있다.
    private const int FirstBoxTid = 100019;
    private const int LastBoxTid  = 100033;

    private static IEnumerable<ItemTableRow> Openables => GameTable.ItemTable.All.Where(r => r.OpenGachaId != 0);

    private static IEnumerable<ItemTableRow> Boxes => Openables.Where(r => r.ItemTID is >= FirstBoxTid and <= LastBoxTid);

    [Fact]
    public void 상자는_산업_레벨마다_세_등급이고_최대_1000개다()
    {
        // 나무·은·황금 × Lv1~5 = 15종.
        Boxes.Count().ShouldBe(15);
        Boxes.GroupBy(r => r.GlobalRarity).Select(g => (g.Key, g.Count())).OrderBy(g => g.Key)
            .ShouldBe(new[] { (GlobalRarity.Common, 5), (GlobalRarity.Uncommon, 5), (GlobalRarity.Rare, 5) });
        Boxes.ShouldAllBe(r => r.ItemType == ItemType.Special && r.MaxStack == 1000);
    }

    [Fact]
    public void 상자_풀은_비용_재화가_None이다()
    {
        // None이면 GachaService.Draw가 거절한다 — 상자를 거치지 않고 내용물을 사는 길을 막는다.
        foreach (var box in Openables)
        {
            GameTable.GachaInfoTable.TryGet(box.OpenGachaId, out var info).ShouldBeTrue();
            info.CostCurrency.ShouldBe(CurrencyType.None);
        }
    }

    [Fact]
    public void 여는_아이템을_열고_또_열어도_처음_것으로_돌아오지_않는다()
    {
        // 황금 상자 → 황금 열쇠 → 비밀 금고처럼 한 단계 더 여는 건 괜찮다. 다시 처음 것이 나오면 무한히 까진다.
        var poolOf = Openables.ToDictionary(r => r.ItemTID, r => r.OpenGachaId);
        var next = poolOf.ToDictionary(
            kv => kv.Key,
            kv => GameTable.GachaItemTable.All
                .Where(r => r.GachaId == kv.Value && poolOf.ContainsKey(r.ItemTID))
                .Select(r => r.ItemTID)
                .ToList());

        var looped = poolOf.Keys.Where(start => Reaches(next, start, start, new HashSet<int>()));

        looped.ShouldBeEmpty();
    }

    private static bool Reaches(Dictionary<int, List<int>> next, int from, int target, HashSet<int> visited)
    {
        foreach (var tid in next[from])
        {
            if (tid == target)
            {
                return true;
            }

            if (visited.Add(tid) && Reaches(next, tid, target, visited))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void 상자_풀에서_캐릭터가_나오지_않는다()
    {
        var boxPools = Openables.Select(r => r.OpenGachaId).ToHashSet();

        GameTable.GachaCharacterTable.All.Where(r => boxPools.Contains(r.GachaId)).ShouldBeEmpty();
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
        GameTable.CommonRewardOverrideTable.All.ShouldAllBe(r => boxOfLevel.ContainsKey(r.ItemTID) && boxOfLevel[r.ItemTID] == r.IndustryLevel);
    }

    [Fact]
    public void 공통_보상은_상자만_주고_드롭_테이블에는_상자가_없다()
    {
        // 상자는 자원 롤과 따로 굴린다 — 드롭 테이블에 섞이면 자원을 대신해 두 번 나온다.
        var boxTids = Boxes.Select(r => r.ItemTID).ToHashSet();

        GameTable.CommonRewardTable.All.ShouldAllBe(r => boxTids.Contains(r.ItemTID));
        GameTable.CommonRewardOverrideTable.All.ShouldAllBe(r => boxTids.Contains(r.ItemTID));
        GameTable.FarmingBasicTable.All.Select(r => r.ItemTID)
            .Concat(GameTable.FishingBasicTable.All.Select(r => r.ItemTID))
            .Concat(GameTable.MiningBasicTable.All.Select(r => r.ItemTID))
            .Concat(GameTable.LoggingBasicTable.All.Select(r => r.ItemTID))
            .Concat(GameTable.HuntingBasicTable.All.Select(r => r.ItemTID))
            .Where(boxTids.Contains)
            .ShouldBeEmpty();
    }
}
