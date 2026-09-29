using System;
using System.Collections.Generic;
using GameData;

// 인벤토리 [정렬]의 방향. 도구 줄의 화살표 버튼이 둘을 오간다.
//
// ※ 오름차순은 **규칙 전체를 뒤집는다** — 등급만이 아니라 산업·TID 순서까지 반대가 된다.
//   기준을 하나만 뒤집으면 "무엇이 반대인가"를 매번 표로 확인해야 한다.
public enum InventorySortOrder
{
    // 등급 높은 순 (기본) — 값나가는 것이 위로 온다
    Descending,

    // 등급 낮은 순 — 정리해 팔 것을 위로 올릴 때
    Ascending,
}

// 인벤토리 [정렬]의 기준 (T-073). 도구 줄의 드롭다운이 고른다.
//
// ※ ▼(Descending)는 **기준마다의 기본 방향**이다 — 등급 높은 순 · 이름 가나다 순 · 수량 많은 순.
//   ▲는 그 결과를 통째로 뒤집는다.
public enum InventorySortKey
{
    // 등급 — 탭마다의 'CompareForSort' 규칙 그대로
    Rarity,

    // 이름 가나다 순
    Name,

    // 보유 수량 — 자원 탭 전용(캐릭터·장비는 개체라 늘 1이다)
    Count,
}

// 인벤토리 탭 하나가 격자에 무엇을 그릴지 답하는 공급자.
//
// 격자('InventoryGridPresenter')는 이 타입만 알고 자원인지 캐릭터인지는 모른다.
// 탭이 늘어도 격자·전환·잠금은 그대로고, 공급자 하나가 더 생길 뿐이다
// (절차는 'Inventory 규칙.md'의 "탭 하나를 채우는 절차").
//
// ■ 내용물은 매번 새로 만들고, **자리는 그대로 둔다** (2026-09-25 · T-044)
// 데이터가 바뀔 때마다 'Rebuild'가 내용물을 새로 채운다. 원본을 그때그때 인덱스로 훑지 않는 것은,
// 수량 0처럼 화면에서 빼야 하는 항목이 섞이면 원본 인덱스와 칸 인덱스가 어긋나기 때문이다.
// 다만 **채운 순서가 곧 칸 번호는 아니다** — 칸 번호는 'Key → 칸'으로 따로 기억한다('_place').
// 한때는 채운 순서를 그대로 칸에 썼는데, 그러면 장착·배치로 하나가 빠질 때마다
// **뒤의 것이 통째로 앞으로 당겨졌다.** 보던 자리가 매번 흔들린다.
//
// ■ 빈 칸은 빈 칸으로 남는다
// 개체가 인벤토리를 떠나면(장착·배치·판매) 그 칸은 **비어 있는 채로** 남고, 다음에 들어오는 것이
// **앞에서부터 세어 첫 빈 칸**을 차지한다. 나가고 들어오는 것이 서로의 자리를 밀지 않는다.
//
// ■ 정렬만이 자리를 다시 매긴다 (클라 임시)
// [정렬]을 누르면 'CompareForSort'로 줄 세우고 **그때 빈 칸도 함께 메운다** — 정리하려고 누르는
// 버튼이라 여기서는 앞으로 당겨지는 것이 맞다. 그 순서를 Key별 자리로 기억하고, 이후로는 다시
// 위 규칙을 따른다.
//
// ■ 나가 있는 개체는 **정렬하면 맨 뒤로 간다** (2026-09-25)
// 배치 중인 캐릭터·장착 중인 장비는 인벤토리에서 빠지지 않고 제자리에 남는다(딤 + '배' 마크).
// 다만 [정렬]은 "지금 손댈 수 있는 것을 위로"가 목적이라, 나가 있는 것은 뒤로 민다('IsAway').
// ⚠️ 이 기억은 **세션 한정**이다. 재접속하면 서버가 주는 순서로 처음부터 자리를 매긴다 —
//   칸 위치의 주인은 서버로 옮겨 간다(서버 'T-058' → 클라 'T-044').
//
// ■ 찾기(필터)는 자리를 건드리지 않는다 (2026-09-29 · T-069)
// 거르는 동안은 **맞는 것을 앞으로, 안 맞는 것을 그 뒤에** 각각 기억된 칸 순서대로 이어 붙이고,
// 안 맞는 것은 격자가 흑백으로 그린다('IsFilteredOut'). 자리 기억('_place')은 늘 전체 기준으로
// 계산하므로, 거르는 중에 채취·판매가 일어나도 필터를 풀면 원래 배치가 그대로 돌아온다.
// ※ 안 맞는 것을 **빼지 않는다** — 한때 맞는 것만 남겼더니 인벤토리가 텅 비어 보였다(2026-09-29 실측).
//   제자리에 두고 흐리게만 하면(WoW 가방식) 결과가 흩어져 스크롤로 찾아야 한다. 둘을 섞은 것이 이 방식이다.
public abstract class InventorySlotSource
{
    // 이번에 그릴 칸 배치. 인덱스가 곧 칸 번호이고, **null이면 빈 칸**이다.
    private readonly List<SlotData?> _slots = new List<SlotData?>();

