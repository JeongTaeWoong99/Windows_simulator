using System.Net;
using System.Net.Sockets;
using MikaNetwork;

namespace WSGameServer;

/// <summary>
/// 클라 세션의 종료 경로 검증 (이슈 #19 B-1).
///
/// <para>
/// 소켓 루프를 Unity 메인 스레드에서 스레드풀로 옮기면 <b>수신 루프·송신 루프·StartAsync의 finally가
/// 서로 다른 스레드에서 동시에 종료 경로로 들어온다.</b> 여기서 놓치면 세션이 끝나지 못해 매달리거나,
/// Disconnected가 두 번 발화해 호스트의 세션 정리가 중복된다.
/// </para>
/// </summary>
public class ClientSessionShutdownTest
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task 원격이_강제로_끊으면_세션이_스스로_정리되고_끝난다()
    {
        var (local, remote) = ConnectedPair();
        var session = new MikaClientSession(local);

        var loop = Task.Run(() => session.StartAsync());
        await WaitUntilConnected(session);

        KillWithReset(remote);

        await loop.WaitAsync(Patience);
        session.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task 원격이_강제로_끊으면_Disconnected는_한_번만_발화한다()
    {
        var (local, remote) = ConnectedPair();
        var session = new MikaClientSession(local);

        int fired = 0;
        session.Disconnected += _ => Interlocked.Increment(ref fired);

        var loop = Task.Run(() => session.StartAsync());
        await WaitUntilConnected(session);

        KillWithReset(remote);

        await loop.WaitAsync(Patience);
        fired.ShouldBe(1);
    }

    [Fact]
    public async Task 여러_스레드가_동시에_Disconnect를_불러도_Disconnected는_한_번만_발화한다()
    {
        const int Racers = 8;

        var (local, remote) = ConnectedPair();
        using var _ = remote;
        var session = new MikaClientSession(local);

        int fired = 0;
        session.Disconnected += _ => Interlocked.Increment(ref fired);

        var loop = Task.Run(() => session.StartAsync());
        await WaitUntilConnected(session);

        // 검사-후-대입 가드는 여기서 둘 이상이 함께 통과한다. 경합을 노리는 테스트라
        // 실패가 확률적이다 — 결정적 근거는 위 두 테스트이고 이것은 보조다.
        var gate = new Barrier(Racers);
        await Task.WhenAll(Enumerable.Range(0, Racers).Select(_ => Task.Run(() =>
        {
            gate.SignalAndWait();
            session.Disconnect();
        })));

        await loop.WaitAsync(Patience);
        fired.ShouldBe(1);
    }

    /// <summary>루프백으로 실제 연결된 소켓 쌍을 만든다</summary>
    private static (Socket Local, Socket Remote) ConnectedPair()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var local = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        local.Connect(listener.LocalEndPoint!);

        return (local, listener.Accept());
    }

    // LingerOption(true, 0)을 걸고 닫으면 FIN이 아니라 RST가 나간다 — 상대의 ReceiveAsync는
    // 0을 반환하지 않고 SocketException으로 깨진다. 앱이 죽은 상황을 이렇게 흉내낸다.
    private static void KillWithReset(Socket remote)
    {
        remote.LingerState = new LingerOption(true, 0);
        remote.Dispose();
    }

    private static async Task WaitUntilConnected(MikaClientSession session)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!session.IsConnected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("세션이 연결 상태로 들어가지 않았다.");
            }

            await Task.Delay(10);
        }
    }
}
