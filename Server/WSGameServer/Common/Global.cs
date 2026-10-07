namespace WSGameServer;

public static class Global
{
    private static ulong _seq = 1;

    public static ulong AllocKey64()
    {
        return Interlocked.Increment(ref _seq);
    }

    // 무응답 판정 시간. 🔒 클라 핑 주기(PingManager.PingIntervalSeconds) ≤ 이 값 ÷ 3 — 둘 중 하나만 고치지 않는다(이슈 #19).
    // 채취 기준 주기(30초)보다 짧아야 부당 적립이 판정 1회를 못 채운다 → Server/docs/세션-감시.md
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromSeconds(15);
}

