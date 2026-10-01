namespace WSGameServer;

public static class Global
{
    private static ulong _seq = 1;

    public static ulong AllocKey64()
    {
        return Interlocked.Increment(ref _seq);
    }

    // 채취 전역 속도 배수. 기획 수치는 그대로 두고 서버 전체를 이 값 하나로 당긴다 → Server/docs/채취-정산.md 5장
    // 확인용으로 올리면 시작 시 경고가 찍힌다 — 그대로 배포하면 재화 산출량이 그 배수만큼 커진다.
    public const double GatherSpeedMultiplier = 1.0;

    // 무응답 판정 시간. 🔒 클라 핑 주기(PingManager.PingIntervalSeconds) ≤ 이 값 ÷ 3 — 둘 중 하나만 고치지 않는다(이슈 #19).
    // 채취 기준 주기(30초)보다 짧아야 부당 적립이 판정 1회를 못 채운다 → Server/docs/세션-감시.md
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromSeconds(15);
}

