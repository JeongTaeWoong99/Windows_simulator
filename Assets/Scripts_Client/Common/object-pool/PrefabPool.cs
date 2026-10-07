using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

// 프리팹 하나를 찍어 두고 돌려쓰는 풀 — 'UnityEngine.Pool.ObjectPool<T>'를 감싼다.
// 짧게 살다 사라지는 것(날아가는 아이콘·떠오르는 글자·이펙트)에 쓴다.
//
// ■ 목록 줄에는 쓰지 않는다 → 'UIRowList<T>'
//   이 풀은 꺼내고 되돌릴 때 부모를 옮긴다('inactiveRoot' ↔ 쓰는 자리). 레이아웃 그룹 아래의 줄은
//   옮길 때마다 양쪽 부모가 다시 배치되고, 스택이라 꺼내는 순서가 줄 순서와 어긋난다.
//
// ■ 쓰는 법
//   _pool = new PrefabPool<FlyIconView>(prefab, poolRoot);
//   var icon = _pool.Get(effectLayer);       // 켜지고 'IPoolable.OnRent'가 불린다
//   icon.GetComponent<PooledObject>().Release();   // 또는 _pool.Release(icon)
//   _pool.Dispose();                          // 주인의 OnDestroy에서
//
// ■ 기본값
//   defaultCapacity : 내부 스택의 처음 크기일 뿐 **오브젝트를 미리 만들지 않는다** → 미리 만들려면 'Prewarm'
//   maxSize         : 풀 안에 쌓아 둘 최대 수. 넘쳐서 돌아온 것은 파괴한다 — 동시 최대치의 2배쯤
//   collectionCheck : 에디터·개발 빌드에서만 켠다 (반납마다 스택을 훑는 비용)
public sealed class PrefabPool<T> : IDisposable where T : Component
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const bool CollectionCheck = true;
#else
    private const bool CollectionCheck = false;
#endif

    private readonly T         _prefab;
    private readonly Transform _inactiveRoot;
    private readonly ObjectPool<PooledObject> _pool;

    private bool _isDisposed;

    // 빌려 가서 아직 안 돌아온 수
    public int CountActive => _pool.CountActive;

    // 풀 안에서 기다리는 수
    public int CountInactive => _pool.CountInactive;

    //   prefab       : 찍어 낼 원본. 루트에 'T'가 있어야 한다
    //   inactiveRoot : 쉬는 오브젝트를 모아 둘 부모 — 꺼 둔 빈 오브젝트를 권한다(레이아웃 그룹 밖)
    public PrefabPool(T prefab, Transform inactiveRoot, int defaultCapacity = 8, int maxSize = 64)
    {
        _prefab       = prefab;
        _inactiveRoot = inactiveRoot;
        _pool         = new ObjectPool<PooledObject>(Create, null, OnRelease, OnDestroyItem, CollectionCheck, defaultCapacity, maxSize);
    }

    // 하나 꺼내 'parent' 아래에 켠다. 위치·크기는 프리팹 값으로 되돌려 준다.
    public T Get(Transform parent)
    {
        PooledObject pooled = _pool.Get();
        Transform    t      = pooled.transform;

        // ⚠️ worldPositionStays는 false — true면 부모의 스케일이 거꾸로 곱해져 크기가 어긋난다
        t.SetParent(parent, false);
        ResetTransform(t);

        pooled.gameObject.SetActive(true);
        pooled.HandleRent();

        return pooled.GetComponent<T>();
    }

    // 꺼낸 것을 되돌린다. 'PooledObject.Release()'와 같다.
    public void Release(T item)
    {
        if (item.TryGetComponent(out PooledObject pooled))
        {
            pooled.Release();
        }
    }

    // 미리 'count'개까지 만들어 풀에 넣어 둔다 — 첫 연출에서 생성 비용이 튀지 않게 (로딩 때 호출).
    public void Prewarm(int count)
    {
        int need = count - _pool.CountInactive;

        if (need <= 0)
        {
            return;
        }

        // 한꺼번에 꺼내야 새로 만들어진다 — 하나씩 꺼내고 되돌리면 같은 것만 돈다.
        // 켜지 않은 채 바로 되돌리므로 'IPoolable.OnRent' 없이 'OnReturn'만 불린다(그래서 OnReturn은 여러 번 불려도 안전해야 한다).
        var buffer = new PooledObject[need];

        for (int i = 0; i < need; i++)
        {
            buffer[i] = _pool.Get();
        }

        foreach (PooledObject pooled in buffer)
        {
            _pool.Release(pooled);
        }
    }

    // 쉬는 것을 모두 파괴하고, 빌려 간 것은 돌아올 곳을 끊는다 — 돌아오면 스스로 파괴된다 (주인의 OnDestroy에서 호출).
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _pool.Clear();
    }

    #region ObjectPool 콜백

    private PooledObject Create()
    {
        T instance = Object.Instantiate(_prefab, _inactiveRoot, false);

        instance.gameObject.SetActive(false);

        PooledObject pooled = instance.GetComponent<PooledObject>();

        if (pooled == null)
        {
            pooled = instance.gameObject.AddComponent<PooledObject>();
        }

        pooled.Bind(ReleaseFromItem);

        return pooled;
    }

    // 'PooledObject.Release'가 부르는 길 — 풀이 이미 정리됐으면 파괴로 끝낸다
    private void ReleaseFromItem(PooledObject pooled)
    {
        if (_isDisposed)
        {
            pooled.HandleReturn();
            pooled.Unbind();
            Object.Destroy(pooled.gameObject);

            return;
        }

        _pool.Release(pooled);
    }

    // 토큰 취소 → IPoolable.OnReturn(트윈 Kill·내용 비우기) → 끄기 → 쉬는 자리로
    private void OnRelease(PooledObject pooled)
    {
        pooled.HandleReturn();
        pooled.gameObject.SetActive(false);

        if (_inactiveRoot != null)
        {
            pooled.transform.SetParent(_inactiveRoot, false);
        }
    }

    // maxSize를 넘쳐 돌아왔거나 Clear될 때. 주인이 먼저 파괴돼 이미 없을 수 있다.
    private static void OnDestroyItem(PooledObject pooled)
    {
        if (pooled != null)
        {
            pooled.Unbind();
            Object.Destroy(pooled.gameObject);
        }
    }

    #endregion

    // 이전 대여에서 트윈이 옮겨 놓은 위치·크기를 프리팹 값으로 되돌린다
    private void ResetTransform(Transform t)
    {
        Transform source = _prefab.transform;

        t.localRotation = source.localRotation;
        t.localScale    = source.localScale;

        if (t is RectTransform rect && source is RectTransform sourceRect)
        {
            rect.anchorMin        = sourceRect.anchorMin;
            rect.anchorMax        = sourceRect.anchorMax;
            rect.pivot            = sourceRect.pivot;
            rect.sizeDelta        = sourceRect.sizeDelta;
            rect.anchoredPosition = sourceRect.anchoredPosition;

            return;
        }

        t.localPosition = source.localPosition;
    }
}
