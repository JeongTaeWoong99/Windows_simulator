namespace WSGameServer;

public static class Global
{
    private static ulong _seq = 1;

    public static ulong AllocKey64()
    {
        return Interlocked.Increment(ref _seq);
    }

    /// <summary>
    /// <b>채취 전역 속도 배수.</b> 2.0이면 모든 슬롯이 2배 빨리 캔다.
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
}

