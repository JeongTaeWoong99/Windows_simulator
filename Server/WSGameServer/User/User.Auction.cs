using GameData;
using MikaProtocol;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>경매 — 등록·구매·취소·검색. 흐름과 경계는 Server/docs/경매장.md 4장.</summary>
public partial class User
{
    private readonly AuctionService _auction;

    // 구매 중 골드 hold(구매 시도 ID → 금액). 경매장 응답을 기다리는 동안 그 골드를 다른 데 쓰지 못하게 한다.
    private readonly Dictionary<long, long> _goldHolds = new();

    // 판매 중 매물 수. -1은 아직 못 읽었다는 뜻 — 읽기 전에는 등록을 받지 않는다(상한을 모른다).
    private int _activeListings = -1;

    private TokenBucket? _searchBucket;

    public long HeldGold => _goldHolds.Values.Sum();

    /// <summary>쓸 수 있는 골드 = 잔액 − hold. 차감 경로는 전부 이 값으로 검사한다.</summary>
    public long AvailableGold => _gold - HeldGold;

    public int ActiveListingCount => _activeListings;

    private void LoadAuctionState() => PostDBTask(new LoadAuctionStateRepository(this));

    public void OnAuctionStateLoaded(int activeListings) => _activeListings = activeListings;

    // ── 등록 ──

    /// <summary>
    /// 경매 등록. 검증 → 메모리에서 빼기(자원 차감·장비 제거) + 등록비 차감 → 한 트랜잭션 저장.
    /// 장비를 메모리에서 빼는 이유: 메모리에 남으면 이후 저장이 잠긴 행을 덮는다.
    /// </summary>
    public void TryRegisterAuction(EAuctionKind kind, int itemTid, int count, long equipId, long unitPrice, DateTime now)
    {
        if (!_auction.IsAvailable || _activeListings < 0)
        {
            Reject(EResultCode.AuctionUnavailable);
            return;
        }

        if (_activeListings >= AuctionRules.MaxActiveListings)
        {
            Reject(EResultCode.AuctionListingLimit);
            return;
        }

        var (code, item, basePrice) = kind switch
        {
            EAuctionKind.Item  => DescribeItem(itemTid, count),
            EAuctionKind.Equip => DescribeEquip(equipId),
            _                  => (EResultCode.AuctionInvalidRequest, null, 0),
        };
        if (code != EResultCode.Ok)
        {
            Reject(code);
            return;
        }

        if (!AuctionRules.IsInBand(basePrice, unitPrice))
        {
            Reject(EResultCode.AuctionPriceOutOfBand);
            return;
        }

        var fee = AuctionRules.ListingFee(checked(unitPrice * item!.Count));
        if (AvailableGold < fee)
        {
            Reject(EResultCode.NotEnoughCurrency);
            return;
        }

        var changes = new List<ItemChangeInfo>();
        if (kind == EAuctionKind.Item)
        {
            Inventory.TryRemoveItems(new Dictionary<int, int> { [itemTid] = count }, out changes);
        }
        else
        {
            _equips.Remove(equipId);
        }

        _gold -= fee;
        _activeListings++;

        PostDBTask(new RegisterAuctionRepository(this, item, unitPrice, fee, changes, _gold, _dia, now + AuctionRules.ListingDuration, now));
        Send(new S_CurrencyResponse { Gold = _gold, Dia = _dia });
        return;

        void Reject(EResultCode result)
        {
            ServerLog.Warn("경매", $"등록 거절 {result} Uid={Uid} {kind} TID {itemTid}×{count} 장비 {equipId} 단가 {unitPrice}");
            Send(new S_AuctionRegisterResponse { Result = result, EquipId = equipId });
        }
    }

    private (EResultCode, AuctionItemSnapshot?, int BasePrice) DescribeItem(int itemTid, int count)
    {
        if (count <= 0 || !GameTable.ItemTable.TryGet(itemTid, out var row))
        {
            return (EResultCode.AuctionInvalidRequest, null, 0);
        }

        if (Inventory.GetCount(itemTid) < count)
        {
            return (EResultCode.NotEnoughItem, null, 0);
        }

        var item = new AuctionItemSnapshot
        {
            Kind = EAuctionKind.Item, Tid = itemTid, Category = (int)row.ItemType, Rarity = (int)row.GlobalRarity, Count = count,
        };
        return (EResultCode.Ok, item, row.BasePrice);
    }

