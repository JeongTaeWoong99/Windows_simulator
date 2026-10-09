using System;
using System.Threading;
using UnityEngine;

// 풀에서 나온 오브젝트에 붙는 표식 — 'PrefabPool<T>'가 만들 때 붙인다. 직접 붙이지 않는다.
//
// ■ 하는 일
//   - 자기를 만든 풀을 기억한다 → 'Release()' 한 줄로 스스로 돌아간다 (풀을 찾을 필요가 없다)
//   - 빌려 간 동안만 살아 있는 취소 토큰('RentToken')을 준다 → 반납하면 그 토큰이 취소된다
//   - 이중 반납을 막는다 → 이미 돌아온 오브젝트의 'Release()'는 아무것도 하지 않는다
//
// ■ 대여 토큰이 필요한 이유
//   풀링된 오브젝트는 반납돼도 파괴되지 않는다. 'GetCancellationTokenOnDestroy'에 묶은 작업은
//   반납 뒤에도 계속 돌아, 다른 용도로 다시 꺼내진 오브젝트를 이전 작업이 만진다.
//   'RentToken'은 반납 · 파괴 둘 중 먼저 오는 쪽에서 취소된다.
[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    private Action<PooledObject>? _release;
    private IPoolable[]           _poolables = Array.Empty<IPoolable>();
    private CancellationTokenSource? _rentCts;

    // 지금 빌려 간 상태인가 (풀 안에 있으면 false)
    public bool IsRented { get; private set; }

    // 꺼낼 때마다 1씩 오른다 — await 뒤에 "아직 같은 대여인가"를 비교하는 보조 방어용
    public int RentVersion { get; private set; }

    // 이번 대여 동안만 유효한 취소 토큰. 반납하거나 파괴되면 취소된다.
    // 풀 밖(대여 전·반납 후)에서 읽으면 이미 취소된 토큰을 준다 — 새 작업이 시작되지 않게.
    public CancellationToken RentToken => _rentCts?.Token ?? new CancellationToken(true);

    // 이 오브젝트를 풀로 되돌린다. 이미 돌아와 있으면 아무것도 하지 않는다 (이중 반납 방어).
    //
    // ※ 경고를 찍지 않는다 — "연출 끝에 finally에서 반납"하는 정상 경로가 이중 호출을 낳는다:
    //   밖에서 먼저 반납 → 대여 토큰 취소 → 기다리던 연출이 취소로 깨어나 finally에서 또 반납.
    public void Release()
    {
        if (!IsRented)
        {
            return;
        }

        if (_release == null)
        {
            // 풀이 사라진 뒤(풀 주인이 파괴됨)에 남은 오브젝트 — 돌아갈 곳이 없으니 스스로 지운다
            Destroy(gameObject);

            return;
        }

        _release(this);
    }

    // 풀이 만들 때 한 번 부른다 ('PrefabPool<T>.Create').
    internal void Bind(Action<PooledObject> release)
    {
        _release   = release;
        _poolables = GetComponentsInChildren<IPoolable>(true);
    }

    // 풀이 사라질 때 부른다 — 대여 중인 오브젝트가 없는 풀로 돌아가지 않게 끊는다 ('PrefabPool<T>.Dispose').
    internal void Unbind()
    {
        _release = null;
    }

    // 꺼낸 직후 (PrefabPool<T>.Get). 켜진 뒤에 불러야 IPoolable이 켜진 상태를 전제로 초기화할 수 있다.
    internal void HandleRent()
    {
        IsRented = true;
        RentVersion++;

        // 파괴 토큰과 이어 둔다 — 대여 중에 씬이 내려가도 작업이 멈춘다
        _rentCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        foreach (IPoolable poolable in _poolables)
        {
            poolable.OnRent();
        }
    }

    // 되돌리기 직전 (PrefabPool<T>.OnRelease). 토큰을 먼저 끊고 나서 IPoolable에 알린다 —
    // 'OnReturn'에서 트윈을 Kill할 때 그 트윈을 기다리던 작업이 취소로 끝나게.
    internal void HandleReturn()
    {
        IsRented = false;

        CancelRent();

        foreach (IPoolable poolable in _poolables)
        {
            poolable.OnReturn();
        }
    }

    private void CancelRent()
    {
        if (_rentCts == null)
        {
            return;
        }

        _rentCts.Cancel();
        _rentCts.Dispose();
        _rentCts = null;
    }

    // 파괴 (Unity 메시지)
    private void OnDestroy()
    {
        CancelRent();
    }
}
