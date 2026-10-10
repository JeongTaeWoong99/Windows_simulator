using System;
using System.Collections.Generic;
using GameData;

// 인벤토리 [정렬]의 방향. 도구 줄의 화살표 버튼이 둘을 오간다. 서버 'EStorageSortOrder'와 값이 같다.
//
// ※ 오름차순은 **규칙 전체를 뒤집는다** — 등급만이 아니라 산업·TID 순서까지 반대가 된다(서버 'StorageSort').
//   기준을 하나만 뒤집으면 "무엇이 반대인가"를 매번 표로 확인해야 한다.
public enum InventorySortOrder
{
    // 등급 높은 순 (기본) — 값나가는 것이 위로 온다
    Descending,

    // 등급 낮은 순 — 정리해 팔 것을 위로 올릴 때
    Ascending,
}

// 인벤토리 [정렬]의 기준 (T-073). 도구 줄의 드롭다운이 고른다. 서버 'EStorageSortKey'와 값이 같다.
//
// ※ ▼(Descending)는 **기준마다의 기본 방향**이다 — 등급 높은 순 · 이름 가나다 순 · 수량 많은 순.
//   ▲는 그 결과를 통째로 뒤집는다.
public enum InventorySortKey
{
    // 등급 — 탭마다의 규칙(등급 → 산업·종류 → 번호)은 서버 'StorageSort'에 있다
    Rarity,

    // 이름 가나다 순
    Name,

    // 보유 수량 — 자원 탭 전용(캐릭터·장비는 개체라 늘 1이다)
    Count,
}

// 칸 하나와 그 칸의 서버 번호. 공급자가 'Fill'에서 담는다.
//
// ※ 'SlotData'에 칸 번호를 끼우지 않는다 — 가챠 결과 칸도 'SlotData'를 쓰는데 거기에는 칸 번호라는 개념이 없다.
public readonly struct PlacedSlot
{
    public readonly SlotData Data;
    public readonly int      Slot;   // 서버가 정한 격자 칸 번호(0부터) — 'ItemInfo.Slot' 등

    public PlacedSlot(in SlotData data, int slot)
    {
        Data = data;
        Slot = slot;
    }
}

// 인벤토리 탭 하나가 격자에 무엇을 그릴지 답하는 공급자.
//
// 격자('InventoryGridPresenter')는 이 타입만 알고 자원인지 캐릭터인지는 모른다.
// 탭이 늘어도 격자·전환·잠금은 그대로고, 공급자 하나가 더 생길 뿐이다
// (절차는 'Inventory 규칙.md'의 "탭 하나를 채우는 절차").
//
// ■ 칸 번호의 주인은 서버다 (2026-10-06 · T-044)
// 각 개체는 서버가 정한 칸 번호('ItemInfo.Slot' · 'CharacterInfo.Slot' · 'EquipInfo.SlotPosition')를 들고 온다.
// 공급자는 그 번호에 그대로 앉힐 뿐 **자리를 스스로 정하지 않는다** — 그래서 재접속해도 자리가 남는다.
// 떠난 칸은 비어 있는 채로 남고, 새로 들어오는 것은 서버가 첫 빈 칸을 준다(서버 'StorageSlots').
// ※ 한때(2026-09-25 ~ 10-06) 클라가 'Key → 칸'을 세션 동안만 기억했다. 서버 칸(T-058)이 오면서 걷어냈다.
//
// ■ [정렬]·칸 이동도 서버가 한다
// 격자가 요청을 보내고, 응답이 오면 'PlayerDataModel'이 칸 번호를 덮어쓴 뒤 변경 이벤트를 낸다 —
// 공급자는 그 이벤트로 다시 채울 뿐이다. 정렬 규칙도 서버에 있다('Server/docs/인벤토리-창고.md').
//
// ■ 나가 있는 개체도 제자리에 남는다 (2026-09-25)
// 배치 중인 캐릭터·장착 중인 장비는 인벤토리에서 빠지지 않는다(딤 + '배' 마크 — 'IsAway').
// [정렬]하면 서버가 맨 뒤로 민다.
//
// ■ 찾기(필터)는 자리를 건드리지 않는다 (2026-09-29 · T-069)
// 거르는 동안은 **맞는 것을 앞으로, 안 맞는 것을 그 뒤에** 각각 칸 순서대로 이어 붙이고,
// 안 맞는 것은 격자가 흑백으로 그린다('IsFilteredOut'). 칸 번호는 그대로라 필터를 풀면 원래 배치가 돌아온다.
// ⚠️ 그래서 거르는 동안은 **보이는 순서 ≠ 칸 번호**다 — 격자는 이때 칸 끌기를 막는다.
// ※ 안 맞는 것을 **빼지 않는다** — 한때 맞는 것만 남겼더니 인벤토리가 텅 비어 보였다(2026-09-29 실측).
//   제자리에 두고 흐리게만 하면(WoW 가방식) 결과가 흩어져 스크롤로 찾아야 한다. 둘을 섞은 것이 이 방식이다.
//
// ■ 획득 감지 (2026-10-10)
// 'Rebuild'마다 개체별 수량('AmountOf')을 직전과 비교해 **늘었거나 새로 생긴 Key**를 모은다 — 격자가 그 칸을 그릴 때
// 한 번 꺼내 반짝인다('TakeGain' → 'SlotGainShine'). 정렬·칸 이동·찾기·강화는 수량이 그대로라 반짝이지 않는다.
// - 직전 기록이 비어 있으면(처음 채움 · 로그인 전 빈 목록) 전부 새것이라 건너뛴다. 그래서 다 팔아 비운 직후의 첫 획득은 반짝이지 않는다.
// - 화면 밖 칸은 기록만 남았다가 스크롤로 들어올 때 반짝인다. 탭을 떠나 있던 동안 들어온 것도 돌아와 다시 채울 때 반짝인다
//   (떠날 때 'Unsubscribe'가 기록을 지우지 않는다).
// - 빠진 개체의 남은 표시는 버린다 — 번호를 다시 쓰는 개체가 엉뚱하게 반짝이지 않게.
public abstract class InventorySlotSource
{
    // 이번에 그릴 칸 배치. 인덱스가 곧 칸 번호이고, **null이면 빈 칸**이다.
    private readonly List<SlotData?> _slots = new List<SlotData?>();

