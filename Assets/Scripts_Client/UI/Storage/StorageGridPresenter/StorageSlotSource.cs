using System;
using System.Collections.Generic;

// 창고 [정렬]의 방향. 도구 줄의 화살표 버튼이 둘을 오간다.
//
// ※ 오름차순은 **규칙 전체를 뒤집는다** — 등급만이 아니라 산업·TID 순서까지 반대가 된다.
//   기준을 하나만 뒤집으면 "무엇이 반대인가"를 매번 표로 확인해야 한다.
public enum StorageSortOrder
{
    // 등급 높은 순 (기본) — 값나가는 것이 위로 온다
    Descending,

    // 등급 낮은 순 — 정리해 팔 것을 위로 올릴 때
    Ascending,
}

// 창고 탭 하나가 격자에 무엇을 그릴지 답하는 공급자.
//
// 격자('StorageGridPresenter')는 이 타입만 알고 자원인지 캐릭터인지는 모른다.
// 탭이 늘어도 격자·전환·잠금은 그대로고, 공급자 하나가 더 생길 뿐이다
// (절차는 'Storage 규칙.md'의 "탭 하나를 채우는 절차").
//
// ■ 내용물은 매번 새로 만들고, **자리는 그대로 둔다** (2026-09-25 · T-044)
// 데이터가 바뀔 때마다 'Rebuild'가 내용물을 새로 채운다. 원본을 그때그때 인덱스로 훑지 않는 것은,
// 수량 0처럼 화면에서 빼야 하는 항목이 섞이면 원본 인덱스와 칸 인덱스가 어긋나기 때문이다.
// 다만 **채운 순서가 곧 칸 번호는 아니다** — 칸 번호는 'Key → 칸'으로 따로 기억한다('_place').
// 한때는 채운 순서를 그대로 칸에 썼는데, 그러면 장착·배치로 하나가 빠질 때마다
// **뒤의 것이 통째로 앞으로 당겨졌다.** 보던 자리가 매번 흔들린다.
//
// ■ 빈 칸은 빈 칸으로 남는다
// 개체가 창고를 떠나면(장착·배치·판매) 그 칸은 **비어 있는 채로** 남고, 다음에 들어오는 것이
// **앞에서부터 세어 첫 빈 칸**을 차지한다. 나가고 들어오는 것이 서로의 자리를 밀지 않는다.
//
// ■ 정렬만이 자리를 다시 매긴다 (클라 임시)
// [정렬]을 누르면 'CompareForSort'로 줄 세우고 **그때 빈 칸도 함께 메운다** — 정리하려고 누르는
// 버튼이라 여기서는 앞으로 당겨지는 것이 맞다. 그 순서를 Key별 자리로 기억하고, 이후로는 다시
// 위 규칙을 따른다.
//
// ■ 나가 있는 개체는 **정렬하면 맨 뒤로 간다** (2026-09-25)
// 배치 중인 캐릭터·장착 중인 장비는 창고에서 빠지지 않고 제자리에 남는다(딤 + '배' 마크).
// 다만 [정렬]은 "지금 손댈 수 있는 것을 위로"가 목적이라, 나가 있는 것은 뒤로 민다('IsAway').
// ⚠️ 이 기억은 **세션 한정**이다. 재접속하면 서버가 주는 순서로 처음부터 자리를 매긴다 —
//   칸 위치의 주인은 서버로 옮겨 간다(서버 'T-058' → 클라 'T-044').
public abstract class StorageSlotSource
{
    // 이번에 그릴 칸 배치. 인덱스가 곧 칸 번호이고, **null이면 빈 칸**이다.
    private readonly List<SlotData?> _slots = new List<SlotData?>();

    // 지금 창고에 있는 것들. 'Fill'이 채운 순서가 곧 **빈 칸을 차지하는 순서**다.
    private readonly List<SlotData> _filled = new List<SlotData>();

    // 이번 배치에서 이미 찬 칸. 첫 빈 칸을 찾을 때 본다.
    private readonly HashSet<int> _taken = new HashSet<int>();

