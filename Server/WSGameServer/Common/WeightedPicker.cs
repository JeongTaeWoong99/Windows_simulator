namespace WSGameServer;

// 가중치 추첨기(드롭·희귀도·가챠). 생성 시 누적 가중치를 쌓아 두고 이진 탐색으로 뽑는다 — 한 번 만들어 재사용한다.
// 생성 후 불변이라 스레드 안전. 추첨은 서버에서만(P4) → Server/docs/데이터-카탈로그.md 4장
public sealed class WeightedPicker<T>
{
    private readonly T[] _items;

    /// <summary>_items와 같은 길이의 누적 가중치(오름차순). 마지막 원소가 곧 전체 합이다.</summary>
    private readonly int[] _cumulative;

    private WeightedPicker(T[] items, int[] cumulative)
    {
        _items      = items;
        _cumulative = cumulative;
    }

    /// <summary>추첨 후보 수. 가중치 0이라 제외된 항목은 세지 않는다.</summary>
    public int Count => _items.Length;

    /// <summary>전체 가중치 합. 100%를 뜻하는 값이며 100으로 맞출 필요는 없다.</summary>
    public int TotalWeight => _cumulative[^1];

    /// <summary>원본 순서를 유지한 후보 목록(가중치 0 항목 제외).</summary>
    public IReadOnlyList<T> Items => _items;

    /// <summary>후보와 가중치 선택자로 만든다. 가중치 0은 후보에서 빼고(기획상 "막아 둠"), 음수·전부 0·int 초과는 예외.</summary>
    public static WeightedPicker<T> From(IEnumerable<T> items, Func<T, int> weightSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weightSelector);

        var picked     = new List<T>();
        var cumulative = new List<int>();
        var running    = 0L;   // 오버플로를 검사하려고 long으로 누적한다

        foreach (var item in items)
        {
            var weight = weightSelector(item);
            if (weight < 0)
            {
                throw new ArgumentException($"[{typeof(T).Name}] 가중치가 음수입니다: {weight}", nameof(items));
            }
            if (weight == 0)
            {
                continue;   // 뽑히지 않는 항목 — 후보에서 제외한다
            }

            running += weight;
            if (running > int.MaxValue)
            {
                throw new ArgumentException($"[{typeof(T).Name}] 가중치 합이 int 범위를 넘습니다.", nameof(items));
            }

            picked.Add(item);
            cumulative.Add((int)running);
        }

        if (picked.Count == 0)
        {
            throw new ArgumentException(
                $"[{typeof(T).Name}] 뽑을 수 있는 후보가 없습니다(비었거나 가중치가 전부 0).", nameof(items));
        }

        return new WeightedPicker<T>(picked.ToArray(), cumulative.ToArray());
    }

    /// <summary>한 번 추첨한다.</summary>
    /// <param name="random">테스트에서 시드를 고정하려면 넘긴다. 생략하면 <see cref="Random.Shared"/>.</param>
    public T Pick(Random? random = null)
    {
        var roll = (random ?? Random.Shared).Next(TotalWeight);   // [0, TotalWeight)

        // roll이 속한 누적 구간 = roll보다 큰 첫 원소의 위치.
        // BinarySearch는 값을 찾으면 그 인덱스를, 못 찾으면 삽입 위치의 보수(~i)를 준다.
        // 정확히 일치했다는 건 roll이 그 구간의 끝(= 다음 구간의 시작)이라는 뜻이라 +1 한다.
        var found = Array.BinarySearch(_cumulative, roll);
        return _items[found >= 0 ? found + 1 : ~found];
    }

    /// <summary>count회 독립 추첨해 뽑힌 순서대로 반환한다. 가챠 10연차처럼 연출에 순서가 필요할 때 쓴다.</summary>
    public List<T> PickMany(int count, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var rng     = random ?? Random.Shared;
        var results = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            results.Add(Pick(rng));
        }

        return results;
    }

    /// <summary>count회 독립 추첨하되 결과를 모으지 않고 그때그때 넘긴다. 개수만 집계할 때 중간 리스트를 피한다.</summary>
    public void PickMany(int count, Action<T> onPicked, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(onPicked);

        var rng = random ?? Random.Shared;
        for (var i = 0; i < count; i++)
        {
            onPicked(Pick(rng));
        }
    }

    /// <summary>후보 index가 뽑힐 확률(0~1). 밸런스 검증·테스트용이며 추첨 경로에서는 쓰지 않는다.</summary>
    public double ProbabilityOf(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Length);

        var weight = index == 0 ? _cumulative[0] : _cumulative[index] - _cumulative[index - 1];
        return (double)weight / TotalWeight;
    }
}

/// <summary>WeightedPicker{T} 생성 헬퍼. 제네릭 인자를 적지 않아도 되도록 타입 추론 진입점을 둔다.</summary>
public static class WeightedPicker
{
    /// <summary>후보 목록으로 추첨기를 만든다. <c>WeightedPicker.From(rows, r =&gt; r.Weight)</c></summary>
    public static WeightedPicker<T> From<T>(IEnumerable<T> items, Func<T, int> weightSelector)
        => WeightedPicker<T>.From(items, weightSelector);

    // 그룹 축이 있는 테이블을 (그룹 키 → 추첨기)로 나눈다. 시트 키가 단일 컬럼이라 로드 후 서버가 그룹 인덱스를 만든다.
    // 테이블 로드 시 한 번 만들어 캐시한다 → Server/docs/데이터-카탈로그.md 4장
    public static Dictionary<TGroup, WeightedPicker<T>> GroupBy<T, TGroup>(
        IEnumerable<T>   items,
        Func<T, TGroup>  groupSelector,
        Func<T, int>     weightSelector)
        where TGroup : notnull
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(groupSelector);

        var buckets = new Dictionary<TGroup, List<T>>();
        foreach (var item in items)
        {
            var group = groupSelector(item);
            if (!buckets.TryGetValue(group, out var bucket))
            {
                buckets[group] = bucket = new List<T>();
            }
            bucket.Add(item);
        }

        var result = new Dictionary<TGroup, WeightedPicker<T>>(buckets.Count);
        foreach (var (group, bucket) in buckets)
        {
            result[group] = WeightedPicker<T>.From(bucket, weightSelector);
        }

        return result;
    }
}