    // 지금 인벤토리에 있는 것들. 'Fill'이 채운 순서가 곧 **빈 칸을 차지하는 순서**다.
    private readonly List<SlotData> _filled = new List<SlotData>();

    // 이번 배치에서 이미 찬 칸. 첫 빈 칸을 찾을 때 본다.
    private readonly HashSet<int> _taken = new HashSet<int>();

    // 자리를 아직 못 받은 것 — 기억에 없거나(새로 들어옴) 자리가 겹친 것. 앞에서부터 빈 칸에 넣는다.
    private readonly List<SlotData> _arrived = new List<SlotData>();

    // 정렬 비교자. 메서드 그룹을 매번 넘기면 호출마다 대리자가 새로 생겨서 한 번만 만든다.
    private readonly Comparison<SlotData> _byRule;

    // Key → 칸 번호. 한 번 정해지면 그 개체가 인벤토리를 떠날 때까지 바뀌지 않는다.
    //
    // ※ 매번 '_nextPlace'에 새로 담아 통째로 맞바꾼다 — 떠난 개체의 자리를 따로 지우지 않아도
    //   저절로 빠진다. 지우는 것을 잊으면 **아무도 못 쓰는 칸**이 계속 쌓인다.
    private Dictionary<long, int> _place     = new Dictionary<long, int>();
    private Dictionary<long, int> _nextPlace = new Dictionary<long, int>();

    // 마지막으로 누른 정렬 방향. 오름차순이면 기준 비교의 결과를 뒤집는다.
    private InventorySortOrder _order = InventorySortOrder.Descending;

    // 마지막으로 고른 정렬 기준.
    private InventorySortKey _key = InventorySortKey.Rarity;

    // 지금 찾기 조건. 기본값이면 거르지 않는다.
    private InventoryFilter _filter;

    // 거를 때 맞는 것 · 안 맞는 것을 칸 번호 순으로 모으는 버퍼. 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다).
    private readonly List<SlotData> _matched   = new List<SlotData>();
    private readonly List<SlotData> _unmatched = new List<SlotData>();

    // 지금 찾기 조건에 안 맞는 Key — 격자가 흑백으로 그린다.
    private readonly HashSet<long> _filteredOut = new HashSet<long>();

    private bool _isSubscribed;

    protected InventorySlotSource()
    {
        _byRule = CompareByRule;
    }

    // 그릴 칸 수 — **빈 칸을 포함한** 마지막 내용물까지의 길이다.
    // 뒤쪽이 통째로 비면 그만큼 줄어든다(그 자리는 격자가 빈 프레임으로 둔다).
    public int Count => _slots.Count;

    // 찾기 조건이 걸려 있나 — 결과 0건 안내를 띄울지 격자가 본다.
    public bool IsFiltering => _filter.IsActive;

    // 거른 결과 수. 거르지 않을 때는 전체 수다.
    public int MatchCount => _filter.IsActive ? _matched.Count : _filled.Count;

    // 이 개체가 지금 찾기 조건에 안 맞나 — 격자가 흑백으로 그린다 (Redraw에서 호출).
    public bool IsFilteredOut(long key) => _filteredOut.Contains(key);

    // 이 탭이 이 정렬 기준을 쓸 수 있나 (도구 줄이 드롭다운 목록을 만들 때 호출).
    // 기본은 등급·이름 — 보유 수량은 자원만 뜻이 있다.
    public virtual bool SupportsSortKey(InventorySortKey key) => key != InventorySortKey.Count;

