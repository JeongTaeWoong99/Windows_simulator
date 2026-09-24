using System.Collections.Immutable;

namespace AuctionServer;

/// <summary>
/// 판매 중 매물의 검색 인덱스. <b>불변이다</b> — 쓰기는 새 버전을 돌려주고, 읽는 쪽은 락 없이 그 순간의 버전을 본다.
/// 불변 컬렉션이 내부 구조를 공유하므로 새 버전을 만드는 비용은 O(log n)이다.
/// </summary>
public sealed class ListingIndex
{
    /// <summary>정렬 키. 단가가 같으면 매물 ID로 순서를 고정한다 — 커서가 한 점을 가리키려면 전순서여야 한다.</summary>
    private readonly record struct PriceKey(long UnitPrice, long ListingId) : IComparable<PriceKey>
    {
        public int CompareTo(PriceKey other)
        {
            var byPrice = UnitPrice.CompareTo(other.UnitPrice);
            if (byPrice != 0)
            {
                return byPrice;
            }

            return ListingId.CompareTo(other.ListingId);
        }
    }

    public static readonly ListingIndex Empty = new(
        ImmutableDictionary<long, Listing>.Empty,
        ImmutableSortedSet<PriceKey>.Empty,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>>.Empty,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>>.Empty);

    private readonly ImmutableDictionary<long, Listing>                                  _byId;
    private readonly ImmutableSortedSet<PriceKey>                                        _all;
    private readonly ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> _byCategory;

    // 종류(TID)별 가격순 — 거래소의 가격대·최저가를 바로 읽는다.
    private readonly ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> _byTid;

    private ListingIndex(
        ImmutableDictionary<long, Listing>                                  byId,
        ImmutableSortedSet<PriceKey>                                        all,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> byCategory,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> byTid)
    {
        _byId       = byId;
        _all        = all;
        _byCategory = byCategory;
        _byTid      = byTid;
    }

    public int Count => _byId.Count;

    public bool Contains(long listingId) => _byId.ContainsKey(listingId);

    public bool TryGet(long listingId, out Listing listing) => _byId.TryGetValue(listingId, out listing!);

    /// <summary>매물을 넣은 새 버전. 같은 ID가 있으면 바꿔 넣는다(남은 수량이 바뀐 경우).</summary>
    public ListingIndex Add(Listing listing)
    {
        var source = Remove(listing.ListingId);
        var key    = new PriceKey(listing.UnitPrice, listing.ListingId);

        return new ListingIndex(
            source._byId.SetItem(listing.ListingId, listing),
            source._all.Add(key),
            With(source._byCategory, (listing.Kind, listing.Category), key),
            With(source._byTid, (listing.Kind, listing.Tid), key));
    }

    /// <summary>매물을 뺀 새 버전. 없으면 자기 자신이다.</summary>
    public ListingIndex Remove(long listingId)
    {
        if (!_byId.TryGetValue(listingId, out var listing))
        {
            return this;
        }

        var key = new PriceKey(listing.UnitPrice, listing.ListingId);

        return new ListingIndex(
            _byId.Remove(listingId),
            _all.Remove(key),
            Without(_byCategory, (listing.Kind, listing.Category), key),
            Without(_byTid, (listing.Kind, listing.Tid), key));
    }

    private static ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> With(
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> groups, (ListingKind, int) group, PriceKey key)
    {
        var set = groups.TryGetValue(group, out var existing) ? existing : ImmutableSortedSet<PriceKey>.Empty;
        return groups.SetItem(group, set.Add(key));
    }

    private static ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> Without(
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> groups, (ListingKind, int) group, PriceKey key)
    {
        var set = groups[group].Remove(key);
        return set.IsEmpty ? groups.Remove(group) : groups.SetItem(group, set);
    }

    /// <summary>
    /// 거래소 목록 — 종류(TID)별 최저 단가와 판매 중 수량. 분류·TID로 거른다(0·빈 목록은 조건 없음).
    /// 만료 시각이 지난 매물은 뺀다. TID 순이다.
    /// </summary>
    public IReadOnlyList<MarketItem> Summaries(ListingKind kind, int category, IReadOnlyCollection<int> tids, DateTime now)
    {
        var wanted = tids.Count > 0 ? tids.ToHashSet() : null;
        var result = new List<MarketItem>();

        foreach (var ((groupKind, tid), set) in _byTid)
        {
            if (groupKind != kind || (wanted is not null && !wanted.Contains(tid)))
            {
                continue;
            }

            long lowest = 0, available = 0;
            Listing? sample = null;
            foreach (var key in set)
            {
                var listing = _byId[key.ListingId];
                if (listing.ExpiresAt <= now || (category != 0 && listing.Category != category))
                {
                    continue;
                }

                sample ??= listing;
                if (lowest == 0)
                {
                    lowest = listing.UnitPrice;
                }
                available += listing.Count;
            }

            if (sample is not null)
            {
                result.Add(new MarketItem(tid, sample.Category, sample.Rarity, lowest, available, 0, 0));
            }
        }

        return result.OrderBy(r => r.Tid).ToList();
    }

