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
// ■ 목록을 통째로 다시 만드는 이유
// 데이터가 바뀔 때마다 'Rebuild'가 목록을 새로 채운다. 원본을 그때그때 인덱스로 훑지 않는 것은,
// 수량 0처럼 화면에서 빼야 하는 항목이 섞이면 원본 인덱스와 칸 인덱스가 어긋나기 때문이다.
//
// ■ 정렬 — 누른 순간의 순서를 기억한다 (클라 임시)
// [정렬]을 누르면 'CompareForSort'로 줄 세우고 **그 순서를 Key별 자리로 기억한다.**
// 이후 채취·판매로 목록이 다시 채워져도 기억한 자리를 따르고, 처음 보는 항목은 도착 순서대로 뒤에 붙는다.
// 매번 규칙으로 다시 줄 세우지 않는 이유 — 새 자원이 들어올 때마다 칸이 뒤섞이면 보던 자리를 잃는다.
// ⚠️ 이 기억은 세션 한정이다. 칸 위치의 주인은 서버로 옮겨 간다(서버 'T-058' → 클라 'T-044').
public abstract class StorageSlotSource
{
    // 이번에 그릴 칸들. 'Rebuild'·'Sort'만 갈아 끼운다.
    private readonly List<SlotData> _slots = new List<SlotData>();

    // [정렬]로 정해진 자리. Key → 칸 순번. 비어 있으면 받은 순서 그대로다.
    private readonly Dictionary<long, int> _rank = new Dictionary<long, int>();

    // 기억에 없는 항목을 도착 순서대로 잠시 담는 버퍼 — 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다).
    private readonly List<SlotData> _unranked = new List<SlotData>();

    // 정렬 비교자. 메서드 그룹을 매번 넘기면 호출마다 대리자가 새로 생겨서 한 번만 만든다.
    private readonly Comparison<SlotData> _byRule;
    private readonly Comparison<SlotData> _byRank;

    // 마지막으로 누른 정렬 방향. 오름차순이면 'CompareForSort'의 결과를 뒤집는다.
    private StorageSortOrder _order = StorageSortOrder.Descending;

    private bool _isSubscribed;

    protected StorageSlotSource()
    {
        _byRule = CompareByRule;
        _byRank = CompareByRank;
    }

    // 그릴 칸 수.
    public int Count => _slots.Count;

    // 내용이 바뀌었다 — 격자가 다시 그린다.
    public event Action? Changed;

    // i번째 칸에 그릴 완성값 (격자가 호출).
    public SlotData Get(int index) => _slots[index];

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

    // 지금 목록을 규칙대로 줄 세우고 그 자리를 기억한다 (격자의 'SortCurrent' — 도구 줄의 화살표 버튼).
    //
    // ※ 격자는 켜진 탭에만 부르므로 목록은 구독 중에 채워진 최신값이다.
    public void Sort(StorageSortOrder order)
    {
        _order = order;
        _slots.Sort(_byRule);

        _rank.Clear();

        for (int i = 0; i < _slots.Count; i++)
        {
            _rank[_slots[i].Key] = i;
        }

        Changed?.Invoke();
    }

    // 기억한 자리를 버리고 받은 순서로 돌아간다 (격자가 로그인 성공 때 호출).
    //
    // 재접속하면 목록이 서버 순서로 새로 오는데, 지난 세션의 자리를 들고 있으면 다른 계정·다른 상태에 덮인다.
    public void ClearOrder()
    {
        _rank.Clear();

        if (_isSubscribed)
        {
            Rebuild();
        }
    }

    // 목록을 다시 채우고 격자에 알린다 (파생 공급자가 데이터 변경 이벤트에서 호출).
    protected void Rebuild()
    {
        _slots.Clear();
        Fill(_slots);

        if (_rank.Count > 0)
        {
            ApplyRememberedOrder();
        }

        Changed?.Invoke();
    }

    // 이 탭이 그릴 칸들을 순서대로 채운다 (Rebuild에서 호출).
    // 여기 담기는 순서가 곧 화면의 칸 순서다 — [정렬]을 누르기 전까지는.
    protected abstract void Fill(List<SlotData> into);

    // [정렬]의 규칙. a가 앞이면 음수 (Sort에서 호출).
    // ⚠️ 끝까지 가서 0이 나오지 않게 한다 — 'List.Sort'는 안정 정렬이 아니라 동점이면 누를 때마다 자리가 바뀐다.
    protected abstract int CompareForSort(SlotData a, SlotData b);

    // 데이터 변경 이벤트를 구독한다 (Subscribe에서 호출).
    protected abstract void OnSubscribe();

    // 구독을 해제한다 (Unsubscribe에서 호출).
    protected abstract void OnUnsubscribe();

    // 기억한 자리대로 앞에 두고, 처음 보는 항목은 도착 순서대로 뒤에 붙인다 (Rebuild에서 호출).
    //
    // ※ 둘을 한 비교자로 섞지 않는다 — 'List.Sort'가 안정 정렬이 아니라서 새 항목끼리의 도착 순서가 흐트러진다.
    //   자리는 Key마다 하나라 기억한 쪽끼리는 동점이 없다.
    private void ApplyRememberedOrder()
    {
        _unranked.Clear();

        int kept = 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            SlotData slot = _slots[i];

            if (_rank.ContainsKey(slot.Key))
            {
                _slots[kept] = slot;
                kept++;
            }
            else
            {
                _unranked.Add(slot);
            }
        }

        _slots.RemoveRange(kept, _slots.Count - kept);
        _slots.Sort(_byRank);
        _slots.AddRange(_unranked);
    }

    // 규칙대로 비교한다. 오름차순이면 결과를 뒤집는다 (Sort의 정렬 비교자).
    private int CompareByRule(SlotData a, SlotData b)
    {
        int compared = CompareForSort(a, b);

        return _order == StorageSortOrder.Ascending ? -compared : compared;
    }

    // 기억한 자리끼리 비교한다 (ApplyRememberedOrder의 정렬 비교자).
    private int CompareByRank(SlotData a, SlotData b)
    {
        return _rank[a.Key].CompareTo(_rank[b.Key]);
    }
}
