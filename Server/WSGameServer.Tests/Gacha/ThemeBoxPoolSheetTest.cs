using GameData;

namespace WSGameServer;

/// <summary>
/// 테마 상자 6종의 열기 풀(레벨테마 7장 · T-209) 검증. 서버는 한 풀의 모든 행을 한 번에 굴리므로
/// <b>칸 → 레벨 → 등급 → 그 안의 하나</b> 계층 확률이 행 가중치에 미리 풀려 있어야 한다(풀 합 = 10억).
/// 깨지면: 엑셀을 손으로 고쳐 칸 비율이 어긋났거나, <c>GameDesign/scripts/theme-box-pools.py</c>를 다시 돌리지 않은 것이다.
/// </summary>
public class ThemeBoxPoolSheetTest
{
    public ThemeBoxPoolSheetTest() => GameTableFixture.EnsureLoaded();

    private static readonly int[] Pools = { 9, 12, 15, 18, 21, 37 };

    private static IEnumerable<GachaEquipTableRow> EquipsOf(int pool, bool accessory)
        => GameTable.GachaEquipTable.All.Where(r => r.GachaId == pool
            && (GameTable.EquipTable[r.EquipTID].EquipKind == EquipKind.Accessory) == accessory);

    [Theory]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(21)]
    [InlineData(37)]
    public void 칸_비율은_모든_테마_상자가_같다(int pool)
    {
        // 백만분율 × 1000: 장비 · 장신구 · 캐릭터 163,300 · 재료 · 골드 200,000 · 소모품 100,000 (레벨테마 7.1)
        var items = GameTable.GachaItemTable.All.Where(r => r.GachaId == pool).ToList();

        EquipsOf(pool, accessory: false).Sum(r => (long)r.Weight).ShouldBe(163_300_000);
        EquipsOf(pool, accessory: true).Sum(r => (long)r.Weight).ShouldBe(163_300_000);
        GameTable.GachaCharacterTable.All.Where(r => r.GachaId == pool).Sum(r => (long)r.Weight).ShouldBe(163_300_000);
        items.Where(r => r.ItemTID < 100_000).Sum(r => (long)r.Weight).ShouldBe(200_000_000);
        GameTable.GachaGoldTable.All.Where(r => r.GachaId == pool).Sum(r => (long)r.Weight).ShouldBe(200_000_000);
        items.Where(r => r.ItemTID >= 100_000).Sum(r => (long)r.Weight).ShouldBe(100_000_000);
    }

    [Fact]
    public void 왕실_상자의_신화_캐릭터는_캐릭터_칸의_4퍼센트다()
    {
        // 163,300,000 × 4% = 6,532,000 (레벨테마 7.2)
        GameTable.GachaCharacterTable.All
            .Where(r => r.GachaId == 37 && GameTable.CharacterTable[r.CharacterTID].GlobalRarity == GlobalRarity.Mythic)
            .Sum(r => (long)r.Weight).ShouldBe(6_532_000);
    }

    [Fact]
    public void 개척자의_상자는_희귀보다_높은_캐릭터를_주지_않는다()
    {
        GameTable.GachaCharacterTable.All.Where(r => r.GachaId == 9)
            .ShouldAllBe(r => GameTable.CharacterTable[r.CharacterTID].GlobalRarity <= GlobalRarity.Rare);
    }

    [Fact]
    public void 산골_상자의_장비는_Lv10이_70퍼센트_Lv20이_30퍼센트다()
    {
        // 163,300,000 × 70% = 114,310,000 · × 30% = 48,990,000 (장비레벨 3.1)
        var byLevel = EquipsOf(12, accessory: false)
            .GroupBy(r => GameTable.EquipTable[r.EquipTID].ItemLevel)
            .ToDictionary(g => g.Key, g => g.Sum(r => (long)r.Weight));

        byLevel.ShouldBe(new Dictionary<int, long> { [10] = 114_310_000, [20] = 48_990_000 }, ignoreOrder: true);
    }

    [Fact]
    public void 개척자의_상자는_Lv1_장비만_준다()
    {
        GameTable.GachaEquipTable.All.Where(r => r.GachaId == 9)
            .ShouldAllBe(r => GameTable.EquipTable[r.EquipTID].ItemLevel == 1);
    }

    [Fact]
    public void 골드는_단계마다_3배다()
    {
        // 개척자 135~271 / 271~677 → 왕실 ×243 = 32,805~65,853 / 65,853~164,511 (레벨테마 7.4)
        GameTable.GachaGoldTable.All.Where(r => r.GachaId == 37)
            .Select(r => (r.MinAmount, r.MaxAmount, r.Weight)).OrderBy(x => x.MinAmount).ToArray()
            .ShouldBe(new[] { (32_805, 65_853, 150_000_000), (65_853, 164_511, 50_000_000) });
    }

    [Fact]
    public void 같은_레벨_등급_안의_장비는_가중치가_같다()
    {
        // 정수로 나눈 나머지 때문에 1까지는 다를 수 있다.
        foreach (var pool in Pools)
        {
            var cells = GameTable.GachaEquipTable.All.Where(r => r.GachaId == pool)
                .GroupBy(r => (GameTable.EquipTable[r.EquipTID].ItemLevel, GameTable.EquipTable[r.EquipTID].GlobalRarity,
                               GameTable.EquipTable[r.EquipTID].EquipKind == EquipKind.Accessory));
            foreach (var cell in cells)
            {
                (cell.Max(r => r.Weight) - cell.Min(r => r.Weight)).ShouldBeLessThanOrEqualTo(1, $"풀 {pool} {cell.Key}");
            }
        }
    }
}
