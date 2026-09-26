using GameData;
using MikaProtocol;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 즉시 판매를 조립하는 서비스 계층(Singleton).
/// 가격은 <c>ItemTable.BasePrice</c>에서 읽고, 인벤토리·재화 부수효과는 <see cref="User"/>에 위임한다.
/// </summary>
public sealed class ShopService : Singleton<ShopService>
{
    // 즉시 판매가 = BasePrice × 이 비율(천분율, 1000 = 100%) — Constants.xlsx.
    // 거래소 가격의 하한과 묶이는 값이다(AuctionRules.MinUnitPrice).
    private static long SellRatePermille => Constants.SellRatePermille;

    public void Sell(User user, List<ItemInfo>? items)
    {
        if (items == null || items.Count == 0)
        {
            user.Send(new S_ItemSellResponse { Result = EResultCode.InvalidSellRequest });
            return;
        }

        // 같은 종류가 여러 번 실려 올 수 있다(클라 일괄 선택) — 합산해 종류당 한 번만 다룬다.
        var request = new Dictionary<int, int>(items.Count);
        long totalGold = 0;

        foreach (var item in items)
        {
            if (item.Count <= 0 || !GameTable.ItemTable.TryGet(item.ItemId, out var row) || !IsSellable(row))
            {
                user.Send(new S_ItemSellResponse { Result = EResultCode.InvalidSellRequest });
                return;
            }

            request[item.ItemId] = request.GetValueOrDefault(item.ItemId) + item.Count;

            // long으로 누적한다 — 일괄 판매는 종류·수량이 커서 int 상한(약 21억)에 닿을 수 있다.
            totalGold += (long)row.BasePrice * item.Count * SellRatePermille / 1000;
        }

        if (!user.TrySellItems(request, totalGold, out var changes))
        {
            user.Send(new S_ItemSellResponse { Result = EResultCode.NotEnoughItem });
            return;
        }

        user.Send(new S_ItemSellResponse
        {
            Result = EResultCode.Ok,
            GainedGold = totalGold,
            ItemChangeInfos = changes,
        });
    }

    // 판매 불가 아이템은 아직 없다. 상자(T-029)가 들어오면 여기서 갈린다.
    private static bool IsSellable(ItemTableRow row) => true;
}
