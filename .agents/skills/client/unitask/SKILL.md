---
name: unitask
description: 코루틴 대신 UniTask로 기다림·흐름을 짜는 규칙 — 지연·다음 프레임·조건 대기, 취소 토큰, async void 금지와 반환형, 기존 코루틴 전환. 클라이언트 코드에서 기다림(지연·다음 프레임·응답 대기)을 새로 짜거나, 코루틴(IEnumerator·StartCoroutine)이 있는 파일을 고칠 때 적용한다. 트윈 연출은 dotween 스킬. 기다림이 없는 로직·데이터·배치 작업에는 적용하지 않는다.
---

> 최종 업데이트: 2026-10-08 (`unitask-dotween`에서 분리)

# 비동기 — UniTask

**새 코드에 코루틴(`IEnumerator` · `StartCoroutine` · `yield return`)을 쓰지 않는다.**
기다림과 흐름은 UniTask가, 움직임과 연출은 DOTween이 맡는다 → [`dotween`](../dotween/SKILL.md).

## 0. 설치 — 표준 패키지다

**UniTask는 표준 패키지다 — 프로젝트에 없으면 이 스킬대로 짜기 전에 사용자에게 아래 안내를 띄워 설치하라고 알린다.**
없는 채로 이 규칙대로 쓰면 컴파일이 깨진다. 설치 여부는 `Packages/manifest.json`의 `com.cysharp.unitask`로 본다.

> 📦 **UniTask 설치**
> - Package Manager → `+` → *Add package from git URL* →
>   `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`
> - 또는 `.unitypackage`: https://github.com/Cysharp/UniTask/releases
> - DOTween과 함께 쓰려면 `Player Settings > Scripting Define Symbols`에 **`UNITASK_DOTWEEN_SUPPORT`** ([`dotween`](../dotween/SKILL.md) 0장)

---

## 1. 무엇으로 기다리나

| 하려는 일 | 쓰는 것 |
|---|---|
| N초 기다리기 | `await UniTask.Delay(TimeSpan.FromSeconds(n), ignoreTimeScale: true, cancellationToken: ct)` — 타임스케일과 상관없이 흘러야 하면 `ignoreTimeScale: true` |
| 다음 프레임 · 조건 기다리기 | `UniTask.NextFrame(ct)` · `UniTask.WaitUntil(() => …, cancellationToken: ct)` |
| 트윈 끝날 때까지 기다리기 | `await tween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct)` — [`dotween`](../dotween/SKILL.md) 2장 |
| 여러 개를 함께 기다리기 | `UniTask.WhenAll(…)` · 먼저 끝나는 하나는 `UniTask.WhenAny(…)` |

> `Update`에서 **매 프레임 무언가를 확인**하는 코드(호버 시간 재기·FPS 집계·입력 감시)는 기다림이 아니다 — 그대로 둔다.

---

## 2. 취소 — 모든 비동기는 토큰을 받는다

- **오브젝트 수명에 묶는다** — `this.GetCancellationTokenOnDestroy()` 또는 Unity 2022.2+의 `destroyCancellationToken`.
- **켜고 끌 때마다 끊어야 하는 흐름**(패널을 닫으면 멈출 연출·지연 표시)은 `CancellationTokenSource`를 필드로 두고,
  시작할 때 이전 것을 `Cancel()`+`Dispose()` 한 뒤 새로 만든다. `OnDisable`/`OnDestroy`에서도 끊는다.
- **`async` 메서드는 마지막 매개변수로 `CancellationToken`을 받고, 안의 await마다 넘긴다.** 한 군데만 빠져도
  그 대기는 오브젝트가 사라진 뒤에도 돈다.
- **풀링된 오브젝트는 destroy 토큰이 발동하지 않는다** — 대여 토큰(`PooledObject.RentToken`)을 쓴다 ([`object-pool`](../object-pool/SKILL.md)).

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
| `yield return tween.WaitForCompletion()` | `await tween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct)` |
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
| 파괴 뒤 접근 | 대기 도중 창·패널을 닫아 본다 — `MissingReferenceException`이 없어야 한다 |
