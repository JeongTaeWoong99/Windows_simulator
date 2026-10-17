using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>더미 매물 한 줄 — 원장에 바로 쓴다.</summary>
public sealed record DummyListing(AuctionItemSnapshot Item, long UnitPrice, DateTime ExpiresAt);

/// <summary>
/// 더미 판매자 매물(치트 <c>SeedAuctionListings</c>, T-168). 검색·부하 테스트에 쓸 매물을 한 번에 올린다.
/// 판매자가 실제 유저가 아니라 팔려도 대금이 아무에게도 가지 않는다 → Server/docs/치트.md
/// </summary>
public static class AuctionDummy
{
    // 실제 계정 번호(1부터 늘어난다)와 겹치지 않는 값. 경매장은 판매자 0 이하를 받지 않는다.
    public const long SellerId = 9_000_000_000_000_000_000;

    public const string SellerName = "더미상점";

    /// <summary>한 번에 올리는 상한 — 0 하나 더 친 오타가 원장을 수십만 줄로 불리지 않게.</summary>
    public const int MaxCount = 5_000;

    /// <summary>
    /// <paramref name="items"/>를 돌아가며 <paramref name="count"/>개를 만든다. 단가·수량·만료를 흩어 정렬이 보이게 한다 —
    /// 단가는 즉시 판매가의 1~4.99배, 만료는 등록 기간의 1/24 ~ 전부.
    /// </summary>
    public static List<DummyListing> Build(IReadOnlyList<ItemTableRow> items, int count, DateTime now)
    {
        var listings = new List<DummyListing>(count);
        for (var i = 0; i < count; i++)
        {
            var row  = items[i % items.Count];
            var item = new AuctionItemSnapshot
            {
                Kind       = EAuctionKind.Item,
                Tid        = row.ItemTID,
                Category   = (int)row.ItemType,
                Rarity     = (int)row.GlobalRarity,
                Count      = Math.Min(1 + i * 7 % 20, AuctionRules.MaxListingCount),
                SellerName = SellerName,
            };

            var unitPrice = Math.Min(AuctionRules.MinUnitPrice(row.BasePrice) * (100 + i * 37 % 400) / 100, AuctionRules.MaxUnitPrice);
            var expiresAt = now + AuctionRules.ListingDuration * (1 + i % 24) / 24;
            listings.Add(new DummyListing(item, unitPrice, expiresAt));
        }

        return listings;
    }
}