    // 이 탭이 산업으로 거를 수 있나 (도구 줄이 산업 드롭다운을 보일지 정할 때 호출).
    // 캐릭터는 산업 축이 없다(적성이 다섯 산업에 걸쳐 있다).
    public virtual bool SupportsIndustryFilter => true;

    // 내용이 바뀌었다 — 격자가 다시 그린다.
    public event Action? Changed;

    // i번째 칸에 그릴 완성값 (격자가 호출). **빈 칸이면 null**이다.
    public SlotData? Get(int index) => _slots[index];

    // 이 개체가 지금 인벤토리 밖에 나가 있나 — 캐릭터는 작업슬롯 배치 중, 장비는 장착 중 (격자·정렬이 호출).
    //
    // ■ 판정의 주인을 공급자 하나로 둔다
    //   "나가 있다"의 뜻이 탭마다 다르다. 격자가 탭을 보고 분기하면 **딤·마크·정렬 세 군데에서
    //   같은 분기를 반복**하게 되고, 탭이 늘 때 한 곳만 빠진다.
    // ※ 자원은 나갈 곳이 없어 기본값 false 그대로다.
    public virtual bool IsAway(long key) => false;

    // 이 개체의 칸 툴팁 — 칸에 다 못 담은 것 (격자가 칸에 올린 순간 호출, T-050). 없으면 null이고 툴팁이 뜨지 않는다.
    //
    // ■ 'SlotData'에서 만들지 않는다
    //   그건 칸에 그릴 것만 담은 완성값이라 적성·배치처·판매가가 없다. 거기에 끼우면 가챠 결과 칸까지
    //   따라 두꺼워진다. 원본('PlayerDataModel'·테이블)을 쥔 공급자가 **띄우는 순간에** 읽는다.
    // ■ 격자가 아니라 여기인 이유는 'IsAway'와 같다 — 무엇을 보일지가 탭마다 통째로 다르다.
    public virtual TooltipContent? BuildTooltip(long key) => null;

    // 이 개체의 능력치 칸 — 길이 = 칸 수, 원소 = 박힌 등급('None'은 빈 칸) (격자가 매번 그릴 때 호출, T-095).
    // 능력치 칸이 없는 탭은 null이고, 칸은 줄을 끈다. 지금은 장비 탭만 값이 있다.
    // ※ 판단을 격자가 아니라 여기 두는 이유는 'IsAway'와 같다.
    public virtual IReadOnlyList<GlobalRarity>? GetStatSockets(long key) => null;

    // 툴팁 첫 줄 — 등급. 줄 바탕을 칸과 같은 등급색으로 칠한다 (공급자들의 'BuildTooltip'에서 호출).
    protected static TooltipContent AddRarityRow(TooltipContent content, GlobalRarity rarity)
        => content.Row("등급", RarityLabel.Get(rarity), "", RarityPalette.Get(rarity));

