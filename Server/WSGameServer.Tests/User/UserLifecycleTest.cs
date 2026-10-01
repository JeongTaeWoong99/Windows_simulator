using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 로그인 생명주기 Create → Login → Destroy (T-020).
/// 로그인 패킷 순서는 클라가 그리는 순서다 — 바뀌어도 컴파일은 통과하고 클라만 깨진다.
/// 접속 목록 등록·해제는 User가 아니라 매니저가 이벤트를 듣고 한다 — User가 전역을 만지면 테스트끼리 샌다(T-089).
/// 종료 정산(지급하되 푸시 없음)은 UserWorkStationTest, 무응답 판정은 SessionIdleSweepTest가 본다.
/// </summary>
public class UserLifecycleTest
{
    private static readonly DateTime Now = TestUserBuilder.Base;

    [Fact]
    public void 생성하면_계정_조회를_예약한다()
    {
        var b    = new TestUserBuilder().WithInlineExecutor();
        var user = b.Build();

        user.Create().ShouldBeTrue();

        b.DB.PostedOf<AccountRepository>().ShouldHaveSingleItem();
    }

    [Fact]
    public void 두_번_생성해도_계정_조회는_한_번이다()
    {
        var b    = new TestUserBuilder().WithInlineExecutor();
        var user = b.Build();

        user.Create();
        user.Create().ShouldBeFalse();

        b.DB.PostedOf<AccountRepository>().Count.ShouldBe(1);
    }

    [Fact]
    public void 로그인_패킷은_클라가_그리는_순서로_나간다()
    {
        // 캐릭터 → 장비 → 해금 → 슬롯: 장비는 착용 캐릭터를, 슬롯은 캐릭터·장비·잠긴 칸을 전제한다.
        var b    = new TestUserBuilder();
        var user = b.Build();

        user.Login(Now);

        var kinds = b.Channel.Sent.Select(p => p.GetType()).ToList();
        var order = new[]
        {
            typeof(S_LoginResponse),
            typeof(S_CharacterListResponse),
            typeof(S_EquipListResponse),
            typeof(S_UnlockListResponse),
            typeof(S_WorkStationSlotsResponse),
        }.Select(t => kinds.IndexOf(t)).ToList();

        order.ShouldAllBe(i => i >= 0);
        order.ShouldBeInOrder();
        kinds[0].ShouldBe(typeof(S_LoginResponse));
    }

    [Fact]
    public void 로그인하면_로그인_이벤트가_한_번_울린다()
    {
        var b       = new TestUserBuilder();
        var user    = b.Build();
        var raised  = new List<User>();
        user.LoggedIn += raised.Add;

        user.Login(Now);

        raised.ShouldBe(new[] { user });
        user.IsLoggedIn.ShouldBeTrue();
    }

    [Fact]
    public void 통로가_끊겼으면_로그인하지_않고_정리로_빠진다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        var raised = false;
        user.LoggedIn += _ => raised = true;
        b.Channel.IsConnected = false;

        user.Login(Now);

        raised.ShouldBeFalse();
        user.IsDestroyed.ShouldBeTrue();
        b.Channel.SentOf<S_LoginResponse>().ShouldBeEmpty();
    }

    [Fact]
    public void 정리하면_종료_이벤트가_한_번만_울린다()
    {
        // Destroy가 여러 경로(소켓 해제 · 유휴 스윕 · 중복 로그인)에서 겹쳐 와도 매니저는 한 번만 내린다.
        var b    = new TestUserBuilder().WithInlineExecutor();
        var user = b.Build();
        var left = 0;
        user.Left += _ => left++;

        user.Destroy();
        user.Destroy();

        left.ShouldBe(1);
    }

    [Fact]
    public void 매니저는_로그인에_올리고_종료에_내린다()
    {
        // 운영 배선(UserManager.CreateUser의 구독)과 같은 모양을 테스트 전용 매니저에 건다 — 전역을 쓰지 않는다.
        var b       = new TestUserBuilder().WithInlineExecutor();
        var user    = b.Build(uid: 7);
        var manager = new UserManager();
        user.LoggedIn += u => manager.JoinUser(u);
        user.Left     += u => manager.LeaveUser(u);

        user.Login(Now);
        manager.TryGetUserByUid(7, out var online).ShouldBeTrue();
        online.ShouldBeSameAs(user);

        user.Destroy();
        manager.TryGetUserByUid(7, out _).ShouldBeFalse();
    }
}
