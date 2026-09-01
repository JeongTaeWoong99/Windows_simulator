using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    // 잔액은 ref로 넘겨 공용 검사(TrySpend)를 태우므로 자동 프로퍼티가 아니라 필드로 둔다.
    //
    // long이다. 인벤토리 수량은 int지만 재화는 다르다 — 거래소가 붙으면 누적 골드가
    // int 상한(약 21억)을 넘고, 넘치는 순간 조용히 음수가 되어 되돌릴 수 없다.
    private long _gold;
    private long _dia;

    /// <summary>무료 재화. <b>캐릭터가 아니라 유저 소유다.</b></summary>
    public long Gold => _gold;

    /// <summary>유료 재화. 현금 결제로 들어오므로 지급 경로를 함부로 늘리지 않는다.</summary>
    public long Dia => _dia;

    /// <summary>DB에서 읽은 재화를 적재한다(로그인 시 1회). 한 번도 번 적이 없으면 행이 없고 그건 0이다.</summary>
    private void LoadCurrency(CurrencyRow? row)
    {
        _gold = row?.gold ?? 0L;
        _dia  = row?.dia  ?? 0L;
    }

    public bool CanAffordGold(long gold) => _gold >= gold;

    public bool CanAffordDia(long dia) => _dia >= dia;

    /// <summary>보유 재화를 보낸다(로그인 직후)</summary>
    public void SendCurrency()
    {
        Send(new S_CurrencyResponse { Gold = _gold, Dia = _dia });
    }

    /// <summary>골드를 지급하고 저장·통지한다</summary>
    /// <returns>변경 후 보유량.</returns>
    public long GainGold(long gold)
    {
        _gold = Add(_gold, gold);

        SaveAndNotifyCurrency();
        return _gold;
    }

    /// <summary>다이아를 지급하고 저장·통지한다</summary>
    /// <returns>변경 후 보유량.</returns>
    public long GainDia(long dia)
    {
        _dia = Add(_dia, dia);

        SaveAndNotifyCurrency();
        return _dia;
    }

    /// <summary>골드를 차감하고 저장·통지한다. <b>잔액이 모자라면 아무것도 바꾸지 않는다.</b></summary>
    /// <returns>차감에 성공했으면 true.</returns>
    public bool TrySpendGold(long gold) => TrySpend(ref _gold, gold);

    /// <summary>다이아를 차감하고 저장·통지한다. <b>잔액이 모자라면 아무것도 바꾸지 않는다.</b></summary>
    /// <returns>차감에 성공했으면 true.</returns>
    public bool TrySpendDia(long dia) => TrySpend(ref _dia, dia);

    /// <summary>
    /// 지급액은 양수여야 한다 — 음수를 허용하면 "지급" 경로로 차감이 일어나 잔액 검사를 우회한다.
    /// </summary>
    private static long Add(long balance, long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        // 오버플로는 음수로 뒤집혀 잔액이 통째로 사라진다. 터뜨리는 편이 낫다.
        return checked(balance + amount);
    }

    // 모자라면 손대지 않고 false다 — 부분 차감하면 호출자가 실패를 알아채기 어렵다.
    private bool TrySpend(ref long balance, long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        if (balance < amount)
        {
            return false;
        }

        balance -= amount;

        SaveAndNotifyCurrency();
        return true;
    }

    // 확정된 잔액을 저장하고 통지한다. 저장·통지가 갈라지지 않게 변경 경로는 전부 여기를 지난다.
    private void SaveAndNotifyCurrency()
    {
        PostDBTask(new SaveCurrencyRepository(this, _gold, _dia));

        Send(new S_CurrencyResponse { Gold = _gold, Dia = _dia });
    }
}
