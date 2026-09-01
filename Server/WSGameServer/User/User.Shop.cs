using GameData;
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

        // 총액이 0이면 지갑을 건드리지 않는다 — Gain은 0을 거부한다(지급 경로로 차감이 새는 것을 막는 규약).
        var remain = GetCurrency(CurrencyType.Gold);
        if (gold > 0)
        {
            remain = Wallet.Gain(CurrencyType.Gold, gold);
        }

        // 저장은 SellItemsRepository 하나가 맡는다 — GainCurrency를 쓰면 DB 작업이 둘로 갈라진다.
        PostDBTask(new SellItemsRepository(this, changes, CurrencyType.Gold, remain));

        Send(new S_CurrencyResponse
        {
            Currencies = new List<CurrencyInfo>
            {
                new() { CurrencyType = (byte)CurrencyType.Gold, Amount = remain },
            },
        });

        return true;
    }
}
