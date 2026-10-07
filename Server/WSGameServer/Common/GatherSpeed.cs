namespace WSGameServer;

/// <summary>
/// 채취 전역 속도 배수(천분율, 1000 = ×1.0). 기획 수치는 그대로 두고 서버 전체를 이 값 하나로 당긴다 → Server/docs/채취-정산.md 5장.
/// 치트(<c>SetGatherSpeed</c>)만 바꾼다. 서버 전체에 하나이고 <b>저장하지 않는다</b> — 재시작하면 ×1.0이다.
/// </summary>
public sealed class GatherSpeed
{
    public const int DefaultPermille = 1000;

    /// <summary>운영 인스턴스. 테스트는 자기 것을 만들어 넘긴다 — 병렬 테스트끼리 배수를 공유하지 않게.</summary>
    public static GatherSpeed Instance { get; } = new();

    private int _permille = DefaultPermille;

    public int Permille => Volatile.Read(ref _permille);

    public void Set(int permille)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(permille, 0);
        Volatile.Write(ref _permille, permille);
    }
}
