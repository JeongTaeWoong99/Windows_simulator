namespace WSGameServer;

/// <summary>
/// DB 작업의 파티션 키가 <b>접속이 아니라 계정</b>에 붙어 있는지 검증한다.
///
/// <para>
/// DBExecutor는 Key가 같은 작업만 순서를 지킨다. 키가 SessionId면 재접속한 유저의
/// 마지막 쓰기(종료 정산)와 새 접속의 첫 읽기(로그인 적재)가 다른 채널에서 병렬로 돌아
/// 낡은 값을 읽고 그 값으로 덮어써 수확이 사라진다.
/// </para>
/// </summary>
public class RepositoryKeyTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private static User Connect(string pid, long sessionId)
    {
        return new User(new FakeClientChannel { SessionId = sessionId }, new FakeDBQueue(), new FakeLogicExecutor(),
                        pid, nickname: "테스터", loggedInAt: Base);
    }

    [Fact]
    public void 같은_계정은_접속이_바뀌어도_DB_작업_키가_같다()
    {
        // 밀려난 세션(sid 10)의 마지막 저장과 새 세션(sid 11)의 로그인 적재가 같은 채널을 타야 한다.
        var previous = Connect("same-pid", sessionId: 10);
        var current  = Connect("same-pid", sessionId: 11);

        var lastSave  = new SaveCurrencyRepository(previous, gold: 1, dia: 0);
        var firstLoad = new LoginRepository(current);

        lastSave.Key.ShouldBe(firstLoad.Key);
    }

    [Fact]
    public void 다른_계정은_DB_작업_키가_다르다()
    {
        // 같은 세션 번호를 재사용해도 계정이 다르면 직렬화할 이유가 없다.
        var a = Connect("pid-a", sessionId: 10);
        var b = Connect("pid-b", sessionId: 10);

        new AccountRepository(a).Key.ShouldNotBe(new AccountRepository(b).Key);
    }
}