    // 데이터 변경 구독을 시작한다 (격자가 이 탭을 켤 때 호출).
    //
    // 목록도 함께 채운다 — 인벤토리를 닫아 둔 사이 채취·가챠로 내용이 바뀌었을 수 있다.
    public void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;
        OnSubscribe();
        Rebuild();
    }

    // 구독을 해제한다 (격자가 이 탭을 끌 때·파괴될 때 호출).
    public void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;
        OnUnsubscribe();
    }

    // 지금 내용을 기준대로 줄 세우고 그 자리를 기억한다 (격자의 'SortCurrent' — 도구 줄의 화살표·기준).
    //
    // **빈 칸은 여기서만 메워진다.** 정리하려고 누르는 버튼이라 앞으로 당겨지는 것이 맞다.
    // ※ 격자는 켜진 탭에만 부르므로 '_filled'는 구독 중에 채워진 최신값이다.
    // ※ 거르는 중이어도 **전체**를 줄 세운다 — 자리 기억은 늘 전체 기준이다(클래스 주석).
    public void Sort(InventorySortKey key, InventorySortOrder order)
    {
        _key   = SupportsSortKey(key) ? key : InventorySortKey.Rarity;
        _order = order;
        _filled.Sort(_byRule);

        _place.Clear();

        for (int i = 0; i < _filled.Count; i++)
        {
            _place[_filled[i].Key] = i;
        }

        Arrange();
        Changed?.Invoke();
    }

    // 찾기 조건을 바꾼다 (격자의 'FilterCurrent' — 도구 줄의 검색창·드롭다운).
    //
    // 자리 기억은 그대로 두고 보이는 것만 다시 모은다 — 필터를 풀면 원래 배치로 돌아와야 한다.
    public void SetFilter(InventoryFilter filter)
    {
        _filter = filter;

        Arrange();
        Changed?.Invoke();
    }

    // 기억한 자리를 버리고 받은 순서로 돌아간다 (격자가 로그인 성공 때 호출).
    //
    // 재접속하면 목록이 서버 순서로 새로 오는데, 지난 세션의 자리를 들고 있으면 다른 계정·다른 상태에 덮인다.
    public void ClearOrder()
    {
        _place.Clear();

        if (_isSubscribed)
        {
            Rebuild();
        }
    }

    // 내용을 다시 채우고 격자에 알린다 (파생 공급자가 데이터 변경 이벤트에서 호출).
    protected void Rebuild()
    {
        _filled.Clear();
        Fill(_filled);

        Arrange();
        Changed?.Invoke();
    }

    // 이 탭이 지금 인벤토리에 두고 있는 것들을 채운다 (Rebuild에서 호출).
    //
    // ⚠️ 여기 담는 순서는 **칸 번호가 아니라 "빈 칸을 고르는 순서"** 다 —
    //   이미 자리를 가진 것은 그 자리에 남고, 처음 보는 것만 이 순서대로 앞의 빈 칸을 가져간다.
    protected abstract void Fill(List<SlotData> into);

    // [정렬]의 규칙. a가 앞이면 음수 (Sort에서 호출).
    // ⚠️ 끝까지 가서 0이 나오지 않게 한다 — 'List.Sort'는 안정 정렬이 아니라 동점이면 누를 때마다 자리가 바뀐다.
    protected abstract int CompareForSort(SlotData a, SlotData b);

    // 보유 수량 비교 — 많은 것이 앞이면 음수. 수량 축이 없는 탭은 0이다 (CompareByRule에서 호출).
    protected virtual int CompareByCount(SlotData a, SlotData b) => 0;

    // 이 칸이 그 산업에 속하나 (Matches에서 호출). 산업 축이 없는 탭은 늘 true다.
    //   industry : 'IndustryType' 값 (0은 호출 전에 걸러진다)
    protected virtual bool MatchesIndustry(SlotData slot, byte industry) => true;

    // 데이터 변경 이벤트를 구독한다 (Subscribe에서 호출).
    protected abstract void OnSubscribe();

    // 구독을 해제한다 (Unsubscribe에서 호출).
    protected abstract void OnUnsubscribe();

    // '_filled'를 칸에 앉힌다 — 가진 자리는 지키고, 나머지는 앞에서부터 첫 빈 칸에 넣는다.
    //
    // 두 번에 나눠 도는 이유 — 먼저 **자리를 가진 것을 전부 앉혀야** 어디가 빈 칸인지 확정된다.
    // 한 번에 돌면서 새 항목을 끼워 넣으면, 뒤에 나오는 "원래 그 칸의 주인"과 부딪힌다.
    private void Arrange()
    {
        _taken.Clear();
        _arrived.Clear();
        _nextPlace.Clear();

        // 1) 이미 자리를 가진 것 — 떠난 개체의 자리는 '_nextPlace'에 실리지 않아 저절로 비워진다.
        foreach (SlotData slot in _filled)
        {
            // 'Add'가 false면 같은 칸을 둘이 주장한 것이다 — 뒤에 온 쪽을 새로 온 것으로 돌린다.
            if (_place.TryGetValue(slot.Key, out int position) && _taken.Add(position))
            {
                _nextPlace[slot.Key] = position;

                continue;
            }

            _arrived.Add(slot);
        }

        // 2) 처음 보는 것 — 앞에서부터 세어 첫 빈 칸에 넣는다.
        //    커서는 뒤로만 간다. 앞의 칸은 1)에서 이미 확정됐고, 여기서 준 칸도 곧바로 차기 때문이다.
        int cursor = 0;

        foreach (SlotData slot in _arrived)
        {
            while (_taken.Contains(cursor))
            {
                cursor++;
            }

            _nextPlace[slot.Key] = cursor;
            _taken.Add(cursor);
            cursor++;
        }

        (_place, _nextPlace) = (_nextPlace, _place);

        // 3) 칸 배치를 만든다. 빈 칸은 null로 남는다.
        int size = 0;

        foreach (int position in _taken)
        {
            if (position >= size)
            {
                size = position + 1;
            }
        }

        _slots.Clear();

        for (int i = 0; i < size; i++)
        {
            _slots.Add(null);
        }

        foreach (SlotData slot in _filled)
        {
            _slots[_place[slot.Key]] = slot;
        }

        _filteredOut.Clear();

        if (_filter.IsActive)
        {
            GatherMatches();
        }
    }

    // 거르는 중이면 맞는 것 → 안 맞는 것 순으로, 각각 칸 번호 순서대로 0번부터 다시 앉힌다 (Arrange 끝에서 호출).
    //
    // 자리 기억('_place')은 이미 전체 기준으로 끝난 뒤다 — 여기서는 보이는 배치('_slots')만 바꾼다.
    // 빈 칸은 이 동안 사라진다 — 모아 보이는 중이라 "어디서 빠졌나"를 읽을 자리가 아니다.
    private void GatherMatches()
    {
        _matched.Clear();
        _unmatched.Clear();

        foreach (SlotData? slot in _slots)
        {
            if (slot == null)
            {
                continue;
            }

            if (Matches(slot.Value))
            {
                _matched.Add(slot.Value);
            }
            else
            {
                _unmatched.Add(slot.Value);
                _filteredOut.Add(slot.Value.Key);
            }
        }

        _slots.Clear();

        foreach (SlotData slot in _matched)
        {
            _slots.Add(slot);
        }

        foreach (SlotData slot in _unmatched)
        {
            _slots.Add(slot);
        }
    }

    // 이 칸이 지금 찾기 조건에 맞나 — 이름 일부 · 등급 · 산업이 모두 맞아야 한다 (GatherMatches에서 호출).
    private bool Matches(SlotData slot)
    {
        if (!string.IsNullOrEmpty(_filter.Text) && slot.Name.IndexOf(_filter.Text, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (_filter.Rarity != GlobalRarity.None && slot.Rarity != _filter.Rarity)
        {
            return false;
        }

        return _filter.Industry == 0 || MatchesIndustry(slot, _filter.Industry);
    }

    // 규칙대로 비교한다. 오름차순이면 결과를 뒤집는다 (Sort의 정렬 비교자).
    //
    // ⚠️ **"나가 있는 것은 맨 뒤"만 방향을 타지 않는다** — 위 "오름차순은 규칙 전체를 뒤집는다"의
    //   유일한 예외다. 이건 값의 순위가 아니라 **덩어리 가르기**라서, 뒤집으면 ▲를 누를 때마다
    //   손댈 수 없는 것들이 맨 위를 차지한다. [정렬]의 목적("지금 쓸 수 있는 것을 위로")과 반대다.
    private int CompareByRule(SlotData a, SlotData b)
    {
        bool awayA = IsAway(a.Key);

        if (awayA != IsAway(b.Key))
        {
            return awayA ? 1 : -1;
        }

        int compared = CompareByKey(a, b);

        return _order == InventorySortOrder.Ascending ? -compared : compared;
    }

    // 고른 기준으로 비교하고, 동점이면 탭 고유 규칙('CompareForSort')으로 끝까지 가른다 (CompareByRule에서 호출).
    //
    // ※ 이름은 'SlotData.Name' 하나만 읽는다 — 표시 이름의 출처가 엑셀로 옮겨 가도(T-085) 칸 문구와 함께 따라온다.
    // ※ 'CompareOrdinal'로 충분하다 — 한글 음절은 유니코드 순서가 곧 가나다 순이고, 문화권에 따라 결과가 흔들리지 않는다.
    private int CompareByKey(SlotData a, SlotData b)
    {
        int byKey = _key switch
        {
            InventorySortKey.Name  => string.CompareOrdinal(a.Name, b.Name),
            InventorySortKey.Count => CompareByCount(a, b),
            _                    => 0,
        };

        return byKey != 0 ? byKey : CompareForSort(a, b);
    }
}
