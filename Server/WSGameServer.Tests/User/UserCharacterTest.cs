using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 캐릭터 스냅샷 패킷 조립 검증.
///
/// <para>
/// 클라이언트는 적성을 <c>CharacterTable</c>에서 읽지 않고 <b>이 패킷으로만</b> 받는다
/// (캐릭터 기획 7.1). 여기가 새면 배치 화면의 필터가 통째로 틀린다 —
/// 특히 값이 0인 산업이 빠지면 "다루지 못함"과 "정보 없음"을 구분할 수 없다.
/// </para>
/// </summary>
public class UserCharacterTest
{
    /// <summary>낚시 적성 0. 0이 빠지지 않는지 보려면 0인 산업이 있는 캐릭터가 필요하다.</summary>
    private const int NoFishingTid = 1002;

    private const long CharacterId = 500;

    public UserCharacterTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWithCharacter(int characterTid)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = characterTid, level = 1, exp = 0 },
        });

        return (user, b);
    }

    private static List<AptitudeInfo> SentAptitudes(TestUserBuilder b)
    {
        return b.Channel.SentOf<S_CharacterListResponse>()
            .ShouldHaveSingleItem()
            .Characters!.ShouldHaveSingleItem()
            .Aptitudes;
    }

    [Fact]
    public void 적성은_1차_산업_5종이_순서대로_실린다()
    {
        var (user, b) = UserWithCharacter(NoFishingTid);

        user.SendCharacters();

        SentAptitudes(b).Select(a => a.Industry).ShouldBe(new[]
        {
            EIndustryType.Farming, EIndustryType.Fishing, EIndustryType.Mining, EIndustryType.Logging, EIndustryType.Hunting,
        });
    }

    [Fact]
    public void 적성이_0인_산업도_빠지지_않는다()
    {
        // 빠뜨리면 클라가 "적성 0(잠금)"과 "값을 못 받음"을 구분하지 못한다.
        GameTable.CharacterTable.TryGet(NoFishingTid, out var row).ShouldBeTrue();
        row.Fishing.ShouldBe(0);   // 이 테스트의 전제 — 엑셀이 바뀌면 여기가 먼저 빨개진다

        var (user, b) = UserWithCharacter(NoFishingTid);

        user.SendCharacters();

        SentAptitudes(b).ShouldContain(a => a.Industry == EIndustryType.Fishing && a.Value == 0);
    }

    [Fact]
    public void 적성_값은_캐릭터_테이블과_같다()
    {
        GameTable.CharacterTable.TryGet(NoFishingTid, out var row).ShouldBeTrue();

        var (user, b) = UserWithCharacter(NoFishingTid);

        user.SendCharacters();

        var aptitudes = SentAptitudes(b);
        aptitudes.First(a => a.Industry == EIndustryType.Farming).Value.ShouldBe((byte)row.Farming);
        aptitudes.First(a => a.Industry == EIndustryType.Mining).Value.ShouldBe((byte)row.Mining);
        aptitudes.First(a => a.Industry == EIndustryType.Logging).Value.ShouldBe((byte)row.Logging);
        aptitudes.First(a => a.Industry == EIndustryType.Hunting).Value.ShouldBe((byte)row.Hunting);
    }
}
