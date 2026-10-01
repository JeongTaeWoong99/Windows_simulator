namespace WSGameServer;

/// <summary>
/// 서버 정렬(T-058)이 클라 임시 정렬(InventorySlotSource.CompareByRule)과 같은 순서를 내는지 지킨다.
/// 다르면 서버 정렬을 켠 순간 플레이어가 보던 순서가 한 번 뒤집힌다.
/// </summary>
public class StorageSortTest
{
    private static StorageSortEntry Res(long id, int rarity, int type, string name = "", long count = 1, bool away = false)
        => new(id, away, name, count, StorageSort.ResourceTieBreak(rarity, type, (int)id));

    [Fact]
    public void 등급순은_높은_등급_먼저_같으면_산업_그다음_TID다()
    {
        // 101(Common·농사) 102(Rare·낚시) 103(Rare·농사) 104(Common·농사)
        // Rare 먼저 → 103(농사1) 102(낚시2) → Common 101 104
        var entries = new[] { Res(101, 1, 1), Res(102, 3, 2), Res(103, 3, 1), Res(104, 1, 1) };

        StorageSort.Order(entries, StorageSortKey.Rarity, ascending: false)
            .ShouldBe(new long[] { 103, 102, 101, 104 });
    }

    [Fact]
    public void 오름차순은_규칙_전체를_뒤집는다()
    {
        var entries = new[] { Res(101, 1, 1), Res(102, 3, 2), Res(103, 3, 1), Res(104, 1, 1) };

        // 위 결과 103 102 101 104의 역순
        StorageSort.Order(entries, StorageSortKey.Rarity, ascending: true)
            .ShouldBe(new long[] { 104, 101, 102, 103 });
    }

    [Fact]
    public void 나가_있는_것은_오름차순이어도_맨_뒤다()
    {
        // 103이 가장 높은 등급이지만 배치 중 → 방향과 무관하게 끝
        var entries = new[] { Res(101, 1, 1), Res(103, 3, 1, away: true), Res(104, 1, 1) };

        StorageSort.Order(entries, StorageSortKey.Rarity, ascending: true)
            .ShouldBe(new long[] { 104, 101, 103 });
    }

    [Fact]
    public void 이름순은_서수_비교이고_같은_이름은_탭_규칙으로_가른다()
    {
        // 가 < 나 (유니코드 순서). 같은 '나' 둘은 등급 높은 202가 먼저
        var entries = new[] { Res(201, 1, 1, "나"), Res(202, 3, 1, "나"), Res(203, 1, 1, "가") };

        StorageSort.Order(entries, StorageSortKey.Name, ascending: false)
            .ShouldBe(new long[] { 203, 202, 201 });
    }

    [Fact]
    public void 수량순은_많은_것_먼저다()
    {
        var entries = new[] { Res(301, 1, 1, count: 5), Res(302, 1, 1, count: 40), Res(303, 1, 1, count: 12) };

        StorageSort.Order(entries, StorageSortKey.Count, ascending: false)
            .ShouldBe(new long[] { 302, 303, 301 });
    }

    [Fact]
    public void 장비는_등급_다음_종류_산업_TID_개체_순이다()
    {
        // 개체 1(Rare·종류2) 2(Rare·종류1·산업2) 3(Rare·종류1·산업1) 4(Epic·종류3)
        // Epic 4 → Rare 중 종류1(산업1: 3, 산업2: 2) → 종류2: 1
        var entries = new[]
        {
            new StorageSortEntry(1, false, "", 1, StorageSort.EquipTieBreak(3, 2, 1, 1101, 1)),
            new StorageSortEntry(2, false, "", 1, StorageSort.EquipTieBreak(3, 1, 2, 1102, 2)),
            new StorageSortEntry(3, false, "", 1, StorageSort.EquipTieBreak(3, 1, 1, 1103, 3)),
            new StorageSortEntry(4, false, "", 1, StorageSort.EquipTieBreak(4, 3, 1, 1104, 4)),
        };

        StorageSort.Order(entries, StorageSortKey.Rarity, ascending: false)
            .ShouldBe(new long[] { 4, 3, 2, 1 });
    }

    [Fact]
    public void 같은_캐릭터_TID는_개체_ID_순이다()
    {
        var entries = new[]
        {
            new StorageSortEntry(9, false, "", 1, StorageSort.CharacterTieBreak(2, 1002, 9)),
            new StorageSortEntry(7, false, "", 1, StorageSort.CharacterTieBreak(2, 1002, 7)),
        };

        StorageSort.Order(entries, StorageSortKey.Rarity, ascending: false)
            .ShouldBe(new long[] { 7, 9 });
    }
}
