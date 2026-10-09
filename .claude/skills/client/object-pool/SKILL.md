---
name: object-pool
description: 런타임에 자주 생기고 사라지는 오브젝트를 Common/object-pool(PrefabPool·PooledObject·IPoolable·UIRowList)로 돌려쓰는 규칙 — 무엇을 풀링할지, 반납·초기화·대여 토큰·UI 함정. Instantiate/Destroy를 새로 쓰거나(연출·이펙트·목록 줄), 생성·삭제가 잦은 화면을 고치거나, 풀에서 꺼낸 오브젝트에 트윈·비동기를 붙일 때 적용한다. 한 번 만들고 끝까지 두는 오브젝트에는 적용하지 않는다.
---

> 최종 업데이트: 2026-10-08 (신설)

# 오브젝트 풀링 — `Common/object-pool/`

코드와 파일 구성은 `<스크립트 루트>/Common/object-pool/object-pool 규칙.md`. 이 문서는 **언제·어떻게** 쓰는지다.

---

## 1. 풀링할까 — 먼저 빈도를 본다

| 생성·삭제 빈도 | 판단 |
|---|---|
| 매 프레임 · 판정마다 · 연출마다 (짧게 살다 사라짐) | **풀링한다** — `PrefabPool<T>` |
| 목록을 다시 그릴 때마다 줄 수가 바뀜 | **제자리 풀** — `UIRowList<T>` |
| 이벤트가 있을 때 가끔 (배치·해제·화면 첫 표시) | 대개 필요 없다. 만들 때 하위 트리가 무거우면(자식 수십 개·`AddComponent` 다수) 끄고 두었다 다시 쓴다 |
| 한 번 만들고 끝까지 둠 | 해당 없음 |

> 추측으로 풀링하지 않는다. Profiler에서 `Object.Instantiate` 샘플·GC Alloc이 **실제로 보일 때**,
> 또는 처음부터 "매번 생기는" 연출을 만들 때 넣는다.

---

## 2. 어느 풀을 쓰나

| | `PrefabPool<T>` | `UIRowList<T>` |
|---|---|---|
| 대상 | 날아가는 아이콘 · 떠오르는 글자 · 이펙트 | 목록 줄 · 결과 칸 · 툴팁 줄 |
| 쉬는 자리 | `inactiveRoot`로 옮긴다 | 원래 부모 아래에서 꺼 둔다 |
| 반납 | 오브젝트가 스스로 `PooledObject.Release()` | 주인이 `HideFrom(n)` |

**목록 줄에 `PrefabPool`을 쓰지 않는다.** 부모를 옮길 때마다 레이아웃 그룹 양쪽이 다시 배치되고,
스택이라 꺼내는 순서가 줄 순서와 어긋난다.

---

## 3. `PrefabPool<T>` 쓰는 법

```csharp
private PrefabPool<FlyIconView> _icons = null!;

private void Start()
{
    _icons = new PrefabPool<FlyIconView>(flyIconPrefab, poolRoot, defaultCapacity: 8, maxSize: 32);
}

private void OnDestroy() => _icons.Dispose();

private void Play(Vector2 from) => _icons.Get(effectLayer).PlayAsync(from).Forget();
```

```csharp
// 꺼낸 오브젝트가 연출을 마치면 스스로 돌아간다
public class FlyIconView : MonoBehaviour, IPoolable
{
    private PooledObject _pooled = null!;
    private CanvasGroup  _group  = null!;

    private void Awake()
    {
        _pooled = GetComponent<PooledObject>();
        _group  = GetComponent<CanvasGroup>();
    }

    public void OnRent()   => _group.alpha = 1f;   // 이전 연출이 남긴 값을 되돌린다
    public void OnReturn() => transform.DOKill();  // 여러 번 불려도 안전해야 한다

    public async UniTask PlayAsync(Vector2 from)
    {
        try
        {
            await transform.DOMove(…).SetLink(gameObject, LinkBehaviour.KillOnDisable)
                           .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _pooled.RentToken);
        }
        finally
        {
            _pooled.Release();  // 취소돼도 돌아간다. 이미 돌아왔으면 무시된다
        }
    }
}
```

