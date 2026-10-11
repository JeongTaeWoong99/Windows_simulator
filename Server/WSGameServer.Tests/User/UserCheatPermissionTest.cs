using MikaProtocol;

namespace WSGameServer;

// User.CheatAdminLevel은 전역 설정이라, 바꾸는 테스트는 다른 테스트와 동시에 돌지 않게 따로 묶는다.
[CollectionDefinition(nameof(CheatLevelCollection), DisableParallelization = true)]
public class CheatLevelCollection;

/// <summary>
/// 원격(<c>Game:CheatAdminLevel</c> = 1)에서 치트 권한이 막히는지 지킨다.
/// 틀리면 친구들이 원격 서버에서 골드·아이템을 마음대로 만든다.
/// </summary>
[Collection(nameof(CheatLevelCollection))]
public class UserCheatPermissionTest : IDisposable
{
    private readonly int _previous = User.CheatAdminLevel;

    public UserCheatPermissionTest()
    {
        GameTableFixture.EnsureLoaded();
        User.CheatAdminLevel = 1;
    }

    public void Dispose()
    {
        User.CheatAdminLevel = _previous;
    }

    [Fact]
    public void 권한이_없으면_NoPermission으로_거절한다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.AdminLevel = 0;

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.GiveGold, Arg1 = 1000 }, TestUserBuilder.Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.NoPermission);
    }

    [Fact]
    public void 권한이_없으면_아무것도_바꾸지_않는다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.AdminLevel = 0;

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.GiveGold, Arg1 = 1000 }, TestUserBuilder.Base);

        user.Gold.ShouldBe(0);
    }

    [Fact]
    public void 관리자는_원격에서도_치트를_쓴다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.AdminLevel = 1;

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.GiveGold, Arg1 = 1000 }, TestUserBuilder.Base);

        user.Gold.ShouldBe(1000);
    }
}
