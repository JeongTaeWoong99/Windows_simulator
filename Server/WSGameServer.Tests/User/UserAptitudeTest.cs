using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 적성 포인트 찍기의 <b>조립부</b> 검증 — 검증 → 저장 → 정산·속도 갱신 → 응답 순서 (캐릭터 기획 5.4).
/// 여기가 어긋나면 저장 없이 적성만 오르거나(재접속 시 증발), 정산 전 구간에 새 속도가 소급된다.
/// </summary>
public class UserAptitudeTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    /// <summary>시작 캐릭터 — 전 산업 기본 1, 상한 2.</summary>
    private const int StarterTid = 1001;

    private const long CharacterId = 500;

    public UserAptitudeTest() => GameTableFixture.EnsureLoaded();

    // Lv10·Lv20에 1포인트씩. 실제 곡선 대신 짧은 곡선을 넣어 포인트 수를 고정한다.
    private static IEnumerable<CharacterLevelTableRow> Curve()
    {
        for (var lv = 1; lv <= 20; lv++)
        {
            yield return new CharacterLevelTableRow { CharacterLevelTID = lv, RequiredExp = lv == 1 ? 0 : 10, AptitudePoint = lv % 10 == 0 ? 1 : 0 };
        }
    }

    private static (User User, TestUserBuilder B) UserWith(int level, int fishingBonus = 0)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Growth.Load(Curve());
        var user = b.Build();

        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = StarterTid, level = level, exp = 0, fishing_bonus = fishingBonus },
        });

        return (user, b);
    }

    private static AptitudeInfo AptitudeOf(CharacterInfo info, EIndustryType industry)
        => info.Aptitudes.Single(a => a.Industry == industry);

    [Fact]
    public void 포인트를_찍으면_보너스를_저장하고_갱신된_캐릭터를_응답한다()
    {
        var (user, b) = UserWith(level: 10);

        user.RaiseAptitude(CharacterId, IndustryType.Fishing, Base);

        var saved = b.DB.PostedOf<SaveCharacterAptitudeRepository>().ShouldHaveSingleItem();
        saved.CharacterId.ShouldBe(CharacterId);
        saved.Bonus.Fishing.ShouldBe(1);

        var response = b.Channel.SentOf<S_AptitudeUpResponse>().ShouldHaveSingleItem();
        response.Result.ShouldBe(EResultCode.Ok);
        var fishing = AptitudeOf(response.Character!, EIndustryType.Fishing);
        fishing.Value.ShouldBe((byte)2);
        fishing.Cap.ShouldBe((byte)2);
        response.Character!.AptitudePoints.ShouldBe(0);
    }

    [Fact]
    public void 배치된_슬롯의_속도가_바로_바뀐다()
    {
        var (user, b) = UserWith(level: 10);
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });

        user.RaiseAptitude(CharacterId, IndustryType.Fishing, Base);

        // 적성 2 = 1250천분율(WorkSpeedTable) × 전역 배수 6.0 = 7500. 적성 1이면 6000이다.
        user.WorkStation.TryGet(0, out var slot).ShouldBeTrue();
        slot.CurrentWorkSpeed.ShouldBe(7500);
        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem().Slot!.CurrentWorkSpeed.ShouldBe(7500);
    }

    [Fact]
    public void 미보유_캐릭터는_거절한다()
    {
        var (user, b) = UserWith(level: 10);

        user.RaiseAptitude(characterId: 999, IndustryType.Fishing, Base);

        b.Channel.SentOf<S_AptitudeUpResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.CharacterNotOwned);
        b.DB.PostedOf<SaveCharacterAptitudeRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 포인트가_없으면_거절하고_아무것도_저장하지_않는다()
    {
        var (user, b) = UserWith(level: 9);

        user.RaiseAptitude(CharacterId, IndustryType.Fishing, Base);

        b.Channel.SentOf<S_AptitudeUpResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NoAptitudePoint);
        b.DB.PostedOf<SaveCharacterAptitudeRepository>().ShouldBeEmpty();
        user.TryGetCharacter(CharacterId, out var character).ShouldBeTrue();
        character.GetAptitude(IndustryType.Fishing).ShouldBe(1);
    }

    [Fact]
    public void 상한에_닿으면_거절한다()
    {
        // Lv20 = 2포인트지만 시작 캐릭터의 낚시 상한은 2라 한 번만 찍힌다.
        var (user, b) = UserWith(level: 20);

        user.RaiseAptitude(CharacterId, IndustryType.Fishing, Base);
        user.RaiseAptitude(CharacterId, IndustryType.Fishing, Base);

        var results = b.Channel.SentOf<S_AptitudeUpResponse>().Select(r => r.Result).ToList();
        results.ShouldBe(new[] { EResultCode.Ok, EResultCode.AptitudeAtCap });
        b.DB.PostedOf<SaveCharacterAptitudeRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 로그인_적재_때_찍은_보너스가_실효_적성에_반영된다()
    {
        // DB에 낚시 보너스 1이 있는 Lv10 캐릭터 — 적성 2, 남은 포인트 0으로 내려가야 한다.
        var (user, b) = UserWith(level: 10, fishingBonus: 1);

        user.SendCharacters();

        var info = b.Channel.SentOf<S_CharacterListResponse>().ShouldHaveSingleItem().Characters!.Single();
        AptitudeOf(info, EIndustryType.Fishing).Value.ShouldBe((byte)2);
        info.AptitudePoints.ShouldBe(0);
    }
}
