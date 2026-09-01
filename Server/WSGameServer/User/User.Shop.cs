using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 아이템을 팔고 대금을 지급한다. <b>보유량이 모자라면 지갑을 건드리지 않고 false를 돌려준다</b> —
    /// 아이템만 사라지는 경로를 만들지 않기 위해 차감이 먼저다.
    /// </summary>
    /// <returns>판매에 성공했으면 true.</returns>
    public bool TrySellItems(IReadOnlyDictionary<int, int> request, long gold, out List<ItemChangeInfo> changes)
    {
        if (!Inventory.TryRemoveItems(request, out changes))
        {
            return false;
        }

        // GainGold를 쓰지 않고 잔액을 직접 올린다 — 그쪽은 저장까지 해서 DB 작업이 인벤 차감과 둘로 갈라진다.
        // 대금은 BasePrice × 수량이라 음수가 될 수 없고, 0원 판매(BasePrice=0)도 그대로 통과시킨다.
        _gold = checked(_gold + gold);

        PostDBTask(new SellItemsRepository(this, changes, _gold, _dia));

        Send(new S_CurrencyResponse { Gold = _gold, Dia = _dia });

        return true;
    }
}