    private (EResultCode, AuctionItemSnapshot?, int BasePrice) DescribeEquip(long equipId)
    {
        if (!TryGetEquip(equipId, out var equip))
        {
            return (EResultCode.EquipNotOwned, null, 0);
        }

        if (equip.IsEquipped)
        {
            return (EResultCode.AuctionEquipped, null, 0);
        }

        var item = new AuctionItemSnapshot
        {
            Kind         = EAuctionKind.Equip,
            Tid          = equip.Tid,
            Category     = (int)equip.Kind,
            Rarity       = (int)equip.Row.GlobalRarity,
            Count        = 1,
            EquipId      = equip.Id,
            EnchantGrade = (int)equip.EnchantGrade,
            Options      = equip.EnchantOptionTids.ToList(),
        };
        return (EResultCode.Ok, item, equip.Row.BasePrice);
    }

    public void OnAuctionRegistered(long tradeId, AuctionItemSnapshot item, long fee, List<ItemChangeInfo> changes)
    {
        ServerLog.Info("경매", $"등록 Uid={Uid} 매물 {tradeId} {item.Kind} TID {item.Tid}×{item.Count} 등록비 {fee}");

        Send(new S_AuctionRegisterResponse
        {
            Result          = EResultCode.Ok,
            ListingId       = tradeId,
            ListingFee      = fee,
            ItemChangeInfos = changes,
            EquipId         = item.EquipId,
        });
        _auction.KickRelay();
    }

    // ── 구매 ──

    /// <summary>즉시구매. hold를 잡고 경매장에 예약한다 — 결과는 <see cref="OnAuctionReserved"/>로 돌아온다.</summary>
    public void TryBuyAuction(long listingId, long expectedTotal, DateTime now)
    {
        if (!_auction.IsAvailable)
        {
            Reply(EResultCode.AuctionUnavailable);
            return;
        }

        if (listingId <= 0 || expectedTotal <= 0)
        {
            Reply(EResultCode.AuctionInvalidRequest);
            return;
        }

        if (AvailableGold < expectedTotal)
        {
            Reply(EResultCode.NotEnoughCurrency);
            return;
        }

        var purchaseId = AuctionIds.NextPurchaseId();
        _goldHolds[purchaseId] = expectedTotal;

        var request = new Proto.ReserveRequest { PurchaseId = purchaseId, ListingId = listingId, BuyerId = Uid, ExpectedTotal = expectedTotal };
        _auction.Call(c => c.ReserveAsync(request), reply => OnAuctionReserved(purchaseId, listingId, expectedTotal, reply, now));
        return;

        void Reply(EResultCode result)
        {
            Send(new S_AuctionBuyResponse { Result = result, ListingId = listingId });
        }
    }

    /// <summary>
    /// 예약 결과(로직 스레드). 성공이면 골드를 빼고 정산을 저장한다. 유저가 이미 나갔으면 아무것도 안 한다 —
    /// 경매장 예약은 타임아웃으로 풀린다.
    /// </summary>
    public void OnAuctionReserved(long purchaseId, long listingId, long expectedTotal, Proto.ReserveReply? reply, DateTime now)
    {
        _goldHolds.Remove(purchaseId);

        if (reply is null)
        {
            // 기한 초과라도 경매장은 잡았을 수 있다 — 놓아 달라고 보낸다.
            ReleasePurchase(purchaseId, now);
            Send(new S_AuctionBuyResponse { Result = EResultCode.AuctionUnavailable, ListingId = listingId });
            return;
        }

        if (reply.Result != Proto.ReserveResult.Ok)
        {
            Send(new S_AuctionBuyResponse { Result = ToResultCode(reply.Result), ListingId = listingId });
            return;
        }

        if (IsDestroyed)
        {
            ServerLog.Warn("경매", $"예약 뒤 유저가 나감 — 예약을 놓는다. Uid={Uid} 매물 {listingId}");
            ReleasePurchase(purchaseId, now);
            return;
        }

        // 경매장이 총액 일치를 조건으로 예약했으므로 어긋나면 버그다. 골드를 음수로 만들지 않고 멈춘다 — 예약은 타임아웃으로 풀린다.
        if (reply.TotalPrice != expectedTotal || _gold < reply.TotalPrice)
        {
            ServerLog.Error("경매", $"예약 총액 불일치 — 정산하지 않는다. Uid={Uid} 매물 {listingId} 본 {expectedTotal} 예약 {reply.TotalPrice} 잔액 {_gold}");
            ReleasePurchase(purchaseId, now);
            Send(new S_AuctionBuyResponse { Result = EResultCode.AuctionPriceChanged, ListingId = listingId });
            return;
        }

        _gold -= reply.TotalPrice;
        Send(new S_CurrencyResponse { Gold = _gold, Dia = _dia });

        PostDBTask(new SettleAuctionRepository(this, purchaseId, listingId, reply.TotalPrice, _gold, _dia, now));
    }

