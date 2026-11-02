using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 전 캐릭터 1장씩 지급 치트(<c>GiveAllCharacters</c> · T-190 · 이슈 #102).
/// CharacterTable 전 종류가 <b>창고 빈 칸 → 인벤토리 빈 칸 → 우편</b> 순서로 빠짐없이 한 장씩 가야 한다 —
/// 틀리면 칸이 넘쳐 클라 격자에 안 보이거나, 몇 종이 사라지거나 두 번 들어온다.
/// </summary>
public class UserCheatGiveAllCharactersTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int AllRounderTid = 1001;

    public UserCheatGiveAllCharactersTest() => GameTableFixture.EnsureLoaded();

    private static int[] AllTids => GameTable.CharacterTable.All.Select(r => r.CharacterTID).ToArray();

    /// <summary>창고에 <paramref name="warehouseFree"/>칸, 인벤토리에 <paramref name="inventoryFree"/>칸이 남은 관리자.</summary>
    private static (User User, TestUserBuilder B) Admin(int warehouseFree, int inventoryFree)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.AdminLevel = 1;

        var rows = new List<CharacterRow>();
        for (var i = 0; i < User.WarehouseCapacity - warehouseFree; i++)
        {
            rows.Add(new CharacterRow { character_id = 10_000 + i, character_tid = AllRounderTid, level = 1, container = (int)EContainer.Warehouse, slot = i });
        }

        for (var i = 0; i < User.StorageCapacity - inventoryFree; i++)
        {
            rows.Add(new CharacterRow { character_id = 20_000 + i, character_tid = AllRounderTid, level = 1, container = (int)EContainer.Inventory, slot = i });
        }

        user.LoadCharacters(rows);
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static C_CheatRequest Req() => new() { Command = ECheatCommand.GiveAllCharacters };

    private static GrantCharacterRepository GrantTo(TestUserBuilder b, EContainer container)
        => b.DB.PostedOf<GrantCharacterRepository>().Where(g => g.Container == container).ShouldHaveSingleItem();

    [Fact]
    public void 창고_빈_칸부터_채우고_인벤토리_빈_칸_다음_남은_것은_우편으로_간다()
    {
        // 창고 2칸 · 인벤토리 3칸 → 표 순서 앞 2종은 창고, 다음 3종은 인벤토리, 나머지는 우편 한 통
        var (user, b) = Admin(warehouseFree: 2, inventoryFree: 3);

        user.ExecuteCheat(Req(), Base);

        GrantTo(b, EContainer.Warehouse).CharacterTids.ShouldBe(AllTids.Take(2));
        GrantTo(b, EContainer.Inventory).CharacterTids.ShouldBe(AllTids.Skip(2).Take(3));
        b.DB.PostedOf<StoreOverflowMailRepository>().ShouldHaveSingleItem().Attachment.CharacterTids.ShouldBe(AllTids.Skip(5));
    }

    [Fact]
    public void 응답에_창고_인벤토리_우편_장수를_나눠_적는다()
    {
        var (user, b) = Admin(warehouseFree: 2, inventoryFree: 3);

        user.ExecuteCheat(Req(), Base);

        var reply = b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem();
        reply.Result.ShouldBe(EResultCode.Ok);
        reply.Message.ShouldBe($"창고 2 · 인벤토리 3 · 우편 {AllTids.Length - 5}");
    }

    [Fact]
    public void 칸이_남으면_우편을_보내지_않는다()
    {
        var (user, b) = Admin(warehouseFree: 1, inventoryFree: User.StorageCapacity);

        user.ExecuteCheat(Req(), Base);

        b.DB.PostedOf<StoreOverflowMailRepository>().ShouldBeEmpty();
        GrantTo(b, EContainer.Inventory).CharacterTids.ShouldBe(AllTids.Skip(1));
    }

    [Fact]
    public void 창고가_가득이면_창고_지급은_나가지_않는다()
    {
        var (user, b) = Admin(warehouseFree: 0, inventoryFree: User.StorageCapacity);

        user.ExecuteCheat(Req(), Base);

        b.DB.PostedOf<GrantCharacterRepository>().ShouldHaveSingleItem().Container.ShouldBe(EContainer.Inventory);
    }

    [Fact]
    public void 지급_대기_중인_창고_칸은_다음_지급이_채우지_않는다()
    {
        // 첫 번째가 창고 2칸을 예약했다 — PK가 오기 전에 다시 눌러도 창고로 더 가지 않는다
        var (user, b) = Admin(warehouseFree: 2, inventoryFree: User.StorageCapacity);
        user.ExecuteCheat(Req(), Base);

        user.ExecuteCheat(Req(), Base);

        b.Channel.SentOf<S_CheatResponse>().Last().Message.ShouldStartWith("창고 0 · ");
    }

    [Fact]
    public void 창고로_받은_캐릭터는_창고_칸에_놓이고_목록이_다시_나간다()
    {
        var (user, b) = Admin(warehouseFree: 2, inventoryFree: 0);
        user.ExecuteCheat(Req(), Base);
        var grant = GrantTo(b, EContainer.Warehouse);

        user.OnCharactersGranted(new[] { (Id: 900L, Tid: grant.CharacterTids[0], Slot: grant.Slots[0]) }, EContainer.Warehouse);

        var info = b.Channel.SentOf<S_CharacterListResponse>().ShouldHaveSingleItem().Characters.Single(c => c.CharacterId == 900);
        info.Container.ShouldBe(EContainer.Warehouse);
        info.Slot.ShouldBe(User.WarehouseCapacity - 2);
    }
}
