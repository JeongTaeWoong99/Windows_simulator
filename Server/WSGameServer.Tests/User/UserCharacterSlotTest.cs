using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 캐릭터 칸(T-058) — 지급·우편 수령은 PK가 확정되기 전에 칸을 예약한다. 예약하지 않으면 응답 전에 두 번 뽑은
/// 캐릭터가 같은 칸을 잡아 하나가 화면에서 가려진다(장비가 같은 이유로 예약한다).
/// </summary>
public class UserCharacterSlotTest
{
    private const int CharacterTid = 1002;

    public UserCharacterSlotTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWith(params (long Id, int Slot)[] characters)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.LoadCharacters(characters
            .Select(c => new CharacterRow { character_id = c.Id, character_tid = CharacterTid, level = 1, exp = 0, slot = c.Slot })
            .ToList());
        return (user, b);
    }

    [Fact]
    public void 뽑은_캐릭터는_첫_빈_칸부터_예약된다()
    {
        var (user, b) = UserWith((1, 0), (2, 2));

        user.GrantGachaCharacters(new[] { CharacterTid, CharacterTid });

        // 0·2가 찼다 → 1, 3
        b.DB.PostedOf<GrantCharacterRepository>().Single().Slots.ShouldBe(new[] { 1, 3 });
    }

    [Fact]
    public void 응답_전_두_번째_뽑기는_예약된_칸을_피한다()
    {
        var (user, b) = UserWith((1, 0));

        user.GrantGachaCharacters(new[] { CharacterTid });
        user.GrantGachaCharacters(new[] { CharacterTid });

        // 첫 뽑기가 1을 예약 → 두 번째는 2
        b.DB.PostedOf<GrantCharacterRepository>().Last().Slots.ShouldBe(new[] { 2 });
    }

    [Fact]
    public void 지급이_끝나면_예약한_칸으로_목록에_실린다()
    {
        var (user, b) = UserWith((1, 0));
        user.GrantGachaCharacters(new[] { CharacterTid });

        user.OnGachaCharactersGranted(new[] { (Id: 50L, Tid: CharacterTid, Slot: 1) });

        b.Channel.SentOf<S_CharacterListResponse>().Last()
            .Characters!.Single(c => c.CharacterId == 50).Slot.ShouldBe(1);
    }

    [Fact]
    public void 로그인_적재한_칸이_목록에_실린다()
    {
        var (user, b) = UserWith((1, 4));

        user.SendCharacters();

        b.Channel.SentOf<S_CharacterListResponse>().Last().Characters!.Single().Slot.ShouldBe(4);
    }

    [Fact]
    public void 우편으로_받은_캐릭터는_예약한_첫_빈_칸에_들어간다()
    {
        var (user, b) = UserWith((1, 0), (2, 1));

        user.OnMailCharacterUnlocked(new MailCharacter(77, CharacterTid, 1, 0), slot: 2, unlocked: true);

        b.Channel.SentOf<S_CharacterSyncResponse>().Last().Character!.Slot.ShouldBe(2);
    }
}
