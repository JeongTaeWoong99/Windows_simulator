using GameData;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 거래소 — 자원을 종류별로 보고, 원하는 수량을 최저가부터 산다. 등록·취소·내 매물은 경매와 같은 길이다(<see cref="TryRegisterAuction"/>).
/// 흐름은 Server/docs/경매장.md 4.5장.
/// </summary>
public partial class User
{
    /// <summary>한 번에 살 수 있는 수량 상한 — hold 계산(수량 × 단가 상한)이 넘치지 않게. Constants.xlsx.</summary>
    public static int MarketMaxBuyCount => (int)Constants.MarketMaxBuyCount;

    // 가격대 조회의 줄 수 — Constants.xlsx.
    private static int MarketPriceLevels => (int)Constants.MarketPriceLevels;

    // 목록·가격대·검색은 같은 빈도 제한을 쓴다 — 창을 바꿔 가며 긁는 매크로를 한 통으로 막는다.
    private bool TakeSearchToken(DateTime now)
    {
        _searchBucket ??= new TokenBucket(AuctionRules.SearchBurst, AuctionRules.SearchRefill, now);
        return _searchBucket.TryTake(now);
    }

    /// <summary>한 요청에 실을 수 있는 TID 수 — 경매장 쓰기 채널 안에서 도는 시세 조회가 밀리지 않게.</summary>
    public const int MaxQueryTids = 100;

    public void TryGetMarketItems(int category, List<int>? tids, DateTime now)
    {
        if (tids is { Count: > MaxQueryTids })
        {
            Send(new S_MarketItemsResponse { Result = EResultCode.AuctionInvalidRequest });
            return;
        }

        if (!TakeSearchToken(now))
        {
            Send(new S_MarketItemsResponse { Result = EResultCode.AuctionTooManyRequests });
            return;
        }

        var request = new Proto.MarketItemsRequest { Category = category };
        request.Tids.AddRange(tids ?? new List<int>());

        _auction.Call(c => c.GetMarketItemsAsync(request), reply =>
        {
            if (reply is null)
            {
                Send(new S_MarketItemsResponse { Result = EResultCode.AuctionUnavailable });
                return;
            }

            Send(new S_MarketItemsResponse
            {
                Result = EResultCode.Ok,
                Items  = reply.Items.Select(m => new MarketItemInfo
                {
                    Tid               = m.Tid,
                    LowestUnitPrice   = m.LowestUnitPrice,
                    AvailableCount    = m.Available,
                    RecentUnitPrice   = m.RecentUnitPrice,
                    YesterdayAvgPrice = m.YesterdayAvgPrice,
                }).ToList(),
            });
        });
    }

    public void TryGetMarketPrice(int tid, DateTime now)
    {
        if (!TakeSearchToken(now))
        {
            Send(new S_MarketPriceResponse { Result = EResultCode.AuctionTooManyRequests, Tid = tid });
            return;
        }

        var request = new Proto.PriceLadderRequest { Tid = tid, Levels = MarketPriceLevels };
        _auction.Call(c => c.GetPriceLadderAsync(request), reply =>
        {
            if (reply is null)
            {
                Send(new S_MarketPriceResponse { Result = EResultCode.AuctionUnavailable, Tid = tid });
                return;
            }

            Send(new S_MarketPriceResponse
            {
                Result = EResultCode.Ok,
                Tid    = tid,
                Levels = reply.Levels.Select(l => new MarketPriceLevelInfo { UnitPrice = l.UnitPrice, Count = l.Quantity }).ToList(),
            });
        });
    }

    /// <summary>
    /// 거래소 구매. 최악의 금액(수량 × 단가 상한)을 hold로 잡고 경매장에 수량을 예약한다 —
    /// 결과는 <see cref="OnMarketReserved"/>로 돌아오고, 실제로 잡힌 금액만 빠진다.
    /// </summary>
    public void TryBuyMarket(int tid, int count, long maxUnitPrice, DateTime now)
    {
        if (!_auction.IsAvailable)
        {
            Reply(EResultCode.AuctionUnavailable);
            return;
        }

        if (count <= 0 || count > MarketMaxBuyCount || maxUnitPrice <= 0 || maxUnitPrice > long.MaxValue / MarketMaxBuyCount)
        {
            Reply(EResultCode.AuctionInvalidRequest);
            return;
        }

        var worst = maxUnitPrice * count;
        if (AvailableGold < worst)
        {
            Reply(EResultCode.NotEnoughCurrency);
            return;
        }

        var purchaseId = AuctionIds.NextPurchaseId();
        _goldHolds[purchaseId] = worst;

        var request = new Proto.ReserveQuantityRequest
        {
            PurchaseId = purchaseId, BuyerId = Uid, Tid = tid, Quantity = count, MaxUnitPrice = maxUnitPrice,
        };
        _auction.Call(c => c.ReserveQuantityAsync(request), reply => OnMarketReserved(purchaseId, tid, count, maxUnitPrice, reply, now));
        return;

        void Reply(EResultCode result)
        {
            Send(new S_MarketBuyResponse { Result = result, Tid = tid, Count = count });
        }
    }