    // 자리를 아직 못 받은 것 — 기억에 없거나(새로 들어옴) 자리가 겹친 것. 앞에서부터 빈 칸에 넣는다.
    private readonly List<SlotData> _arrived = new List<SlotData>();

    // 정렬 비교자. 메서드 그룹을 매번 넘기면 호출마다 대리자가 새로 생겨서 한 번만 만든다.
    private readonly Comparison<SlotData> _byRule;

    // Key → 칸 번호. 한 번 정해지면 그 개체가 창고를 떠날 때까지 바뀌지 않는다.
    //
    // ※ 매번 '_nextPlace'에 새로 담아 통째로 맞바꾼다 — 떠난 개체의 자리를 따로 지우지 않아도
    //   저절로 빠진다. 지우는 것을 잊으면 **아무도 못 쓰는 칸**이 계속 쌓인다.
    private Dictionary<long, int> _place     = new Dictionary<long, int>();
    private Dictionary<long, int> _nextPlace = new Dictionary<long, int>();

    // 마지막으로 누른 정렬 방향. 오름차순이면 'CompareForSort'의 결과를 뒤집는다.
    private StorageSortOrder _order = StorageSortOrder.Descending;

    private bool _isSubscribed;

    protected StorageSlotSource()
    {
        _byRule = CompareByRule;
    }

    // 그릴 칸 수 — **빈 칸을 포함한** 마지막 내용물까지의 길이다.
    // 뒤쪽이 통째로 비면 그만큼 줄어든다(그 자리는 격자가 빈 프레임으로 둔다).
    public int Count => _slots.Count;

    // 내용이 바뀌었다 — 격자가 다시 그린다.
    public event Action? Changed;

    // i번째 칸에 그릴 완성값 (격자가 호출). **빈 칸이면 null**이다.
    public SlotData? Get(int index) => _slots[index];

    // 이 개체가 지금 창고 밖에 나가 있나 — 캐릭터는 작업슬롯 배치 중, 장비는 장착 중 (격자·정렬이 호출).
    //
    // ■ 판정의 주인을 공급자 하나로 둔다
    //   "나가 있다"의 뜻이 탭마다 다르다. 격자가 탭을 보고 분기하면 **딤·마크·정렬 세 군데에서
    //   같은 분기를 반복**하게 되고, 탭이 늘 때 한 곳만 빠진다.
    // ※ 자원은 나갈 곳이 없어 기본값 false 그대로다.
    public virtual bool IsAway(long key) => false;

    // 데이터 변경 구독을 시작한다 (격자가 이 탭을 켤 때 호출).
    //
    // 목록도 함께 채운다 — 창고를 닫아 둔 사이 채취·가챠로 내용이 바뀌었을 수 있다.
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

    // 지금 내용을 규칙대로 줄 세우고 그 자리를 기억한다 (격자의 'SortCurrent' — 도구 줄의 화살표 버튼).
    //
    // **빈 칸은 여기서만 메워진다.** 정리하려고 누르는 버튼이라 앞으로 당겨지는 것이 맞다.
    // ※ 격자는 켜진 탭에만 부르므로 '_filled'는 구독 중에 채워진 최신값이다.
    public void Sort(StorageSortOrder order)
    {
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

    // 이 탭이 지금 창고에 두고 있는 것들을 채운다 (Rebuild에서 호출).
    //
    // ⚠️ 여기 담는 순서는 **칸 번호가 아니라 "빈 칸을 고르는 순서"** 다 —
    //   이미 자리를 가진 것은 그 자리에 남고, 처음 보는 것만 이 순서대로 앞의 빈 칸을 가져간다.
    protected abstract void Fill(List<SlotData> into);

    // [정렬]의 규칙. a가 앞이면 음수 (Sort에서 호출).
    // ⚠️ 끝까지 가서 0이 나오지 않게 한다 — 'List.Sort'는 안정 정렬이 아니라 동점이면 누를 때마다 자리가 바뀐다.
    protected abstract int CompareForSort(SlotData a, SlotData b);

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

        int compared = CompareForSort(a, b);

        return _order == StorageSortOrder.Ascending ? -compared : compared;
    }
}
