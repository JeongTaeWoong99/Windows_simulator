namespace WSGameServer;

// 작업속도 보정 누산기. 속도 = 적성기본값 × (1 + Σ가산) × Π승산. 새 보정은 기본이 가산, 승산은 의도적으로 고를 때만.
// 값 타입이며 실제 계산은 Resolve에서 한 번뿐이다 → Server/docs/채취-정산.md 5장
public readonly struct WorkSpeed
{
    /// <summary>적성 기본 작업속도(천분율)로 누산을 시작한다.</summary>
    public static WorkSpeed From(int baseWorkSpeed) => new(baseWorkSpeed, 0, 0);

    private WorkSpeed(int baseWorkSpeed, int addPermille, double mulRate)
    {
        _baseWorkSpeed = baseWorkSpeed;
        _addPermille  = addPermille;
        _mulRate      = mulRate;
    }

    private readonly int _baseWorkSpeed;

    /// <summary>가산 보정의 <b>합</b>(천분율). <c>+25%</c> → <c>250</c>. 기본값에 대한 비율이다.</summary>
    private readonly int _addPermille;

    /// <summary>승산 보정의 곱. 0은 "승산 없음(×1.0)" — Multiply가 0 이하를 거부하므로 실제 배수로 들어올 일이 없다.</summary>
    private readonly double _mulRate;

    /// <summary>가산 보정(천분율). 250이면 기본값의 +25%. 특성·부스트·장비가 여기로 오고, 감소는 음수.</summary>
    public WorkSpeed Add(int ratePermille)
        => new(_baseWorkSpeed, _addPermille + ratePermille, _mulRate);

    /// <summary><b>승산</b> 보정을 곱한다(천분율). <c>1500</c>이면 결과 전체에 <c>×1.5</c>.</summary>
    public WorkSpeed Multiply(int ratePermille)
        => Multiply((double)ratePermille / WorkStationSlot.WorkSpeedScale);

    /// <summary>승산 보정(배수). 0 이하는 무시한다 — 보정 하나가 슬롯을 조용히 멈춰 세우면 안 된다(MinWorkSpeed 참조).</summary>
    public WorkSpeed Multiply(double rate)
    {
        if (rate <= 0)
        {
            return this;
        }

        return new(_baseWorkSpeed, _addPermille, _mulRate == 0 ? rate : _mulRate * rate);
    }

    /// <summary>보정을 적용해 최종 속도(천분율)를 확정한다. 실수 연산은 여기서 한 번만 일어나고 int로 끝난다.</summary>
    public int Resolve()
    {
        // 가산은 (1 + Σ가산)을 천분율 그대로 곱한 뒤 나눈다 — 먼저 곱해야 1 미만이 잘려 나가지 않는다.
        // 감소 보정이 겹쳐 -100%를 넘어가면 0으로 막는다(음수 속도는 시간을 거꾸로 돌리는 셈이다).
        var addFactor = Math.Max(0, WorkStationSlot.WorkSpeedScale + _addPermille);
        var speed     = (long)_baseWorkSpeed * addFactor / WorkStationSlot.WorkSpeedScale;

        // 승산은 소수 배수라 마지막에 한 번만 곱한다.
        if (_mulRate != 0)
        {
            speed = (long)(speed * _mulRate);
        }

        return (int)Math.Clamp(speed, WorkStationSlot.MinWorkSpeed, int.MaxValue);
    }
}