    /// <summary>
    /// 수량 예약 결과(로직 스레드). 예약이 요청과 맞으면 잡힌 금액만 빼고 정산을 저장한다.
    /// 어긋나면(버그) 골드를 건드리지 않고 멈춘다 — 경매장 예약은 타임아웃으로 풀린다.
    /// </summary>
    public void OnMarketReserved(long purchaseId, int tid, int count, long maxUnitPrice, Proto.ReserveReply? reply, DateTime now)
    {
        _goldHolds.Remove(purchaseId);

        if (reply is null)
        {
            // 기한 초과라도 경매장은 잡았을 수 있다 — 놓아 달라고 보낸다.
            ReleasePurchase(purchaseId, now);
            Reply(EResultCode.AuctionUnavailable);
            return;
        }

        if (reply.Result == Proto.ReserveResult.NotEnough)
        {
            Reply(EResultCode.MarketNotEnough);
            return;
        }

        if (reply.Result != Proto.ReserveResult.Ok)
        {
            Reply(ToResultCode(reply.Result));
            return;
        }

        if (IsDestroyed)
        {
            ServerLog.Warn("경매", $"거래소 예약 뒤 유저가 나감 — 예약을 놓는다. Uid={Uid} TID {tid}×{count}");
            ReleasePurchase(purchaseId, now);
            return;
        }

        var allocations = reply.Allocations.Select(a => new MarketAllocation(a.ListingId, a.Quantity, a.UnitPrice)).ToList();
        var total       = allocations.Sum(a => a.Price);
        if (allocations.Sum(a => a.Quantity) != count || allocations.Any(a => a.UnitPrice > maxUnitPrice || a.Quantity <= 0)
            || total != reply.TotalPrice || _gold < total)
        {
            ServerLog.Error("경매", $"거래소 예약이 요청과 어긋난다 — 정산하지 않는다. Uid={Uid} TID {tid}×{count} 상한 {maxUnitPrice} 예약 {reply.TotalPrice}");
            ReleasePurchase(purchaseId, now);
            Reply(EResultCode.MarketNotEnough);
            return;
        }

        _gold -= total;
        Send(new S_CurrencyResponse { Gold = _gold, Dia = _dia });

        PostDBTask(new SettleMarketRepository(this, purchaseId, tid, allocations, _gold, _dia, now));
        return;

        void Reply(EResultCode result)
        {
            Send(new S_MarketBuyResponse { Result = result, Tid = tid, Count = count });
        }
    }

    /// <summary>거래소 정산 결과(로직 스레드). 실패면 뺀 골드를 돌려준다 — DB는 정산 트랜잭션이 이미 되돌려 썼다.</summary>
    public void OnMarketSettled(int tid, int count, long totalPrice, AuctionMarketSettleResult result)
    {
        _auction.KickRelay();

        if (!result.Settled)
        {
            GainGold(totalPrice);
            Send(new S_MarketBuyResponse { Result = EResultCode.MarketNotEnough, Tid = tid, Count = count });
            return;
        }

        ServerLog.Info("경매", $"거래소 구매 Uid={Uid} TID {tid}×{count} 총액 {totalPrice} 판매자 {result.SellerMails.Count}명");
        OnMailsArrived(new List<UserMailRow> { result.BuyerMail! });
        foreach (var (sellerId, mail, closed) in result.SellerMails)
        {
            _auction.FindOnlineUser(sellerId)?.OnMarketSold(mail, closed);
        }

        Send(new S_MarketBuyResponse { Result = EResultCode.Ok, Tid = tid, Count = count, TotalPrice = totalPrice });
    }

    /// <summary>
    /// 내 거래소 매물이 팔렸다(판매자 쪽). 일부만 팔린 매물은 남은 수량으로 계속 팔리므로 다 팔린 매물(<paramref name="closedListings"/>)만
    /// 판매 중 건수에서 뺀다. 적재가 이미 읽은 우편이면 건수도 이미 DB에서 읽힌 값이라 빼지 않는다.
    /// </summary>
    public void OnMarketSold(UserMailRow mail, int closedListings)
    {
        if (OnMailsArrived(new List<UserMailRow> { mail }) > 0)
        {
            _activeListings = Math.Max(0, _activeListings - closedListings);
        }
    }
}

/// <summary>거래소 정산 한 트랜잭션 → <see cref="AuctionDb.SettleMarketAsync"/>. 작업 파티션은 구매자다.</summary>
public sealed class SettleMarketRepository(
    User buyer, long purchaseId, int tid, List<MarketAllocation> allocations, long gold, long dia, DateTime now) : IRepository
{
    private AuctionMarketSettleResult _result = new(false, null, Array.Empty<(long, UserMailRow, int)>());

    public long Key => User.DbKey;

    public User User { get; } = buyer;

    public IReadOnlyList<MarketAllocation> Allocations => allocations;

    public async Task ExecuteAsync(DbConnection connection)
        => _result = await AuctionDb.SettleMarketAsync(connection, User.Uid, purchaseId, tid, allocations, gold, dia, now);

    public void Apply() => User.OnMarketSettled(tid, allocations.Sum(a => a.Quantity), allocations.Sum(a => a.Price), _result);

    public void OnFailed(Exception e) => User.OnSettleFailed(purchaseId, allocations.Sum(a => a.Price), GetType().Name, e);
}
