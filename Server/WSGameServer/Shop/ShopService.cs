using GameData;
using MikaProtocol;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 즉시 판매와 골드 상점 구매를 조립하는 서비스 계층(Singleton).
/// 판매가는 <c>ItemTable.BasePrice</c>, 구매가는 <c>ShopTable.PriceGold</c>에서 읽고, 인벤토리·재화 부수효과는 <see cref="User"/>에 위임한다.
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

            if (user.IsItemLocked(item.ItemId))
            {
                user.Send(new S_ItemSellResponse { Result = EResultCode.EntityLocked });
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

    /// <summary>
    /// 골드 상점에서 산다(#78). 칸 → 잔액 순으로 본 뒤 차감하고 지급한다 — 어느 검사에 걸려도 아무것도 바뀌지 않는다.
    /// </summary>
    public void Buy(User user, int shopTid, int count)
    {
        // 한 번에 사는 수는 한 번에 여는 수와 같다 — 뽑기권 10장을 사서 바로 10장 연다.
        if (count < 1 || count > GachaService.MaxOpenCount || !GameTable.ShopTable.TryGet(shopTid, out var row))
        {
            Reply(EResultCode.InvalidShopRequest);
            return;
        }

        if (!user.HasStorageFor(new[] { (row.ItemTID, count) }, 0, 0))
        {
            Reply(EResultCode.StorageFull);
            return;
        }

        var cost = row.PriceGold * count;
        if (!user.TrySpendGold(cost))
        {
            Reply(EResultCode.NotEnoughCurrency);
            return;
        }

        user.Send(new S_ShopBuyResponse
        {
            Result          = EResultCode.Ok,
            ShopTID         = shopTid,
            ItemChangeInfos = user.GainItem(row.ItemTID, count),
        });
        return;

        void Reply(EResultCode code)
        {
            ServerLog.Warn("상점", $"구매 거절 — {code}. Uid={user.Uid} ShopTID={shopTid} Count={count}");
            user.Send(new S_ShopBuyResponse { Result = code, ShopTID = shopTid });
        }
    }

    // 판매 불가 아이템은 아직 없다. 상자(T-029)가 들어오면 여기서 갈린다.
    private static bool IsSellable(ItemTableRow row) => true;
}