    // 지금 인벤토리에 있는 것들과 그 칸 번호.
    private readonly List<PlacedSlot> _filled = new List<PlacedSlot>();

    // 이번 배치에서 이미 찬 칸. 칸이 겹쳤을 때 첫 빈 칸을 찾으려고 본다.
    private readonly HashSet<int> _taken = new HashSet<int>();

    // 서버 칸 번호가 겹치거나 음수라 제자리에 못 앉은 것. 정상이면 비어 있다.
    private readonly List<SlotData> _stray = new List<SlotData>();

    // 지금 찾기 조건. 기본값이면 거르지 않는다.
    private InventoryFilter _filter;

    // 거를 때 맞는 것 · 안 맞는 것을 칸 번호 순으로 모으는 버퍼. 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다).
    private readonly List<SlotData> _matched   = new List<SlotData>();
    private readonly List<SlotData> _unmatched = new List<SlotData>();

    // 지금 찾기 조건에 안 맞는 Key — 격자가 흑백으로 그린다.
    private readonly HashSet<long> _filteredOut = new HashSet<long>();

    // 획득 감지 — 직전 'Rebuild'의 Key별 수량과, 아직 격자가 꺼내 가지 않은 획득 Key
    private Dictionary<long, int> _amounts     = new Dictionary<long, int>();
    private Dictionary<long, int> _amountsNext = new Dictionary<long, int>();
    private readonly HashSet<long> _gains      = new HashSet<long>();
    private readonly List<long>    _goneGains  = new List<long>();

    private bool _isSubscribed;

    // 그릴 칸 수 — **빈 칸을 포함한** 마지막 내용물까지의 길이다.
    // 뒤쪽이 통째로 비면 그만큼 줄어든다(그 자리는 격자가 빈 프레임으로 둔다).
    public int Count => _slots.Count;

    // 찾기 조건이 걸려 있나 — 결과 0건 안내를 띄울지, 칸 끌기를 막을지 격자가 본다.
    public bool IsFiltering => _filter.IsActive;

    // 거른 결과 수. 거르지 않을 때는 전체 수다.
    public int MatchCount => _filter.IsActive ? _matched.Count : _filled.Count;

