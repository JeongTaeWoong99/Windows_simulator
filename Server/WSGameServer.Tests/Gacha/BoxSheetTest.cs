using GameData;

namespace WSGameServer;

/// <summary>
/// 실제 상자 데이터 검증 — <c>Item.xlsx</c>의 상자와 <c>Gacha.xlsx</c>의 상자 풀이 맞물리는지 본다.
/// <b>테마 상자</b>는 채취 단계마다 1종(6종 · T-208)이고, <b>열 수 있는 아이템</b>은 <c>OpenGachaId</c>가 있는 것 전부다.
/// 깨지면: 열 수 없는 상자가 생기거나, 상자에서 상자가 나와 무한히 까지거나, 상자 풀을 골드로 사게 된다.
/// </summary>
public class BoxSheetTest
{
    public BoxSheetTest() => GameTableFixture.EnsureLoaded();

    private static IEnumerable<ItemTableRow> Boxes
    {
        get
        {
            var raidBoxTids = GameTable.RaidBossTable.All.Select(r => r.RewardBoxItemTID).ToHashSet();
            return GameTable.ItemTable.All.Where(r => r.ItemType == ItemType.Box && !raidBoxTids.Contains(r.ItemTID));
        }
    }

    private static IEnumerable<ItemTableRow> Openables => GameTable.ItemTable.All.Where(r => r.OpenGachaId != 0);

    private static IEnumerable<ItemTableRow> Relics => GameTable.ItemTable.All.Where(r => r.ItemType == ItemType.Relic);

    /// <summary>채취 단계 레벨 → 그 단계 테마 상자 (레벨테마.md 2장).</summary>
    private static readonly Dictionary<int, int> ThemeBoxOfLevel = new()
    {
        [1] = 100081, [20] = 100082, [40] = 100083, [60] = 100084, [80] = 100085, [100] = 100086,
    };

    [Fact]
    public void 상자는_채취_단계마다_한_종류이고_최대_1000개다()
    {
        // 개척자의 상자 ~ 왕실 상자 6종. 재질(나무·은·황금) 축은 없다(T-208).
        Boxes.Select(r => r.ItemTID).OrderBy(t => t).ShouldBe(new[] { 100081, 100082, 100083, 100084, 100085, 100086 });
        Boxes.ShouldAllBe(r => r.OpenGachaId != 0 && r.MaxStack == 1000);
    }

    [Fact]
    public void 옛_재질_상자_15종은_표에_없다()
    {
        // 지운 TID는 다시 쓰지 않는다 — 옛 DB 행이 다른 것으로 둔갑한다(excel-table-creator 규칙 2).
        for (var tid = 100019; tid <= 100033; tid++)
        {
            GameTable.ItemTable.TryGet(tid, out _).ShouldBeFalse($"옛 상자 {tid}가 남아 있다");
        }
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
    public void 캐릭터는_테마_상자와_캐릭터_뽑기권에서만_나온다()
    {
        // 테마 상자 캐릭터 칸(레벨테마 7.2)과 이미 가진 뽑기권(T-184 — 구매는 닫힘) 말고는 열쇠·유물에서 캐릭터가 나오지 않는다.
        var allowed = ThemeBoxOfLevel.Values.Concat(GameTable.ShopTable.All.Select(r => r.ItemTID)).ToHashSet();
        var pools   = Openables.Where(r => !allowed.Contains(r.ItemTID)).Select(r => r.OpenGachaId).ToHashSet();

        GameTable.GachaCharacterTable.All.Where(r => pools.Contains(r.GachaId)).ShouldBeEmpty();
    }

    [Fact]
    public void 테마_상자는_저마다_다른_풀을_연다()
    {
        // 왕실 상자가 성주 풀 21을 임시로 쓰던 것을 새 풀 37로 옮겼다(T-209).
        ThemeBoxOfLevel.Values.Select(tid => GameTable.ItemTable[tid].OpenGachaId)
            .ShouldBe(new[] { 9, 12, 15, 18, 21, 37 });
    }

    [Fact]
    public void 채취_단계마다_그_단계_테마_상자_한_행이_있다()
    {
        // 공통 행이 단계마다 있어야 한다 — 빠진 단계는 덮어쓰기가 없는 산업에서 상자를 얻을 길이 없다.
        var common = GameTable.CommonRewardTable.All;

        common.Select(r => (r.IndustryLevel, r.ItemTID)).OrderBy(x => x.IndustryLevel)
            .ShouldBe(ThemeBoxOfLevel.Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Key));
    }

    [Fact]
    public void 덮어쓰기는_그_단계_테마_상자이거나_유물이다()
    {
        // 발굴은 상자 대신 미감정 유물을 굴린다(T-150). 다른 단계 상자가 섞이면 단계 구분이 무너진다.
        GameTable.CommonRewardOverrideTable.All.ShouldAllBe(r =>
            (ThemeBoxOfLevel.ContainsKey(r.IndustryLevel) && ThemeBoxOfLevel[r.IndustryLevel] == r.ItemTID) ||
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
