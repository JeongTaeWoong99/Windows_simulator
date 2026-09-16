using Microsoft.Data.Sqlite;
using MikaNetwork.Server;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 운영 DB 큐(<see cref="DBManager"/>)가 DB 스레드의 실패를 <b>로직 스레드로 되돌리는지</b> 검증한다.
/// 되돌리지 않으면 Apply도 OnFailed도 불리지 않아 로그인이 무응답으로 멈춘다.
/// </summary>
public class DBManagerTest
{
    private static readonly object Gate = new();
    private static bool _started;

    // DBExecutor는 프로세스 전역 싱글턴이라 한 번만 시작한다.
    private static void EnsureExecutorStarted()
    {
        lock (Gate)
        {
            if (_started)
            {
                return;
            }

            DBExecutor.Instance.Start(1);
            _started = true;
        }
    }

    [Fact]
    public async Task SQL이_실패하면_OnFailed가_로직_스레드로_돌아온다()
    {
        EnsureExecutorStarted();

        var executor = new FakeLogicExecutor();
        var manager  = new DBManager(executor);

        // 테이블이 하나도 없는 빈 DB — LoginRepository의 첫 SELECT부터 실패한다.
        manager.Initialize(() => new SqliteConnection("Data Source=:memory:"));

        var builder = new TestUserBuilder();
        var user    = builder.Build();
        manager.Post(new LoginRepository(user));

        await WaitForPostAsync(executor);
        executor.Drain();

        builder.Channel.SentOf<S_LoginResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.DbError);
        user.IsDestroyed.ShouldBeTrue();
    }

    // DB 스레드가 결과를 로직 큐에 넣을 때까지 기다린다. 5초면 :memory: 실패 한 번에는 넉넉하다.
    private static async Task WaitForPostAsync(FakeLogicExecutor executor)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (executor.Posted.Count == 0)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("DB 스레드가 결과를 되돌리지 않았다");
            }

            await Task.Delay(10);
        }
    }
}