    // 버린 예약을 놓는다 — 안 보내면 그 수량이 타임아웃까지 잠겨 취소·만료·다른 구매가 막힌다.
    private void ReleasePurchase(long purchaseId, DateTime now) => PostDBTask(new ReleasePurchaseRepository(this, purchaseId, now));

    public void OnPurchaseReleased() => _auction.KickRelay();

    /// <summary>
    /// 정산이 예외로 끝났다(로직 스레드). 트랜잭션은 롤백됐으니 메모리에서 뺀 대금을 돌려주고 예약도 놓은 뒤 세션을 끊는다 —
    /// 돌려주지 않으면 이후 재화 저장이 대금이 빠진 잔액을 확정값으로 쓴다.
    /// </summary>
    public void OnSettleFailed(long purchaseId, long totalPrice, string repositoryName, Exception e)
    {
        _gold = checked(_gold + totalPrice);
        PostDBTask(new SaveCurrencyRepository(this, _gold, _dia));
        ReleasePurchase(purchaseId, DateTime.UtcNow);
        OnDbFailed(repositoryName, e);
    }

    /// <summary>정산 결과(로직 스레드). 실패면 뺀 골드를 돌려준다 — 저장은 그 반환이 확정 잔액으로 덮는다.</summary>
    public void OnAuctionSettled(long listingId, long totalPrice, AuctionSettleResult result)
    {
        _auction.KickRelay();

        if (!result.Settled)
        {
            ServerLog.Warn("경매", $"정산 실패 — 골드 반환. Uid={Uid} 매물 {listingId}");
            GainGold(totalPrice);
            Send(new S_AuctionBuyResponse { Result = EResultCode.AuctionSoldOut, ListingId = listingId });
            return;
        }

        ServerLog.Info("경매", $"구매 Uid={Uid} 매물 {listingId} 총액 {totalPrice} 판매자 {result.SellerId}");
        OnMailsArrived(new List<UserMailRow> { result.BuyerMail! });
        _auction.FindOnlineUser(result.SellerId)?.OnAuctionClosed(result.SellerMail!);

        Send(new S_AuctionBuyResponse { Result = EResultCode.Ok, ListingId = listingId });
    }

    /// <summary>내 매물이 팔렸거나 돌아왔다(판매자 쪽, 로직 스레드). 우편이 이미 DB에 있다 — 도착만 알린다.</summary>
    public void OnAuctionClosed(UserMailRow mail)
    {
        // 적재가 이미 읽은 우편이면 판매 중 건수도 이미 DB에서 읽힌 값이다 — 두 번 빼지 않는다.
        if (OnMailsArrived(new List<UserMailRow> { mail }) > 0 && _activeListings > 0)
        {
            _activeListings--;
        }
    }

    private static EResultCode ToResultCode(Proto.ReserveResult result)
    {
        switch (result)
        {
            case Proto.ReserveResult.NotFound:
                return EResultCode.AuctionNotFound;
            case Proto.ReserveResult.InProgress:
                return EResultCode.AuctionInProgress;
            case Proto.ReserveResult.Sold:
                return EResultCode.AuctionSoldOut;
            case Proto.ReserveResult.Closed:
                return EResultCode.AuctionClosed;
            case Proto.ReserveResult.PriceChanged:
                return EResultCode.AuctionPriceChanged;
            case Proto.ReserveResult.OwnListing:
                return EResultCode.AuctionOwnListing;
            default:
                return EResultCode.AuctionUnavailable;
        }
    }

