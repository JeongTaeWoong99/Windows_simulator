using System;
using System.Collections.Generic;

// 창고 탭 하나가 격자에 무엇을 그릴지 답하는 공급자.
//
// 격자('StorageGridPresenter')는 이 타입만 알고 자원인지 캐릭터인지는 모른다.
// 탭이 늘어도 격자·전환·잠금은 그대로고, 공급자 하나가 더 생길 뿐이다
// (절차는 'Storage 규칙.md'의 "탭 하나를 채우는 절차").
//
// ■ 목록을 통째로 다시 만드는 이유
// 데이터가 바뀔 때마다 'Rebuild'가 목록을 새로 채운다. 원본을 그때그때 인덱스로 훑지 않는 것은,
// 수량 0처럼 화면에서 빼야 하는 항목이 섞이면 원본 인덱스와 칸 인덱스가 어긋나기 때문이다.
// 걸러 내기·정렬이 붙을 자리도 여기다 — 격자는 받은 순서대로 앞 칸부터 그린다.
public abstract class StorageSlotSource
{
    // 이번에 그릴 칸들. 'Rebuild'만 갈아 끼운다.
    private readonly List<SlotData> _slots = new List<SlotData>();

    private bool _isSubscribed;

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

    // 목록을 다시 채우고 격자에 알린다 (파생 공급자가 데이터 변경 이벤트에서 호출).
    protected void Rebuild()
    {
        _slots.Clear();
        Fill(_slots);

        Changed?.Invoke();
    }

    // 이 탭이 그릴 칸들을 순서대로 채운다 (Rebuild에서 호출).
    // 여기 담기는 순서가 곧 화면의 칸 순서다.
    protected abstract void Fill(List<SlotData> into);

    // 데이터 변경 이벤트를 구독한다 (Subscribe에서 호출).
    protected abstract void OnSubscribe();

    // 구독을 해제한다 (Unsubscribe에서 호출).
    protected abstract void OnUnsubscribe();
}
