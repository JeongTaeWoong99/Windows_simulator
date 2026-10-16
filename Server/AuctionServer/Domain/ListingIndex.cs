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
        ImmutableDictionary<ListingKind, ImmutableSortedSet<PriceKey>>.Empty,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>>.Empty,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>>.Empty);

    private readonly ImmutableDictionary<long, Listing>                                  _byId;
    private readonly ImmutableSortedSet<PriceKey>                                        _all;
    private readonly ImmutableDictionary<ListingKind, ImmutableSortedSet<PriceKey>>        _byKind;
    private readonly ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> _byCategory;

    // 종류(TID)별 가격순 — 거래소의 가격대·최저가를 바로 읽는다.
    private readonly ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> _byTid;

    private ListingIndex(
        ImmutableDictionary<long, Listing>                                  byId,
        ImmutableSortedSet<PriceKey>                                        all,
        ImmutableDictionary<ListingKind, ImmutableSortedSet<PriceKey>>        byKind,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> byCategory,
        ImmutableDictionary<(ListingKind, int), ImmutableSortedSet<PriceKey>> byTid)
    {
        _byId       = byId;
        _all        = all;
        _byKind     = byKind;
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
            With(source._byKind, listing.Kind, key),
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
            Without(_byKind, listing.Kind, key),
            Without(_byCategory, (listing.Kind, listing.Category), key),
            Without(_byTid, (listing.Kind, listing.Tid), key));
    }

    private static ImmutableDictionary<TGroup, ImmutableSortedSet<PriceKey>> With<TGroup>(
        ImmutableDictionary<TGroup, ImmutableSortedSet<PriceKey>> groups, TGroup group, PriceKey key)
        where TGroup : notnull
    {
        var set = groups.TryGetValue(group, out var existing) ? existing : ImmutableSortedSet<PriceKey>.Empty;
        return groups.SetItem(group, set.Add(key));
    }

    private static ImmutableDictionary<TGroup, ImmutableSortedSet<PriceKey>> Without<TGroup>(
        ImmutableDictionary<TGroup, ImmutableSortedSet<PriceKey>> groups, TGroup group, PriceKey key)
        where TGroup : notnull
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
    /// 조건에 맞는 매물을 커서 다음부터 한 쪽만큼 돌려준다. 만료 시각이 지난 매물은 청소 전이라도 뺀다 (T-155).
    /// 훑는 범위는 TID → 분류 → 종류 중 가장 좁은 인덱스다(이슈 #71). 단가 낮은순은 인덱스 순서 그대로라 커서부터 한 쪽만 읽고,
    /// 다른 정렬은 범위를 한 번 훑으며 커서 다음의 앞쪽 한 쪽만 남긴다. 건수는 <see cref="SearchQuery.TotalCountCap"/>에서 멈춘다.
    /// </summary>
    public SearchPage Search(SearchQuery query, DateTime now)
    {
        var pageSize = query.PageSize <= 0 ? SearchQuery.DefaultPageSize : Math.Min(query.PageSize, SearchQuery.MaxPageSize);
        var tids     = query.Tids.Count > 0 ? query.Tids.ToHashSet() : null;
        var options  = query.OptionTids.Distinct().ToList();
        var sets     = PickSets(query);

        bool IsMatch(Listing listing) => Matches(listing, query, tids, options, now);

        return query.Sort == SearchSort.PriceAsc
            ? SearchByPrice(sets, IsMatch, query, pageSize)
            : SearchBySortValue(sets, IsMatch, query, pageSize);
    }

    // 단가 낮은순 — 집합들을 커서 다음부터 합쳐 읽다가 한 쪽 + 1개를 채우면 멈춘다.
    private SearchPage SearchByPrice(List<ImmutableSortedSet<PriceKey>> sets, Func<Listing, bool> isMatch, SearchQuery query, int pageSize)
    {
        var page    = new List<(long Value, Listing Listing)>();
        var hasMore = false;
        foreach (var listing in MergeByPrice(sets, query, Cursor(query)))
        {
            if (!isMatch(listing))
            {
                continue;
            }

            if (page.Count == pageSize)
            {
                hasMore = true;
                break;
            }

            page.Add((listing.UnitPrice, listing));
        }

        var total = 0;
        foreach (var listing in sets.SelectMany(set => Walk(set, query, after: null)))
        {
            if (isMatch(listing) && ++total == SearchQuery.TotalCountCap)
            {
                break;
            }
        }

        return ToPage(page, hasMore, total);
    }

    // 그 밖의 정렬 — 인덱스 순서와 달라 범위를 다 본다. 정렬은 하지 않고 커서 다음의 앞쪽 한 쪽 + 1개만 큐에 남긴다.
    private SearchPage SearchBySortValue(List<ImmutableSortedSet<PriceKey>> sets, Func<Listing, bool> isMatch, SearchQuery query, int pageSize)
    {
        var cursor = Cursor(query);
        var keep   = pageSize + 1;

        // 큐의 맨 앞이 남긴 것 중 가장 뒤 순서다 — 넘치면 그것부터 버린다.
        var best  = new PriorityQueue<(long Value, Listing Listing), (long, long)>(Comparer<(long, long)>.Create((a, b) => b.CompareTo(a)));
        var total = 0;
        foreach (var listing in sets.SelectMany(set => Walk(set, query, after: null)))
        {
            if (!isMatch(listing))
            {
                continue;
            }

            total++;
            var value = SortValue(listing, query);
            if (cursor is { } c && (value, listing.ListingId).CompareTo((c.UnitPrice, c.ListingId)) <= 0)
            {
                continue;
            }

            best.Enqueue((value, listing), (value, listing.ListingId));
            if (best.Count > keep)
            {
                best.Dequeue();
            }
        }

        var picked = new List<(long Value, Listing Listing)>();
        while (best.TryDequeue(out var entry, out _))
        {
            picked.Add(entry);
        }
        picked.Reverse();

        return ToPage(picked.Take(pageSize).ToList(), picked.Count > pageSize, total);
    }

    private static SearchPage ToPage(List<(long Value, Listing Listing)> page, bool hasMore, int total)
    {
        var totalCount = Math.Min(total, SearchQuery.TotalCountCap);
        if (page.Count == 0)
        {
            return new SearchPage(Array.Empty<Listing>(), false, totalCount);
        }

        var last = page[^1];
        return new SearchPage(page.Select(x => x.Listing).ToList(), hasMore, totalCount, last.Value, last.Listing.ListingId);
    }

    // 첫 쪽은 커서가 없다. 정렬 값을 단가 자리에 담는다 — 단가 낮은순이면 둘이 같다.
    private static PriceKey? Cursor(SearchQuery query)
    {
        if (query.CursorSortValue == 0 && query.CursorListingId == 0)
        {
            return null;
        }

        return new PriceKey(query.CursorSortValue, query.CursorListingId);
    }

    // 집합마다 시작 칸부터 단가순으로 꺼내 하나로 합친다. 집합끼리는 겹치지 않는다.
    private IEnumerable<Listing> MergeByPrice(List<ImmutableSortedSet<PriceKey>> sets, SearchQuery query, PriceKey? after)
    {
        var heads = new PriorityQueue<IEnumerator<Listing>, PriceKey>();
        foreach (var set in sets)
        {
            var walker = Walk(set, query, after).GetEnumerator();
            if (walker.MoveNext())
            {
                heads.Enqueue(walker, new PriceKey(walker.Current.UnitPrice, walker.Current.ListingId));
            }
        }

        while (heads.TryDequeue(out var walker, out _))
        {
            yield return walker.Current;
            if (walker.MoveNext())
            {
                heads.Enqueue(walker, new PriceKey(walker.Current.UnitPrice, walker.Current.ListingId));
            }
        }
    }

    // 한 집합을 단가순으로 — 단가 하한(과 커서) 다음부터 시작해 단가 상한을 넘으면 멈춘다. 시작 칸은 이진 탐색으로 찾는다.
    private IEnumerable<Listing> Walk(ImmutableSortedSet<PriceKey> set, SearchQuery query, PriceKey? after)
    {
        var start = StartIndex(set, new PriceKey(query.MinUnitPrice, 0), inclusive: true);
        if (after is { } cursor)
        {
            start = Math.Max(start, StartIndex(set, cursor, inclusive: false));
        }

        for (var i = start; i < set.Count; i++)
        {
            var key = set[i];
            if (query.MaxUnitPrice > 0 && key.UnitPrice > query.MaxUnitPrice)
            {
                yield break;
            }

            yield return _byId[key.ListingId];
        }
    }

    private static int StartIndex(ImmutableSortedSet<PriceKey> set, PriceKey key, bool inclusive)
    {
        var index = set.IndexOf(key);
        if (index < 0)
        {
            return ~index;
        }

        return inclusive ? index : index + 1;
    }

    // 정렬 값 — 작은 것이 먼저다. 내림차순은 부호를 뒤집는다.
    private static long SortValue(Listing listing, SearchQuery query)
    {
        switch (query.Sort)
        {
            case SearchSort.PriceDesc:
                return -listing.UnitPrice;
            case SearchSort.ExpiresSoon:
                return new DateTimeOffset(listing.ExpiresAt).ToUnixTimeMilliseconds();
            case SearchSort.RarityDesc:
                return -listing.Rarity;
            case SearchSort.AttrDesc:
                return -listing.Attr(query.SortAttr);
            default:
                return listing.UnitPrice;
        }
    }

    // 훑을 인덱스 — 가장 좁은 것을 고른다. TID가 여럿이거나 종류가 없으면 집합이 여럿이고, 서로 겹치지 않는다.
    private List<ImmutableSortedSet<PriceKey>> PickSets(SearchQuery query)
    {
        if (query.Tids.Count > 0)
        {
            var kinds = query.Kind is { } kind ? new[] { kind } : Enum.GetValues<ListingKind>();
            return query.Tids.Distinct()
                .SelectMany(tid => kinds.Select(k => (k, tid)))
                .Where(_byTid.ContainsKey)
                .Select(group => _byTid[group])
                .ToList();
        }

        if (query.Kind is not { } only)
        {
            return new List<ImmutableSortedSet<PriceKey>> { _all };
        }

        if (query.Category != 0)
        {
            return _byCategory.TryGetValue((only, query.Category), out var byCategory)
                ? new List<ImmutableSortedSet<PriceKey>> { byCategory }
                : new List<ImmutableSortedSet<PriceKey>>();
        }

        return _byKind.TryGetValue(only, out var byKind)
            ? new List<ImmutableSortedSet<PriceKey>> { byKind }
            : new List<ImmutableSortedSet<PriceKey>>();
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

        // TID 인덱스로 고르면 분류가 걸러지지 않았다.
        if (query.Kind is not null && query.Category != 0 && listing.Category != query.Category)
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

        if (query.MinUnitPrice > 0 && listing.UnitPrice < query.MinUnitPrice)
        {
            return false;
        }

        if (query.MaxUnitPrice > 0 && listing.UnitPrice > query.MaxUnitPrice)
        {
            return false;
        }

        foreach (var range in query.Attrs)
        {
            var value = listing.Attr(range.Key);
            if (value < range.Min || (range.Max > 0 && value > range.Max))
            {
                return false;
            }
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