| 기본값 | 기준 |
|---|---|
| `defaultCapacity` | 동시에 뜨는 최대 수. ⚠️ **스택 크기일 뿐 미리 만들지 않는다** — 미리 만들려면 `Prewarm(n)` |
| `maxSize` | 동시 최대치의 2배쯤. 넘쳐 돌아온 것은 파괴된다. `ObjectPool` 기본값 10000은 사실상 무제한이다 |
| `collectionCheck` | 에디터·개발 빌드에서만 켜진다 (코드에 고정) |

### 콜백이 하는 일 (코드에 이미 있다 — 바꿀 때 순서를 지킨다)

| 시점 | 하는 일 |
|---|---|
| 만들 때 | `Instantiate(prefab, inactiveRoot)` · `PooledObject` 붙이기 · `IPoolable` 수집 |
| 꺼낼 때 | `SetParent(parent, false)` → 위치·크기를 프리팹 값으로 → `SetActive(true)` → 대여 토큰 생성 → `OnRent` |
| 되돌릴 때 | 대여 토큰 취소 → `OnReturn`(트윈 Kill·내용 비우기) → `SetActive(false)` → `inactiveRoot`로 |
| 넘치거나 정리 | `Destroy` |

---

## 4. 반드시 지킬 것

- **반납은 `finally`에서 한다.** DOTween `OnComplete`로 반납하면 트윈이 Kill될 때 불리지 않아 **반납이 누락된다.**
- **이중 반납은 `PooledObject`가 조용히 막는다** — 밖에서 먼저 반납하면 대여 토큰이 취소돼 연출이 깨어나
  `finally`에서 한 번 더 부르는데, 이건 정상 경로다. `PrefabPool.Release`·`ObjectPool.Release`를 **직접** 두 번 부르는 것만 피한다.
- **풀링된 오브젝트에 `GetCancellationTokenOnDestroy`·`destroyCancellationToken`을 쓰지 않는다.**
  반납돼도 파괴되지 않아 발동하지 않는다 → 반납 뒤에도 작업이 돌아 **다시 꺼내진 오브젝트를 이전 작업이 만진다.**
  `PooledObject.RentToken`을 쓴다(반납·파괴 중 먼저 오는 쪽에서 취소된다).
- **트윈은 `SetLink(gameObject, LinkBehaviour.KillOnDisable)`** — 기본 `KillOnDestroy`는 풀에서 의미가 없다.
- **`OnRent`에서 표시 상태를 되돌린다** — 중간에 Kill된 페이드는 알파 0을, 이동은 엇나간 위치를 남긴다.
- **`OnReturn`은 여러 번 불려도 안전해야 한다** — `Prewarm`은 `OnRent` 없이 `OnReturn`만 부른다.
- **이벤트 구독은 만들 때 한 번** — 꺼낼 때마다 걸면 중복으로 쌓인다.

---

## 5. UI를 풀링할 때

- `SetParent`는 반드시 `worldPositionStays: false` — 아니면 부모 스케일이 거꾸로 곱해진다(`PrefabPool`은 이미 그렇게 한다).
- **꺼진 자식은 LayoutGroup·ContentSizeFitter 계산에서 빠진다.** 켠 직후 크기를 재면 이전 값이 나온다.
  꼭 재야 하면 `LayoutRebuilder.ForceRebuildLayoutImmediate`를 **다 켠 뒤 한 번** 부른다.
- **자기 `Canvas`를 단 오브젝트는 꺼졌다 켜지면 `overrideSorting`이 풀린다** — 켠 뒤에 다시 건다.
- 꺼진 부모 아래에서 만든 줄은 `Awake`가 부모가 켜질 때까지 미뤄진다 — 만들고 바로 `Bind`하려면 부모를 먼저 켠다.
- 레이아웃 그룹 아래 연출 오브젝트(떠오르는 글자)는 `LayoutElement.ignoreLayout = true` 또는 그룹 밖 레이어에 띄운다.

---

## 6. 검증

| 볼 것 | 어떻게 |
|---|---|
| 생성이 사라졌나 | Profiler CPU → 연출 반복 중 `Object.Instantiate` 샘플 0 · GC Alloc 0B |
| 반납 누락 | `CountActive`가 연출이 끝난 뒤 0으로 돌아오나 · Hierarchy 오브젝트 수가 늘지 않나 |
| 반납 뒤에도 도는 작업 | `Window > UniTask Tracker` 0 · `DOTween.TotalActiveTweens()`가 원래 수로 |
| 상태 잔존 | 같은 오브젝트를 다른 데이터로 다시 꺼내 첫 프레임에 옛 값이 비치지 않나 |
