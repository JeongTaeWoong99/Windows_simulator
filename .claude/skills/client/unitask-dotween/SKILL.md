---
name: unitask-dotween
description: 코루틴 대신 UniTask(대기·흐름)와 DOTween(연출·트윈)으로 비동기를 짜는 규칙 — 취소 토큰·트윈 수명·async void 금지·기존 코루틴 전환. 클라이언트 코드에서 기다림(지연·다음 프레임·응답 대기)이나 움직임·페이드 연출을 새로 짜거나, 코루틴(IEnumerator·StartCoroutine)이 있는 파일을 고칠 때 적용한다. 대기·연출이 없는 로직·데이터·배치 작업에는 적용하지 않는다.
---

> 최종 업데이트: 2026-10-08 (신설 — `clean-code-style`에서 분리 · UniTask의 DOTween 전용 API)

# 비동기·연출 — UniTask · DOTween

## 0. 전제 — 패키지 두 개와 심볼 하나

| 필요한 것 | 설치 |
|---|---|
| **UniTask** | Package Manager → *Add package from git URL* → `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| **DOTween** (무료판) | Asset Store → `Assets/Plugins/Demigiant/` 임포트 → `Tools > Demigiant > DOTween Utility Panel` → **Setup DOTween** |
| **`UNITASK_DOTWEEN_SUPPORT`** | `Player Settings > Scripting Define Symbols`에 추가 — 이게 있어야 트윈을 await할 수 있다 |

**셋 중 하나라도 없으면 이 스킬은 해당 없다.** 없는 프로젝트에서 이 규칙대로 쓰면 컴파일이 깨진다.

---

## 1. 무엇으로 짜나

**새 코드에 코루틴(`IEnumerator` · `StartCoroutine` · `yield return`)을 쓰지 않는다.**
기다림과 흐름은 UniTask가, 움직임과 연출은 DOTween이 맡는다.

| 하려는 일 | 쓰는 것 |
|---|---|
| N초 기다리기 | `await UniTask.Delay(TimeSpan.FromSeconds(n), ignoreTimeScale: true, cancellationToken: ct)` — 타임스케일과 상관없이 흘러야 하면 `ignoreTimeScale: true` |
| 다음 프레임 · 조건 기다리기 | `UniTask.NextFrame(ct)` · `UniTask.WaitUntil(() => …, cancellationToken: ct)` |
| 값 · 위치 · 색 · 크기 바꾸기 | DOTween (`DOFade` · `DOAnchorPos` · `DOScale` · `DOTween.To` …). `Update`에서 `Lerp`로 직접 굴리지 않는다 |
| 트윈 끝날 때까지 기다리기 | `await tween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct)` |
| 여러 단계 연출 | 시간만 이어 붙이면 `Sequence`, 중간에 데이터·서버 응답을 기다리면 `async UniTask` 안에서 트윈을 차례로 await |

> `Update`에서 **매 프레임 무언가를 확인**하는 코드(호버 시간 재기·FPS 집계·입력 감시)는 기다림이 아니다 — 그대로 둔다.

### 1-1. 트윈을 기다릴 때는 UniTask의 DOTween 전용 API를 쓴다

`UNITASK_DOTWEEN_SUPPORT`가 켜지면 UniTask가 `Tween`에 확장 메서드를 붙인다. **DOTween만 쓸 때의 대기 API는 쓰지 않는다.**

| 쓰지 않는다 (DOTween 단독) | 왜 | 대신 (UniTask × DOTween) |
|---|---|---|
| `yield return tween.WaitForCompletion()` | 코루틴 전용 | `await tween.ToUniTask(…)` |
| `await tween.AsyncWaitForCompletion()` · `AsyncWaitForRewind()` 등 | `System.Threading.Tasks.Task`라 할당이 생기고, **매 프레임 `Task.Yield()`로 폴링**하며, 취소 토큰을 받지 않는다 | `await tween.ToUniTask(…)` · `AwaitForRewind(…)` |
| `tween.OnComplete(() => 다음 일)` 콜백 이어 붙이기 | Kill되면 불리지 않고, 취소·예외 흐름이 끊긴다 | `await` 뒤에 다음 일을 쓴다 |

UniTask 쪽은 트윈 콜백에 걸어 **폴링 없이** 깨어나고, 대기 객체를 풀링하며, 취소 토큰과 취소 시 트윈 처리 방식을 받는다.

| API | 깨어나는 때 |
|---|---|
| `tween.ToUniTask(behaviour, ct)` | 트윈이 **Kill될 때**(완료 후 자동 Kill 포함) — 기본으로 쓴다 |
| `tween.AwaitForComplete(behaviour, ct)` | 완료 콜백 — `SetAutoKill(false)`로 남겨 두는 트윈을 기다릴 때 |
| `AwaitForPause` · `AwaitForPlay` · `AwaitForRewind` · `AwaitForStepComplete` | 각 콜백 — 반복 트윈의 한 바퀴(`StepComplete`) 등 |
| `tween.WithCancellation(ct)` · 인자 없는 `await tween` | 간단형 — 취소 시 트윈만 Kill하고 정상 완료(`TweenCancelBehaviour.Kill`)라 **뒤 코드가 계속 돈다**. 쓰지 않는다 |

- `behaviour`는 **`TweenCancelBehaviour.KillAndCancelAwait`를 기본으로** 쓴다 (아래 함정 표).
  취소돼도 끝 상태로 보내야 하면 `CompleteAndCancelAwait`.
- 여러 트윈을 동시에 기다리면 `await UniTask.WhenAll(a.ToUniTask(…), b.ToUniTask(…))` — 시간만 맞추면 되는 연출은 `Sequence` 하나로 묶는 편이 가볍다.

---

## 2. 취소 — 모든 비동기는 토큰을 받는다

- **오브젝트 수명에 묶는다** — `this.GetCancellationTokenOnDestroy()` 또는 Unity 2022.2+의 `destroyCancellationToken`.
- **켜고 끌 때마다 끊어야 하는 흐름**(패널을 닫으면 멈출 연출·지연 표시)은 `CancellationTokenSource`를 필드로 두고,
  시작할 때 이전 것을 `Cancel()`+`Dispose()` 한 뒤 새로 만든다. `OnDisable`/`OnDestroy`에서도 끊는다.
- **`async` 메서드는 마지막 매개변수로 `CancellationToken`을 받고, 안의 await마다 넘긴다.** 한 군데만 빠져도
  그 대기는 오브젝트가 사라진 뒤에도 돈다.
- **풀링된 오브젝트는 destroy 토큰이 발동하지 않는다** — 대여 토큰(`PooledObject.RentToken`)을 쓴다 ([`object-pool`](../object-pool/SKILL.md)).

### 트윈 수명

- **트윈은 오브젝트에 묶는다** — 만들 때 `SetLink(gameObject)`(기본 `KillOnDestroy`).
  껐다 켜는 오브젝트·풀링된 오브젝트는 `SetLink(gameObject, LinkBehaviour.KillOnDisable)`.
- 같은 대상에 새 트윈을 걸기 전에 이전 트윈을 `Kill()`한다 (필드로 들고 있다가 Kill하거나 `DOKill()`).

### ⚠️ 함정 — UniTask 소스로 확인한 것

| 함정 | 결과 | 대응 |
|---|---|---|
| `ToUniTask()` 기본값은 `TweenCancelBehaviour.Kill` | 취소돼도 **트윈만 죽고 await 뒤가 정상 완료로 계속 돈다** | `KillAndCancelAwait`를 명시한다 |
| 트윈이 밖에서 Kill됨(`SetLink`·`DOKill`) | await가 **정상 완료로** 깨어난다 | 토큰을 함께 넘겨 두고, await 뒤에 오브젝트를 만지기 전에 확인한다 |
| 인자 없는 `await tween` | 그 트윈의 **`onKill`을 덮어쓴다** | `OnKill` 콜백을 건 트윈은 `ToUniTask`로 기다린다 |
| DOTween 설정의 Recycle Tweens(`DOTweenSettings.defaultRecyclable`) | Kill된 트윈 객체가 다른 트윈으로 재사용돼, 필드에 들고 있던 참조가 엉뚱한 트윈을 조작한다 | 켜지 않는다 (기본 꺼짐) |

---

## 3. `async void` 금지 — 반환형 고르기

| 상황 | 반환형 |
|---|---|
| 기다려 줄 호출자가 있다 (거의 전부) | `async UniTask` / `async UniTask<T>` |
| 호출하고 잊는다 (버튼 핸들러 · 이벤트 구독에서 시작) | `async UniTask`로 만들고 부르는 쪽에서 `FooAsync(ct).Forget()` |
| 시작점 자체가 `void` 시그니처여야 한다 (`Start`·이벤트 핸들러를 그대로 async로) | `async UniTaskVoid` — 그 메서드 안에서 끝까지 책임진다 |

- **`async void`는 쓰지 않는다** — 예외가 호출자에게 가지 않고, 취소 예외까지 오류로 찍힌다.
- **`Forget()`은 반드시 붙인다.** await도 Forget도 하지 않은 UniTask는 예외가 조용히 사라진다.
- **취소(`OperationCanceledException`)는 오류가 아니다** — `Forget()`이 기본으로 삼키므로 일부러 잡지 않는다.
  취소 뒤 정리할 것이 있으면 `try/finally`, 결과로 분기해야 하면 `SuppressCancellationThrow()`.
- **이름 끝에 `Async`를 붙인다** — `PlayRevealAsync(ct)`.

```csharp
// 지연 표시 — 다시 불리면 이전 대기를 끊고 새로 센다
private CancellationTokenSource? _delayCts;

