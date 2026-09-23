namespace WSGameServer;

/// <summary>
/// 요청 빈도 제한. 가득 찬 상태에서 시작해 한 번에 <c>capacity</c>번까지 허용하고, <c>refill</c>마다 하나씩 회복한다.
/// 시각은 인자로 받는다 — 로직 스레드 전용이라 락이 없다.
/// </summary>
public sealed class TokenBucket
{
    private readonly int      _capacity;
    private readonly TimeSpan _refill;

    private int      _tokens;
    private DateTime _lastRefill;

    public TokenBucket(int capacity, TimeSpan refill, DateTime now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity   = capacity;
        _refill     = refill;
        _tokens     = capacity;
        _lastRefill = now;
    }

    public bool TryTake(DateTime now)
    {
        Refill(now);

        if (_tokens == 0)
        {
            return false;
        }

        _tokens--;
        return true;
    }

    // 지난 간격 수만큼 채운다. 남는 조각 시간은 버리지 않고 다음 회복으로 넘긴다.
    private void Refill(DateTime now)
    {
        if (now <= _lastRefill)
        {
            return;
        }

        var earned = (int)Math.Min(_capacity, (now - _lastRefill).Ticks / _refill.Ticks);
        if (earned == 0)
        {
            return;
        }

        _tokens = Math.Min(_capacity, _tokens + earned);
        _lastRefill = _tokens == _capacity ? now : _lastRefill + _refill * earned;
    }
}
