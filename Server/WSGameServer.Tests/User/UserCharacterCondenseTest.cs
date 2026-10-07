using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 캐릭터 응축(기획 5.5). 개체는 넣은 재료의 <b>누적 수</b>만 기억하고 ★은 누적 기준으로 읽는다 —
/// 단계 제한 없이 넣은 만큼 오르고, 최고 ★을 넘는 몫도 소모된다. 재료 검증에 하나라도 걸리면 아무것도 지우지 않는다.
/// </summary>
public class UserCharacterCondenseTest
{
    public UserCharacterCondenseTest() => GameTableFixture.EnsureLoaded();

    private const int  Rambol = 1001;
    private const long Target = 500;
    private const long Other  = 900;   // 다른 캐릭터(TID 1002)

    // 테스트용 ★표 — 누적 기준 2 · 5 · 9, 가산 100 · 200 · 500‰
    private static CharacterStarTableRow[] Stars() => new[]
    {
        new CharacterStarTableRow { CharacterStarTID = 1, RequiredCount = 2, SpeedAddPermille = 100 },
        new CharacterStarTableRow { CharacterStarTID = 2, RequiredCount = 3, SpeedAddPermille = 200 },
        new CharacterStarTableRow { CharacterStarTID = 3, RequiredCount = 4, SpeedAddPermille = 500 },
    };

    // 대상 500 + 같은 캐릭터 재료 501~520 + 다른 캐릭터 900
    private static (User User, TestUserBuilder B) NewUser(int targetCondenseCount = 0)
    {
        var b = new TestUserBuilder();
        b.Stars.Load(Stars());

        var rows = new List<CharacterRow>
        {
            new() { character_id = Target, character_tid = Rambol, level = 7, exp = 3, condense_count = targetCondenseCount },
            new() { character_id = Other,  character_tid = 1002,   level = 1, exp = 0 },
        };
        for (var i = 1; i <= 20; i++)
        {
            rows.Add(new CharacterRow { character_id = Target + i, character_tid = Rambol, level = 1, exp = 0 });
        }

        var user = b.Build();
        user.LoadCharacters(rows);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static long[] Materials(int count) => Enumerable.Range(1, count).Select(i => Target + i).ToArray();

    private static S_CharacterCondenseResponse Last(TestUserBuilder b) => b.Channel.SentOf<S_CharacterCondenseResponse>().Last();

    [Fact]
    public void 재료를_넣으면_누적이_쌓이고_재료는_사라진다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(Target, Materials(1), TestUserBuilder.Base);

        // 1개 → 누적 1, 기준 2에 못 미쳐 ★0
        (Last(b).Result, Last(b).Character!.CondenseCount, Last(b).Character!.Star).ShouldBe((EResultCode.Ok, 1, 0));
        user.TryGetCharacter(Target + 1, out _).ShouldBeFalse();
    }

    [Fact]
    public void 기준을_여러_개_넘기면_한_번에_여러_단계_오른다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(Target, Materials(6), TestUserBuilder.Base);

