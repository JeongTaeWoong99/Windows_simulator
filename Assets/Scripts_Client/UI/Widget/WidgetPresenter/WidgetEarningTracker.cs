// 위젯 상단의 수익 집계 — 누적 골드 · 시간당 골드 · 측정 시간. 마지막 초기화 이후로 센다.
//
// ■ 골드는 실제로 번 돈이 아니라 추정치다
// 수확한 아이템을 즉시 판매가('BasePrice')로 환산한 합이다 → 'PlayerDataModel.GatherValueEarned'.
// 보유 골드의 증감으로 세지 않는다 — 판매·구매·가챠가 섞여 "캐릭터가 번 돈"이 아니게 된다.
//
// ■ 로그인해야 센다 · 앱을 켤 때마다 0에서 시작한다 (세션 기준 · 2026-10-09 사용자 결정)
// 로그인 전에는 캐릭터가 일하지 않으므로 시계도 멈춰 있다 — 'Begin'이 불리기 전까지 측정 시간은 0이다.
// 저장하지 않는다. 껐다 켜도 이어 가려면 시작 시각과 누적값을 따로 남겨야 한다.
//
// 시각은 부르는 쪽이 넘긴다 — 이 클래스는 시계를 직접 읽지 않아 계산만 남는다.
public class WidgetEarningTracker
{
    // 이보다 짧으면 시간당 값을 내지 않는다 — 첫 수확 하나로 숫자가 크게 튀어 보인다
    private const double MinRateSeconds = 60;

    private double _startTime;

    public bool IsStarted { get; private set; }
    public long TotalGold { get; private set; }

    // 집계를 시작한다 (로그인 성공). 이미 세는 중이면 아무것도 하지 않는다.
    public void Begin(double now)
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted  = true;
        TotalGold  = 0;
        _startTime = now;
    }

    // 수확 하나의 환산 골드를 더한다 (GatherValueEarned 구독)
    public void Add(long gold)
    {
        if (IsStarted)
        {
            TotalGold += gold;
        }
    }

    // 누적을 0으로 하고 지금부터 다시 센다 (초기화 버튼). 시작 전이면 그대로 멈춰 있다.
    public void Reset(double now)
    {
        TotalGold  = 0;
        _startTime = now;
    }

    // 마지막 초기화 이후 지난 초. 시작 전이면 0
    public double ElapsedSeconds(double now)
    {
        return IsStarted ? now - _startTime : 0;
    }

    // 시간당 골드. 측정 시간이 짧아 아직 믿을 수 없으면 false
    public bool TryGetPerHour(double now, out long perHour)
    {
        double elapsed = ElapsedSeconds(now);

        if (elapsed < MinRateSeconds)
        {
            perHour = 0;

            return false;
        }

        perHour = (long)(TotalGold * 3600 / elapsed);

        return true;
    }
}
