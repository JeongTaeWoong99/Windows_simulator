namespace WSGameServer;

public static class Global
{
    private static ulong _seq = 1;

    public static ulong AllocKey64()
    {
        return Interlocked.Increment(ref _seq);
    }

    /// <summary>
    /// <b>채취 전역 속도 배수.</b> 2.0ㄸ이면 모든 슬롯이 2배 빨리 캔다.
    ///
    /// <para>
    /// 기획 수치(<c>WorkStationSlot.BaseCycleSeconds</c> = 30초, <c>WorkSpeedTable</c>)는 그대로 두고
    /// 서버 전체를 이 값 하나로 당긴다. 기획 데이터를 확인 편의로 고치면 엑셀이 진짜 밸런스인지
    /// 테스트용 값인지 구분이 사라진다.
    /// </para>
    ///
    /// <para>
    /// 현재 <b>6.0배</b> — 기준 속도(적성 1 = 1000천분율) 슬롯이 30초가 아니라 <b>5초</b>에 한 번 판정한다.
    /// ⚠️ 확인용 설정이므로 배포 전에 1.0으로 되돌린다(1.0이 아니면 시작 시 경고를 찍는다).
    /// </para>
    /// </summary>
    public const double GatherSpeedMultiplier = 6.0;

    /// <summary>
    /// <b>무응답 판정 시간.</b> 이만큼 아무 바이트도 못 받은 세션은 끊는다.
    ///
    /// <para>
    /// 클라이언트는 5초마다 <c>C_PingRequest</c>를 보낸다 — 세 번 연속 놓쳐야 끊기는 값이다.
    /// </para>
    ///
    /// <para>
    /// 🔒 <b>불변식 — 핑 주기 ≤ 판정 시간 ÷ 3.</b> 이 둘은 각각 정하는 값이 아니라 <b>한 쌍</b>이다.
    /// 한때 판정을 5초로 내려 클라 핑 주기(5초)와 같아진 적이 있는데, 여유가 0이 되어
    /// <b>프레임 오버슈트 몇 ms 때문에 멀쩡한 세션이 무작위로 끊겼다</b>(이슈 #19).
    /// 두 값 중 하나만 고치지 않는다. 클라 쪽 짝은 <c>PingManager.PingIntervalSeconds</c>다.
    /// </para>
    ///
    /// <para>
    /// <b>이 값이 곧 "공짜로 얻는 최대 오프라인 시간"이다.</b> 접속 판정이 재화 생성 조건이라
    /// (게임기획코어 P2), 끊긴 걸 서버가 모르는 구간만큼 부당 적립이 생긴다.
    /// 채취 기준 주기(<see cref="WorkStationSlot.BaseCycleSeconds"/> = 30초)보다 짧게 두어
    /// <b>부당 구간이 판정 1회를 못 채우게</b> 했다.
    /// ⚠️ 단 <see cref="GatherSpeedMultiplier"/>가 1.0이 아니면 실효 주기가 그만큼 짧아져
    /// 이 보장이 깨진다 — 배포 전에 배수를 되돌린다(일감 T-004).
    /// </para>
    /// </summary>
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromSeconds(15);
}

