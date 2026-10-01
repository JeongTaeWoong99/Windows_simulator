using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 정렬 요청(T-058) — 격자 전체를 0부터 다시 매기고, 응답에 전부 싣고, 저장한다.
/// 순서 규칙 자체는 StorageSortTest가 지킨다. 여기서는 요청 경로와 거절을 본다.
/// </summary>
public class UserStorageSortTest
{
    private const int FishTid      = 10001;   // 붕어 — 표에 있다(등급 1 이상)
    private const int UnknownTid   = 900_001; // 표에 없다 → 등급 0, 맨 뒤
    private const int CharacterTid = 1002;

    public UserStorageSortTest() => GameTableFixture.EnsureLoaded();

    private static S_StorageSlotsResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_StorageSlotsResponse>().Last();

    [Fact]
    public void 테스트가_기대는_붕어는_등급이_있다()
    {
        // 아래 순서 기대값의 전제 — 엑셀이 바뀌면 원인 불명의 실패 대신 여기가 먼저 빨개진다.
        GameTable.ItemTable.TryGet(FishTid, out var fish).ShouldBeTrue();
        ((int)fish.GlobalRarity).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void 정렬하면_빈_칸_없이_0부터_다시_매긴다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadInventory(new[]
        {
            new InventoryRow { item_id = UnknownTid, count = 1, slot = 0 },
            new InventoryRow { item_id = FishTid,    count = 1, slot = 9 },
        });

        user.SortStorage(EContainer.Inventory, EStorageTab.Resource, EStorageSortKey.Rarity, EStorageSortOrder.Descending);

        // 붕어(등급 있음)가 먼저 0, 표에 없는 것(등급 0)이 1 — 칸 9의 구멍이 메워진다
        Last(b).Slots.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { ((long)FishTid, 0), ((long)UnknownTid, 1) });
    }

    [Fact]
    public void 정렬_결과를_저장한다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadInventory(new[] { new InventoryRow { item_id = FishTid, count = 1, slot = 3 } });

        user.SortStorage(EContainer.Inventory, EStorageTab.Resource, EStorageSortKey.Rarity, EStorageSortOrder.Descending);

        b.DB.PostedOf<SaveStorageSlotsRepository>().Single().Changes.ShouldBe(new[] { new SlotChange(FishTid, 0) });
    }

    [Fact]
    public void 캐릭터_탭의_수량순은_거절한다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.SortStorage(EContainer.Inventory, EStorageTab.Character, EStorageSortKey.Count, EStorageSortOrder.Descending);

        Last(b).Result.ShouldBe(EResultCode.InvalidStorageSortKey);
    }

    [Fact]
    public void 배치_중인_캐릭터는_정렬하면_맨_뒤로_간다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = CharacterTid, level = 1, slot = 0 },
            new CharacterRow { character_id = 2, character_tid = CharacterTid, level = 1, slot = 1 },
        });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Farming, 1, TestUserBuilder.Base, null) });

        user.SortStorage(EContainer.Inventory, EStorageTab.Character, EStorageSortKey.Rarity, EStorageSortOrder.Descending);

        // 같은 TID라 원래는 1·2 순이지만 1이 배치 중 → 2, 1
        Last(b).Slots.Select(s => s.Key).ShouldBe(new long[] { 2, 1 });
    }

    [Fact]
    public void 정렬은_지급_대기_칸을_비워_둔다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = 1, character_tid = CharacterTid, level = 1, slot = 0 },
            new CharacterRow { character_id = 2, character_tid = CharacterTid, level = 1, slot = 2 },
        });
        user.GrantGachaCharacters(new[] { CharacterTid });   // 칸 1 예약

        user.SortStorage(EContainer.Inventory, EStorageTab.Character, EStorageSortKey.Rarity, EStorageSortOrder.Descending);

        // 1·2 순서는 그대로, 예약된 칸 1을 건너뛰어 0·2
        Last(b).Slots.Select(s => (s.Key, s.Slot)).ShouldBe(new[] { (1L, 0), (2L, 2) });
    }
}
