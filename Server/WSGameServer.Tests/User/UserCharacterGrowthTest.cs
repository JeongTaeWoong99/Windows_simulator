using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 정산 → 캐릭터 경험치 → 저장 → 푸시가 엮이는 경로 검증 (캐릭터 기획 5.2 · T-003).
///
/// <see cref="CharacterGrowthTest"/>가 레벨업 산수를 잠갔다면, 여기는 그 위층이다 —
/// <b>판정 횟수가 어느 캐릭터의 경험치가 되고, 그 결과가 DB와 클라이언트에 어떻게 닿는가.</b>
/// </summary>
public class UserCharacterGrowthTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    public UserCharacterGrowthTest() => GameTableFixture.EnsureLoaded();

    /// <summary>낚시 Lv1 판정당 경험치 3, 곡선 Lv2 10 · Lv3 12 · Lv4 14(만렙)인 유저를 만든다.</summary>
    private static (User User, TestUserBuilder B) UserWith(int level = 1, int exp = 0)
    {
        var b = new TestUserBuilder().WithFishingDrops();

        b.Levels.Load(new[]
        {
            new IndustryLevelTableRow
            {
                IndustryLevelTID = 201, IndustryType = IndustryType.Fishing, Level = 1,
                Name = "개울", RequiredScore = 30_000, ExpPerJudge = 3,
            },
        });
        b.Growth.Load(new[]
        {
            new CharacterLevelTableRow { CharacterLevelTID = 1, RequiredExp = 0 },
            new CharacterLevelTableRow { CharacterLevelTID = 2, RequiredExp = 10 },
            new CharacterLevelTableRow { CharacterLevelTID = 3, RequiredExp = 12 },
            new CharacterLevelTableRow { CharacterLevelTID = 4, RequiredExp = 14 },
        });

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = level, exp = exp },
        });

        // 기본 속도(1.0배) = 30초에 1판정. 슬롯 비용은 기본 상수라 Levels의 RequiredScore와 무관하다.
        user.WorkStation.Load(new[]
        {
            new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base),
        });

        return (user, b);
    }

    [Fact]
    public void 판정한_만큼_배치된_캐릭터가_경험치를_번다()
    {
        var (user, _) = UserWith();

        // 2분 30초 = 판정 5회 × 3 = 15 → Lv2(10) 남은 5.
        user.SettleWorkStation(Base.AddSeconds(150));

        user.TryGetCharacter(CharacterId, out var character).ShouldBeTrue();
        character.Level.ShouldBe(2);
        character.Exp.ShouldBe(5);
    }

    [Fact]
    public void 경험치가_바뀌면_확정값을_저장한다()
    {
        var (user, b) = UserWith();

        user.SettleWorkStation(Base.AddSeconds(150));

        var saved = b.DB.PostedOf<SaveCharacterGrowthRepository>().ShouldHaveSingleItem();
        saved.CharacterId.ShouldBe(CharacterId);
        saved.Level.ShouldBe(2);
        saved.Exp.ShouldBe(5);
    }

    [Fact]
    public void 경험치가_바뀌면_그_캐릭터를_밀어_준다()
    {
        var (user, b) = UserWith();

        user.SettleWorkStation(Base.AddSeconds(150));

        var synced = b.Channel.SentOf<S_CharacterSyncResponse>().ShouldHaveSingleItem().Character!;
        synced.CharacterId.ShouldBe(CharacterId);
        synced.Level.ShouldBe(2);
        synced.Exp.ShouldBe(5);
    }

    [Fact]
    public void notify가_false면_저장은_하되_푸시하지_않는다()
    {
        // 접속 종료 정산 경로 — 세션이 닫혀 보낼 곳이 없을 뿐, 번 경험치는 버리지 않는다.
        var (user, b) = UserWith();

        user.SettleWorkStation(Base.AddSeconds(150), notify: false);

        b.DB.PostedOf<SaveCharacterGrowthRepository>().ShouldHaveSingleItem();
        b.Channel.SentOf<S_CharacterSyncResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 만렙_캐릭터는_저장도_푸시도_하지_않는다()
    {
        var (user, b) = UserWith(level: 4);

        user.SettleWorkStation(Base.AddSeconds(150));

        b.DB.PostedOf<SaveCharacterGrowthRepository>().ShouldBeEmpty();
        b.Channel.SentOf<S_CharacterSyncResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 산업_레벨_행이_없으면_경험치를_주지_않는다()
    {
        // 판정 비용과 같은 규약 — 데이터 한 줄 누락에 정산 전체가 죽지 않는다.
        var (user, b) = UserWith();
        b.Levels.Load(Array.Empty<IndustryLevelTableRow>());

        user.SettleWorkStation(Base.AddSeconds(150));

        user.TryGetCharacter(CharacterId, out var character).ShouldBeTrue();
        character.Exp.ShouldBe(0);
        b.DB.PostedOf<SaveCharacterGrowthRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 배치를_바꾸기_전_구간의_경험치는_이전_캐릭터에게_간다()
    {
        // 정산이 배치 변경보다 먼저라야 한다 — 뒤집히면 새 캐릭터가 남이 번 경험치를 받는다.
        var (user, b) = UserWith();
        const long otherId = 501;
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
            new CharacterRow { character_id = otherId,     character_tid = AllRounderTid, level = 1, exp = 0 },
        });

        user.AssignWorkStation(0, IndustryType.Fishing, otherId, Base.AddSeconds(150));

        user.TryGetCharacter(CharacterId, out var previous).ShouldBeTrue();
        user.TryGetCharacter(otherId, out var next).ShouldBeTrue();
        previous.Level.ShouldBe(2);
        next.Exp.ShouldBe(0);
        b.DB.PostedOf<SaveCharacterGrowthRepository>().ShouldHaveSingleItem().CharacterId.ShouldBe(CharacterId);
    }
}