    /// <summary>한 종류의 가격대 — 단가가 싼 순으로 단가별 판매 중 수량. 최대 <paramref name="levels"/>칸.</summary>
    public IReadOnlyList<PriceLevel> PriceLadder(ListingKind kind, int tid, int levels, DateTime now)
    {
        var ladder = new List<PriceLevel>();
        if (!_byTid.TryGetValue((kind, tid), out var set))
        {
            return ladder;
        }

        foreach (var key in set)
        {
            var listing = _byId[key.ListingId];
            if (listing.ExpiresAt <= now)
            {
                continue;
            }

            if (ladder.Count > 0 && ladder[^1].UnitPrice == listing.UnitPrice)
            {
                ladder[^1] = ladder[^1] with { Quantity = ladder[^1].Quantity + listing.Count };
                continue;
            }

            if (ladder.Count == levels)
            {
                break;
            }

            ladder.Add(new PriceLevel(listing.UnitPrice, listing.Count));
        }

        return ladder;
    }

    /// <summary>
    /// 단가 오름차순으로 훑으며 조건에 맞는 매물을 한 페이지만큼 모은다. 조건을 코드로 검사하므로 조합 폭발이 없다.
    /// 만료 시각이 지난 매물은 청소 전이라도 뺀다.
    /// </summary>
    public SearchPage Search(SearchQuery query, DateTime now)
    {
        var pageSize = query.PageSize <= 0 ? SearchQuery.DefaultPageSize : Math.Min(query.PageSize, SearchQuery.MaxPageSize);
        var set      = PickSet(query);
        if (set.IsEmpty)
        {
            return new SearchPage(Array.Empty<Listing>(), false);
        }

        var tids    = query.Tids.Count > 0 ? query.Tids.ToHashSet() : null;
        var options = query.OptionTids.Distinct().ToList();
        var found   = new List<Listing>(pageSize);

        for (var i = StartAfter(set, query); i < set.Count; i++)
        {
            var key = set[i];
            if (query.MaxUnitPrice > 0 && key.UnitPrice > query.MaxUnitPrice)
            {
                break;
            }

            var listing = _byId[key.ListingId];
            if (!Matches(listing, query, tids, options, now))
            {
                continue;
            }

            if (found.Count == pageSize)
            {
                return new SearchPage(found, true);
            }

            found.Add(listing);
        }

        return new SearchPage(found, false);
    }

    private ImmutableSortedSet<PriceKey> PickSet(SearchQuery query)
    {
        if (query.Kind is not { } kind || query.Category == 0)
        {
            return _all;
        }

        return _byCategory.TryGetValue((kind, query.Category), out var set) ? set : ImmutableSortedSet<PriceKey>.Empty;
    }

    // 커서보다 큰 첫 위치. 첫 페이지(커서 0, 0)면 0이다. IndexOf는 없으면 "다음 큰 원소 위치"의 보수를 준다.
    private static int StartAfter(ImmutableSortedSet<PriceKey> set, SearchQuery query)
    {
        if (query.CursorUnitPrice == 0 && query.CursorListingId == 0)
        {
            return 0;
        }

        var position = set.IndexOf(new PriceKey(query.CursorUnitPrice, query.CursorListingId));
        if (position >= 0)
        {
            return position + 1;
        }

        return ~position;
    }

    private static bool Matches(Listing listing, SearchQuery query, HashSet<int>? tids, List<int> options, DateTime now)
    {
        if (listing.ExpiresAt <= now)
        {
            return false;
        }

        if (query.Kind is { } kind && listing.Kind != kind)
        {
            return false;
        }

        if (tids is not null && !tids.Contains(listing.Tid))
        {
            return false;
        }

        if (query.MinRarity > 0 && listing.Rarity < query.MinRarity)
        {
            return false;
        }

        if (query.MaxRarity > 0 && listing.Rarity > query.MaxRarity)
        {
            return false;
        }

        if (query.MinEnchantGrade > 0 && listing.EnchantGrade < query.MinEnchantGrade)
        {
            return false;
        }

        foreach (var option in options)
        {
            if (!listing.Options.Contains(option))
            {
                return false;
            }
        }

        return true;
    }
}