        // 누적 6 → 기준 2·5를 넘어 ★2
        Last(b).Character!.Star.ShouldBe(2);
    }

    [Fact]
    public void 누적은_다음_요청으로_이어진다()
    {
        var (user, b) = NewUser(targetCondenseCount: 4);

        user.TryCondenseCharacter(Target, Materials(1), TestUserBuilder.Base);

        // 4 + 1 = 5 → 기준 5에 닿아 ★2
        (Last(b).Character!.CondenseCount, Last(b).Character!.Star).ShouldBe((5, 2));
    }

    [Fact]
    public void 최고_기준을_넘는_재료도_소모되고_누적은_최고에서_멈춘다()
    {
        var (user, b) = NewUser(targetCondenseCount: 8);

        user.TryCondenseCharacter(Target, Materials(3), TestUserBuilder.Base);

        // 8 + 3 = 11 → 최고 9에서 멈춘다. 넘친 2개도 사라진다
        (Last(b).Character!.CondenseCount, Last(b).Character!.Star).ShouldBe((9, 3));
        user.TryGetCharacter(Target + 3, out _).ShouldBeFalse();
    }

    [Fact]
    public void 최고_별이면_거절하고_재료를_지우지_않는다()
    {
        var (user, b) = NewUser(targetCondenseCount: 9);

        user.TryCondenseCharacter(Target, Materials(1), TestUserBuilder.Base);

        Last(b).Result.ShouldBe(EResultCode.CondenseMaxStar);
        user.TryGetCharacter(Target + 1, out _).ShouldBeTrue();
    }

    [Fact]
    public void 다른_캐릭터가_섞이면_아무것도_녹지_않는다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(Target, new[] { Target + 1, Other }, TestUserBuilder.Base);

        Last(b).Result.ShouldBe(EResultCode.CondenseTidMismatch);
        user.TryGetCharacter(Target + 1, out _).ShouldBeTrue();
        b.DB.PostedOf<CondenseCharacterRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 슬롯에_배치된_재료는_녹일_수_없다()
    {
        var (user, b) = NewUser();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, Target + 1, TestUserBuilder.Base) });

        user.TryCondenseCharacter(Target, Materials(1), TestUserBuilder.Base);

        Last(b).Result.ShouldBe(EResultCode.CondenseMaterialBusy);
    }

    [Fact]
    public void 빈_재료_중복_대상_자신은_잘못된_요청이다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(Target, Array.Empty<long>(), TestUserBuilder.Base);
        Last(b).Result.ShouldBe(EResultCode.CondenseInvalidRequest);

        // 중복을 받으면 한 개체로 두 번 쌓인다
        user.TryCondenseCharacter(Target, new[] { Target + 1, Target + 1 }, TestUserBuilder.Base);
        Last(b).Result.ShouldBe(EResultCode.CondenseInvalidRequest);

        user.TryCondenseCharacter(Target, new[] { Target }, TestUserBuilder.Base);
        Last(b).Result.ShouldBe(EResultCode.CondenseInvalidRequest);
    }

    [Fact]
    public void 미보유_대상과_재료는_거절된다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(999, Materials(1), TestUserBuilder.Base);
        Last(b).Result.ShouldBe(EResultCode.CharacterNotOwned);

        user.TryCondenseCharacter(Target, new[] { 999L }, TestUserBuilder.Base);
        Last(b).Result.ShouldBe(EResultCode.CharacterNotOwned);
    }

    [Fact]
    public void 재료_삭제와_누적을_한_번에_저장한다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(Target, Materials(2), TestUserBuilder.Base);

        var saved = b.DB.PostedOf<CondenseCharacterRepository>().Single();
        (saved.CharacterId, saved.CondenseCount).ShouldBe((Target, 2));
        saved.MaterialIds.ShouldBe(new[] { Target + 1, Target + 2 });
    }

    [Fact]
    public void 레벨과_경험치는_그대로다()
    {
        var (user, b) = NewUser();

        user.TryCondenseCharacter(Target, Materials(5), TestUserBuilder.Base);

        (Last(b).Character!.Level, Last(b).Character!.Exp).ShouldBe((7, 3));
    }

    [Fact]
    public void 별이_오르면_배치된_슬롯이_빨라진다()
    {
        var (user, b) = NewUser();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, Target, TestUserBuilder.Base) });
        user.RefreshWorkStationSpeed(TestUserBuilder.Base, notify: false);
        user.WorkStation.TryGet(0, out var slot);

        user.TryCondenseCharacter(Target, Materials(2), TestUserBuilder.Base);

        // 램볼 낚시 적성 1 = 1000‰. ★1 +100‰ → 1100 (레벨 곡선은 비어 있어 0 · 장비·특성 없음)
        slot!.CurrentWorkSpeed.ShouldBe(1100);
    }
}