    // 이 개체가 지금 찾기 조건에 안 맞나 — 격자가 흑백으로 그린다 (Redraw에서 호출).
    public bool IsFilteredOut(long key) => _filteredOut.Contains(key);

    // 이 개체가 방금 들어왔거나 늘었나 — 맞으면 표시를 지우고 true (격자가 칸을 그릴 때 호출 · 칸마다 한 번만 반짝인다).
    public bool TakeGain(long key) => _gains.Remove(key);

    // 이 탭이 이 정렬 기준을 쓸 수 있나 (도구 줄이 드롭다운 목록을 만들 때 호출).
    // 기본은 등급·이름 — 보유 수량은 자원만 뜻이 있다. 서버도 같은 판정으로 거절한다('InvalidStorageSortKey').
    public virtual bool SupportsSortKey(InventorySortKey key) => key != InventorySortKey.Count;

    // 이 탭이 산업으로 거를 수 있나 (도구 줄이 산업 드롭다운을 보일지 정할 때 호출).
    // 캐릭터는 산업 축이 없다(적성이 다섯 산업에 걸쳐 있다).
    public virtual bool SupportsIndustryFilter => true;

    // 이 탭이 산업 아닌 묶음(상자 · 기타)으로 거를 수 있나 (도구 줄이 분류 드롭다운 항목을 만들 때 호출). 자원 탭만 된다.
    public virtual bool SupportsGroupFilter => false;

    // 내용이 바뀌었다 — 격자가 다시 그린다.
    public event Action? Changed;

    // i번째 칸에 그릴 완성값 (격자가 호출). **빈 칸이면 null**이다.
    public SlotData? Get(int index) => _slots[index];

    // 이 개체가 지금 인벤토리 밖에 나가 있나 — 캐릭터는 작업슬롯 배치 중, 장비는 장착 중 (격자가 호출).
    //
    // ■ 판정의 주인을 공급자 하나로 둔다
    //   "나가 있다"의 뜻이 탭마다 다르다. 격자가 탭을 보고 분기하면 **딤·마크 두 군데에서
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

    // 이 개체가 낀 장비 4칸의 등급 — 'EquipLabel.WornSlots' 순서, 빈 칸은 'None' (격자가 매번 그릴 때 호출, T-104).
    // 장비를 끼지 않는 탭은 null이고, 칸은 장착 네모를 끈다. 지금은 캐릭터 탭만 값이 있다.
    public virtual IReadOnlyList<GlobalRarity>? GetWornEquips(long key) => null;

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

    // 찾기 조건을 바꾼다 (격자의 'FilterCurrent' — 도구 줄의 검색창·드롭다운).
    //
    // 칸 번호는 그대로 두고 보이는 것만 다시 모은다 — 필터를 풀면 원래 배치로 돌아와야 한다.
    public void SetFilter(InventoryFilter filter)
    {
        _filter = filter;

        Arrange();
        Changed?.Invoke();
    }

    // 내용을 다시 채우고 격자에 알린다 (파생 공급자가 데이터 변경 이벤트에서 호출).
    protected void Rebuild()
    {
        _filled.Clear();
        Fill(_filled);

        DetectGains();
        Arrange();
        Changed?.Invoke();
    }

    // 이 탭이 지금 인벤토리에 두고 있는 것들을 **서버 칸 번호와 함께** 채운다 (Rebuild에서 호출).
    // ※ 담는 순서는 상관없다 — 자리는 칸 번호가 정한다.
    protected abstract void Fill(List<PlacedSlot> into);

    // 이 개체의 수량 — 획득 감지가 직전과 비교한다 (DetectGains에서 호출).
    // 개체(캐릭터·장비)는 늘 1이라 "새로 생겼나"만 본다. 자원만 보유 수량으로 덮어쓴다.
    protected virtual int AmountOf(long key) => 1;

    // 이 칸이 그 산업에 속하나 (Matches에서 호출). 산업 축이 없는 탭은 늘 true다.
    //   industry : 'IndustryType' 값 (0은 호출 전에 걸러진다)
    protected virtual bool MatchesIndustry(SlotData slot, byte industry) => true;

    // 이 칸이 그 묶음에 속하나 (Matches에서 호출). 묶음 축이 없는 탭은 늘 true다.
    //   group : 'None'은 호출 전에 걸러진다
    protected virtual bool MatchesGroup(SlotData slot, InventoryItemGroup group) => true;