    // ── 취소 ──

    /// <summary>취소. 경매장이 판정하고, 아이템은 취소 이벤트 → 릴레이 → 반환 우편으로 돌아온다.</summary>
    public void TryCancelAuction(long listingId)
    {
        var request = new Proto.CancelRequest { ListingId = listingId, SellerId = Uid };
        _auction.Call(c => c.CancelAsync(request), reply =>
        {
            var result = reply is null ? EResultCode.AuctionUnavailable : ToResultCode(reply.Result);
            if (result == EResultCode.Ok)
            {
                _auction.KickRelay();
            }

            Send(new S_AuctionCancelResponse { Result = result, ListingId = listingId });
        });
    }

    private static EResultCode ToResultCode(Proto.CancelResult result)
    {
        switch (result)
        {
            case Proto.CancelResult.Ok:
                return EResultCode.Ok;
            case Proto.CancelResult.NotFound:
                return EResultCode.AuctionNotFound;
            case Proto.CancelResult.NotOwner:
                return EResultCode.AuctionNotOwner;
            case Proto.CancelResult.InProgress:
                return EResultCode.AuctionInProgress;
            default:
                return EResultCode.AuctionClosed;
        }
    }

    // ── 조회 ──

    /// <summary>검색 중계. 유저별 빈도 제한을 여기서 건다 — 싼 매물을 긁는 매크로를 막는다.</summary>
    public void TrySearchAuction(C_AuctionSearchRequest req, DateTime now)
    {
        if (req.Tids is { Count: > MaxQueryTids } || req.OptionTids is { Count: > MaxQueryTids })
        {
            Send(new S_AuctionSearchResponse { Result = EResultCode.AuctionInvalidRequest });
            return;
        }

        if (!TakeSearchToken(now))
        {
            Send(new S_AuctionSearchResponse { Result = EResultCode.AuctionTooManyRequests });
            return;
        }

        var request = new Proto.SearchRequest
        {
            Kind            = (int)req.Kind,
            Category        = req.Category,
            MinRarity       = req.MinRarity,
            MaxRarity       = req.MaxRarity,
            MinEnchantGrade = req.MinEnchantGrade,
            MaxUnitPrice    = req.MaxUnitPrice,
            CursorUnitPrice = req.CursorUnitPrice,
            CursorListingId = req.CursorListingId,
            PageSize        = req.PageSize,
        };
        request.Tids.AddRange(req.Tids ?? new List<int>());
        request.OptionTids.AddRange(req.OptionTids ?? new List<int>());

        _auction.Call(c => c.SearchAsync(request), reply =>
        {
            if (reply is null)
            {
                Send(new S_AuctionSearchResponse { Result = EResultCode.AuctionUnavailable });
                return;
            }

            Send(new S_AuctionSearchResponse
            {
                Result   = EResultCode.Ok,
                Listings = reply.Listings.Select(ToListingInfo).ToList(),
                HasMore  = reply.HasMore,
            });
        });
    }

    public void TryGetMyAuctionListings()
    {
        var request = new Proto.SellerListingsRequest { SellerId = Uid };
        _auction.Call(c => c.GetSellerListingsAsync(request), reply =>
        {
            if (reply is null)
            {
                Send(new S_AuctionMyListingsResponse { Result = EResultCode.AuctionUnavailable });
                return;
            }

            Send(new S_AuctionMyListingsResponse { Result = EResultCode.Ok, Listings = reply.Listings.Select(ToListingInfo).ToList() });
        });
    }

    private static AuctionListingInfo ToListingInfo(Proto.ListingView v)
    {
        return new AuctionListingInfo
        {
            ListingId       = v.ListingId,
            Kind            = (EAuctionKind)v.Kind,
            Tid             = v.Tid,
            Category        = v.Category,
            Rarity          = v.Rarity,
            Count           = v.Count,
            EnchantGrade    = v.EnchantGrade,
            EnchantOptions  = v.Options.ToList(),
            UnitPrice       = v.UnitPrice,
            TotalPrice      = v.TotalPrice,
            ExpiresAtUnixMs = v.ExpiresAtUnixMs,
            State           = (EAuctionListingState)v.State,
        };
    }
}
