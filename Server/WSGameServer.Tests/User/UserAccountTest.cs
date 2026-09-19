using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 계정 레벨 검증 — <b>캐릭터가 얻은 경험치가 그대로 계정 경험치가 된다</b>, 레벨이 오르면 그 레벨의 특성 포인트를 준다.
/// 깨지면: 계정이 안 자라 특성 트리가 영영 안 열리거나, 포인트가 두 번 들어와 트리가 공짜가 된다.
/// </summary>
public class UserAccountTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;

    public UserAccountTest() => GameTableFixture.EnsureLoaded();

    /// <summary>
    /// 테스트 전용 곡선 — Lv2 = 10 · Lv3 = 20 · Lv4 = 30. 특성 포인트는 Lv2에서 1, Lv4에서 2.
    /// 캐릭터 곡선은 Lv2 = 1000으로 멀리 둬 캐릭터 레벨업이 끼어들지 않게 한다.
    /// </summary>
    private static (User User, TestUserBuilder B) NewUser()
    {
        var b = new TestUserBuilder();
        b.Accounts.Load(new[]
        {
            new AccountLevelTableRow { AccountLevelTID = 1, RequiredExp = 0,  TraitPoint = 0 },
            new AccountLevelTableRow { AccountLevelTID = 2, RequiredExp = 10, TraitPoint = 1 },
            new AccountLevelTableRow { AccountLevelTID = 3, RequiredExp = 20, TraitPoint = 0 },
            new AccountLevelTableRow { AccountLevelTID = 4, RequiredExp = 30, TraitPoint = 2 },
        });
        b.Growth.Load(new[]
        {
            new CharacterLevelTableRow { CharacterLevelTID = 1, RequiredExp = 0 },
            new CharacterLevelTableRow { CharacterLevelTID = 2, RequiredExp = 1000 },
        });

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        return (user, b);
    }

    private static Character Worker(User user)
    {
        user.TryGetCharacter(CharacterId, out var character).ShouldBeTrue();
        return character;
    }

    [Fact]
    public void 계정은_레벨1_경험치0_포인트0에서_시작한다()
    {
        var (user, _) = NewUser();

        user.AccountLevel.ShouldBe(1);
        user.AccountExp.ShouldBe(0);
        user.TraitPoint.ShouldBe(0);
    }

    [Fact]
    public void 캐릭터가_얻은_경험치가_그대로_계정_경험치로_쌓인다()
    {
        var (user, _) = NewUser();

        user.GrantCharacterExp(Worker(user), 7, notify: true);

        user.AccountLevel.ShouldBe(1);
        user.AccountExp.ShouldBe(7);
    }

    [Fact]
    public void 계정_레벨이_오르면_도달한_레벨의_특성_포인트를_준다()
    {
        // 10(Lv2) + 20(Lv3) + 30(Lv4) = 60 → Lv4, 남은 0. 포인트는 Lv2의 1 + Lv4의 2 = 3
        var (user, _) = NewUser();

        user.GrantCharacterExp(Worker(user), 60, notify: true);

        user.AccountLevel.ShouldBe(4);
        user.AccountExp.ShouldBe(0);
        user.TraitPoint.ShouldBe(3);
    }

    [Fact]
    public void 만렙이면_경험치가_더_쌓이지_않는다()
    {
        // 마지막 행(Lv4)이 곧 만렙이다 — 상한 위로 쌓인 값은 의미가 없어 버린다.
        var (user, _) = NewUser();

        user.GrantCharacterExp(Worker(user), 1000, notify: true);

        user.AccountLevel.ShouldBe(4);
        user.AccountExp.ShouldBe(0);
    }

    [Fact]
    public void 경험치가_오르면_저장하고_계정_레벨을_밀어_준다()
    {
        var (user, b) = NewUser();

        user.GrantCharacterExp(Worker(user), 15, notify: true);

        b.DB.PostedOf<SaveAccountRepository>().ShouldNotBeEmpty();
        var pushed = b.Channel.SentOf<S_AccountLevelResponse>().Last();
        pushed.Level.ShouldBe(2);
        pushed.Exp.ShouldBe(5);
        pushed.TraitPoint.ShouldBe(1);
    }

    [Fact]
    public void 로그인_데이터의_계정_행이_되살아난다()
    {
        var (user, _) = NewUser();

        user.LoadAccount(new AccountRow { level = 3, exp = 4, trait_point = 2 });

        user.AccountLevel.ShouldBe(3);
        user.AccountExp.ShouldBe(4);
        user.TraitPoint.ShouldBe(2);
    }

    [Fact]
    public void 계정_행이_없으면_레벨1로_본다()
    {
        // 재화처럼 가입 시 행을 만들지 않는다 — 한 번도 경험치를 번 적 없으면 행이 없다.
        var (user, _) = NewUser();

        user.LoadAccount(null);

        user.AccountLevel.ShouldBe(1);
        user.TraitPoint.ShouldBe(0);
    }
}