    // 데이터 변경 이벤트를 구독한다 (Subscribe에서 호출).
    protected abstract void OnSubscribe();

    // 구독을 해제한다 (Unsubscribe에서 호출).
    protected abstract void OnUnsubscribe();

    // 직전 수량과 비교해 늘었거나 새로 생긴 Key를 '_gains'에 모은다 (Rebuild에서 호출 — 머리 주석 '획득 감지').
    private void DetectGains()
    {
        bool hasBaseline = _amounts.Count > 0;

        _amountsNext.Clear();

        foreach (PlacedSlot placed in _filled)
        {
            long key    = placed.Data.Key;
            int  amount = AmountOf(key);

            _amountsNext[key] = amount;

            if (hasBaseline && (!_amounts.TryGetValue(key, out int before) || amount > before))
            {
                _gains.Add(key);
            }
        }

        // 빠진 개체의 남은 표시를 버린다
        if (_gains.Count > 0)
        {
            _goneGains.Clear();

            foreach (long key in _gains)
            {
                if (!_amountsNext.ContainsKey(key))
                {
                    _goneGains.Add(key);
                }
            }

            foreach (long key in _goneGains)
            {
                _gains.Remove(key);
            }
        }

        // 두 사전을 맞바꿔 쓴다 — 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다)
        (_amounts, _amountsNext) = (_amountsNext, _amounts);
    }

    // '_filled'를 서버 칸 번호에 앉힌다 (Rebuild · SetFilter에서 호출).
    //
    // ⚠️ 칸이 겹치거나 번호가 음수면 서버와 어긋난 것이다 — 버리지 않고 첫 빈 칸에 앉히고 경고를 남긴다.
    //   버리면 "아이템이 사라졌다"로 읽힌다. 다음 동기화(정렬·재접속)가 서버 자리로 되돌린다.
    private void Arrange()
    {
        _taken.Clear();
        _stray.Clear();
        _slots.Clear();

        foreach (PlacedSlot placed in _filled)
        {
            if (placed.Slot < 0 || !_taken.Add(placed.Slot))
            {
                _stray.Add(placed.Data);

                continue;
            }

            SetSlot(placed.Slot, placed.Data);
        }

        if (_stray.Count > 0)
        {
            PlaceStrays();
        }

        _filteredOut.Clear();

        if (_filter.IsActive)
        {
            GatherMatches();
        }
    }

    // 제자리에 못 앉은 것을 앞에서부터 첫 빈 칸에 넣는다 (Arrange에서 호출 — 정상이면 불리지 않는다).
    private void PlaceStrays()
    {
        int cursor = 0;

        foreach (SlotData slot in _stray)
        {
            while (_taken.Contains(cursor))
            {
                cursor++;
            }

            SetSlot(cursor, slot);
            _taken.Add(cursor);
        }

        ClientLogger.Warn(ClientLogger.UI,
            $"{GetType().Name} — 서버 칸 번호가 겹치거나 비정상인 항목 {_stray.Count}개를 빈 칸에 임시로 앉혔다. 칸 이동이 엉뚱한 자리를 가리킬 수 있다.");
    }

    // index번 칸에 놓는다 — 배치가 짧으면 빈 칸(null)으로 늘린다 (Arrange · PlaceStrays에서 호출).
    private void SetSlot(int index, in SlotData data)
    {
        while (_slots.Count <= index)
        {
            _slots.Add(null);
        }

        _slots[index] = data;
    }

    // 거르는 중이면 맞는 것 → 안 맞는 것 순으로, 각각 칸 번호 순서대로 0번부터 다시 앉힌다 (Arrange 끝에서 호출).
    //
    // 칸 번호는 그대로다 — 여기서는 보이는 배치('_slots')만 바꾼다.
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

    // 이 칸이 지금 찾기 조건에 맞나 — 이름 일부 · 등급 · 산업 · 묶음이 모두 맞아야 한다 (GatherMatches에서 호출).
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

        if (_filter.Group != InventoryItemGroup.None && !MatchesGroup(slot, _filter.Group))
        {
            return false;
        }

        return _filter.Industry == 0 || MatchesIndustry(slot, _filter.Industry);
    }
}