private void BeginDelayedShow()
{
    CancelDelayedShow();
    _delayCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
    ShowAfterDelayAsync(_delayCts.Token).Forget();
}

private async UniTask ShowAfterDelayAsync(CancellationToken ct)
{
    await UniTask.Delay(TimeSpan.FromSeconds(0.3), ignoreTimeScale: true, cancellationToken: ct);
    await panel.DOFade(1f, 0.15f).SetLink(gameObject).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
}

private void CancelDelayedShow()
{
    _delayCts?.Cancel();
    _delayCts?.Dispose();
    _delayCts = null;
}
```

---

## 4. 기존 코루틴 — 단계적으로 바꾼다

- 이미 있는 코루틴은 **그 파일을 고칠 일이 생겼을 때** 함께 바꾼다. 코루틴만 바꾸려고 손대지 않는다.

| 코루틴 | UniTask |
|---|---|
| `Coroutine? _handle` + `StopCoroutine(_handle)` | `CancellationTokenSource? _cts` + `Cancel()` · `Dispose()` |
| `yield return new WaitForSecondsRealtime(s)` | `await UniTask.Delay(TimeSpan.FromSeconds(s), ignoreTimeScale: true, cancellationToken: ct)` |
| `yield return new WaitForSeconds(s)` | `await UniTask.Delay(TimeSpan.FromSeconds(s), cancellationToken: ct)` |
| `yield return null` | `await UniTask.NextFrame(ct)` |
| `StartCoroutine(Foo())` in `Start` | `FooAsync(destroyCancellationToken).Forget()` |

- ⚠️ **코루틴은 오브젝트가 꺼지면 같이 멈췄다.** UniTask는 꺼져도 계속 돈다 — 꺼질 때 멈춰야 하던 흐름이면
  `OnDisable`에서 CTS를 끊는 것까지 옮겨야 같은 동작이다.
- ⚠️ **타임아웃이 스스로를 끝내는 흐름**(대기 → 만료 처리 → 정리 중에 자기 CTS를 Cancel)은 정리에서 다시
  자기를 취소하지 않게 한다 — 만료 경로에서는 CTS를 먼저 떼어 두고 정리한다.
- 바꾼 뒤 확인 — `grep -rnE "IEnumerator|StartCoroutine|yield return" <스크립트 폴더>`.

---

## 5. 검증

| 볼 것 | 어디서 |
|---|---|
| 끝나지 않고 남은 UniTask | `Window > UniTask Tracker` (Enable Tracking 켜고) — 화면을 닫은 뒤 0이어야 한다 |
| 남은 트윈 | `DOTween.TotalActiveTweens()` — 연출이 끝나면 원래 수로 돌아와야 한다 |
| 파괴 뒤 접근 | 연출 도중 창·패널을 닫아 본다 — `MissingReferenceException`이 없어야 한다 |
