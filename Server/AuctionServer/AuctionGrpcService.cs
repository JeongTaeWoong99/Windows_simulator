using Grpc.Core;
using Proto = AuctionProtocol;

namespace AuctionServer;

/// <summary>gRPC ↔ 엔진 매핑만 한다. 판정은 전부 엔진·DB가 한다.</summary>
public sealed class AuctionGrpcService(AuctionEngine engine) : Proto.Auction.AuctionBase
{
    public override async Task<Proto.RegisterReply> Register(Proto.RegisterRequest request, ServerCallContext context)
    {
        if (request.Listing is null)
        {
            return new Proto.RegisterReply { Result = Proto.RegisterResult.Rejected };
        }

        var result = await engine.RegisterAsync(ToListing(request.Listing));
        return new Proto.RegisterReply { Result = (Proto.RegisterResult)result };
    }

    public override async Task<Proto.ReserveReply> Reserve(Proto.ReserveRequest request, ServerCallContext context)
    {
        var outcome = await engine.ReserveAsync(request.PurchaseId, request.ListingId, request.BuyerId, request.ExpectedTotal);
        return ToReply(outcome);
    }

    public override async Task<Proto.ReserveReply> ReserveQuantity(Proto.ReserveQuantityRequest request, ServerCallContext context)
    {
        var outcome = await engine.ReserveQuantityAsync(request.PurchaseId, request.BuyerId, request.Tid, request.Quantity, request.MaxUnitPrice);
        return ToReply(outcome);
    }

    private static Proto.ReserveReply ToReply(ReserveOutcome outcome)
    {
        var reply = new Proto.ReserveReply
        {
            Result     = (Proto.ReserveResult)outcome.Result,
            SellerId   = outcome.SellerId,
            TotalPrice = outcome.TotalPrice,
        };
        reply.Allocations.AddRange(outcome.Lines.Select(a => new Proto.Allocation
        {
            ListingId = a.ListingId, SellerId = a.SellerId, Quantity = a.Quantity, UnitPrice = a.UnitPrice,
        }));
        return reply;
    }

    public override async Task<Proto.MarketItemsReply> GetMarketItems(Proto.MarketItemsRequest request, ServerCallContext context)
    {
        var items = await engine.MarketItemsAsync(request.Category, request.Tids.ToArray());

        var reply = new Proto.MarketItemsReply();
        reply.Items.AddRange(items.Select(m => new Proto.MarketItem
        {
            Tid = m.Tid, Category = m.Category, Rarity = m.Rarity, LowestUnitPrice = m.LowestUnitPrice, Available = m.Available,
            RecentUnitPrice = m.RecentUnitPrice, YesterdayAvgPrice = m.YesterdayAvgPrice,
        }));
        return reply;
    }

    public override Task<Proto.PriceLadderReply> GetPriceLadder(Proto.PriceLadderRequest request, ServerCallContext context)
    {
        var reply = new Proto.PriceLadderReply();
        reply.Levels.AddRange(engine.PriceLadder(request.Tid, request.Levels)
            .Select(l => new Proto.PriceLevel { UnitPrice = l.UnitPrice, Quantity = l.Quantity }));
        return Task.FromResult(reply);
    }

    public override async Task<Proto.ConfirmReply> Confirm(Proto.ConfirmRequest request, ServerCallContext context)
    {
        await engine.ConfirmAsync(request.PurchaseId, request.Success);
        return new Proto.ConfirmReply();
    }

    public override async Task<Proto.CancelReply> Cancel(Proto.CancelRequest request, ServerCallContext context)
    {
        var result = await engine.CancelAsync(request.ListingId, request.SellerId);
        return new Proto.CancelReply { Result = (Proto.CancelResult)result };
    }

    public override Task<Proto.SearchReply> Search(Proto.SearchRequest request, ServerCallContext context)
    {
        var page = engine.Search(new SearchQuery
        {
            Kind            = request.Kind == 0 ? null : (ListingKind)request.Kind,
            Category        = request.Category,
            Tids            = request.Tids.ToArray(),
            MinRarity       = request.MinRarity,
            MaxRarity       = request.MaxRarity,
            MinEnchantGrade = request.MinEnchantGrade,
            OptionTids      = request.OptionTids.ToArray(),
            MaxUnitPrice    = request.MaxUnitPrice,
            CursorUnitPrice = request.CursorUnitPrice,
            CursorListingId = request.CursorListingId,
            PageSize        = request.PageSize,
        });

        var reply = new Proto.SearchReply { HasMore = page.HasMore };
        reply.Listings.AddRange(page.Listings.Select(l => ToView(l, ListingState.Listed)));
        return Task.FromResult(reply);
    }

    public override async Task<Proto.SellerListingsReply> GetSellerListings(Proto.SellerListingsRequest request, ServerCallContext context)
    {
        var listings = await engine.GetSellerListingsAsync(request.SellerId);

        var reply = new Proto.SellerListingsReply();
        reply.Listings.AddRange(listings.Select(l => ToView(l.Listing, l.State)));
        return reply;
    }

    public override async Task<Proto.FetchEventsReply> FetchEvents(Proto.FetchEventsRequest request, ServerCallContext context)
    {
        var events = await engine.FetchEventsAsync(request.Max);

        var reply = new Proto.FetchEventsReply();
        reply.Events.AddRange(events.Select(e => new Proto.AuctionEvent
        {
            EventId   = e.EventId,
            Kind      = (Proto.EventKind)e.Kind,
            ListingId = e.ListingId,
            SellerId  = e.SellerId,
        }));
        return reply;
    }

    public override async Task<Proto.AckEventsReply> AckEvents(Proto.AckEventsRequest request, ServerCallContext context)
    {
        await engine.AckEventsAsync(request.EventIds.ToArray());
        return new Proto.AckEventsReply();
    }

    public override async Task<Proto.ListingStatesReply> GetListingStates(Proto.ListingStatesRequest request, ServerCallContext context)
    {
        var states = await engine.GetStatesAsync(request.ListingIds.ToArray());

        var reply = new Proto.ListingStatesReply();
        reply.States.AddRange(states.Select(s => new Proto.ListingStateEntry
        {
            ListingId = s.Key,
            State     = s.Value is { } state ? (Proto.ListingState)state : Proto.ListingState.NotFound,
        }));
        return reply;
    }

    private static Listing ToListing(Proto.ListingSnapshot s)
    {
        return new Listing(
            s.ListingId, s.SellerId, (ListingKind)s.Kind, s.Tid, s.Category, s.Rarity, s.Count, s.EnchantGrade,
            s.Options.ToArray(), s.UnitPrice, DateTimeOffset.FromUnixTimeMilliseconds(s.ExpiresAtUnixMs).UtcDateTime);
    }

    private static Proto.ListingView ToView(Listing l, ListingState state)
    {
        var view = new Proto.ListingView
        {
            ListingId       = l.ListingId,
            Kind            = (int)l.Kind,
            Tid             = l.Tid,
            Category        = l.Category,
            Rarity          = l.Rarity,
            Count           = l.Count,
            EnchantGrade    = l.EnchantGrade,
            UnitPrice       = l.UnitPrice,
            TotalPrice      = l.TotalPrice,
            ExpiresAtUnixMs = new DateTimeOffset(l.ExpiresAt).ToUnixTimeMilliseconds(),
            State           = (Proto.ListingState)state,
        };
        view.Options.AddRange(l.Options);
        return view;
    }
}
