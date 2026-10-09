---
name: dotween
description: DOTween으로 움직임·페이드 연출을 짜는 규칙 — 트윈 수명(SetLink·Kill), 설정, 트윈을 기다릴 때 DOTween 단독 대기 API 대신 UniTask의 DOTween 전용 API(ToUniTask·AwaitFor…). 클라이언트 코드에서 값·위치·색·크기를 바꾸는 연출을 새로 짜거나 고칠 때, 트윈 끝을 기다릴 때 적용한다. 기다림만 있는 흐름은 unitask 스킬. 연출이 없는 로직·데이터·배치 작업에는 적용하지 않는다.
---

> 최종 업데이트: 2026-10-08 (`unitask-dotween`에서 분리 · 함정 — 넘긴 트윈을 finally에서 다시 Kill)

# 연출 — DOTween

**값 · 위치 · 색 · 크기를 바꾸는 연출은 DOTween으로 한다** (`DOFade` · `DOAnchorPos` · `DOScale` · `DOTween.To` …).
`Update`에서 `Lerp`로 직접 굴리거나 코루틴으로 프레임마다 바꾸지 않는다. 기다림·흐름은 [`unitask`](../unitask/SKILL.md).

## 0. 설치 — 표준 패키지다

**DOTween은 표준 패키지다 — 프로젝트에 없으면 이 스킬대로 짜기 전에 사용자에게 아래 안내를 띄워 설치하라고 알린다.**
설치 여부는 `Assets/Plugins/Demigiant/DOTween/`과 `Assets/Resources/DOTweenSettings.asset`으로 본다.

> 📦 **DOTween 설치**
> - Asset Store(무료판): https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676
>   → *Add to My Assets* → Package Manager *My Assets*에서 임포트
> - 임포트 뒤 `Tools > Demigiant > DOTween Utility Panel` → **Setup DOTween** → 쓰는 모듈만 켜고 Apply
>   (⚠️ EPO 같은 외부 에셋 모듈은 **그 에셋이 설치된 경우에만** 켠다 — 없으면 컴파일 오류)
> - UniTask와 함께 쓰려면 `Player Settings > Scripting Define Symbols`에 **`UNITASK_DOTWEEN_SUPPORT`**
>   — 이게 있어야 아래 2장의 API가 생긴다 (UniTask 설치는 [`unitask`](../unitask/SKILL.md) 0장)

---

## 1. 트윈 수명

- **트윈은 오브젝트에 묶는다** — 만들 때 `SetLink(gameObject)`(기본 `KillOnDestroy`).
  껐다 켜는 오브젝트·풀링된 오브젝트는 `SetLink(gameObject, LinkBehaviour.KillOnDisable)`.
- 같은 대상에 새 트윈을 걸기 전에 이전 트윈을 `Kill()`한다 (필드로 들고 있다가 Kill하거나 `DOKill()`).
- 시간만 이어 붙이는 여러 단계 연출은 `Sequence` 하나로 만든다. 중간에 데이터·서버 응답을 기다려야 하면
  `async UniTask` 안에서 트윈을 차례로 await한다 (2장).
- DOTween 설정의 **Recycle Tweens(`defaultRecyclable`)는 켜지 않는다** (기본 꺼짐) — Kill된 트윈 객체가 다른 트윈으로
  재사용돼, 필드에 들고 있던 참조가 엉뚱한 트윈을 조작한다.

---

## 2. 트윈을 기다릴 때는 UniTask의 DOTween 전용 API를 쓴다

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
- 토큰은 [`unitask`](../unitask/SKILL.md) 2장대로 — 오브젝트 수명 토큰, 풀링된 오브젝트면 `PooledObject.RentToken`.

### ⚠️ 함정 — UniTask 소스로 확인한 것

| 함정 | 결과 | 대응 |
|---|---|---|
| `ToUniTask()` 기본값은 `TweenCancelBehaviour.Kill` | 취소돼도 **트윈만 죽고 await 뒤가 정상 완료로 계속 돈다** | `KillAndCancelAwait`를 명시한다 |
| 트윈이 밖에서 Kill됨(`SetLink`·`DOKill`) | await가 **정상 완료로** 깨어난다 | 토큰을 함께 넘겨 두고, await 뒤에 오브젝트를 만지기 전에 확인한다 |
| 인자 없는 `await tween` | 그 트윈의 **`onKill`을 덮어쓴다** | `OnKill` 콜백을 건 트윈은 `ToUniTask`로 기다린다 |
| `KillAndCancelAwait`로 넘긴 트윈을 `finally`에서 또 `Kill()` | 취소되면 UniTask가 Kill하고 **그 Kill 안에서 finally가 동기로 돈다** — 두 번째 Kill이 DOTween 안에서 `IndexOutOfRangeException`(`TweenManager.RemoveActiveTween`) | 넘긴 뒤에는 Kill하지 않는다. 넘기기 전에 예외가 났을 때만 직접 Kill (플래그로 가른다) |

---

## 3. 검증

| 볼 것 | 어디서 |
|---|---|
| 남은 트윈 | `DOTween.TotalActiveTweens()` — 연출이 끝나면 원래 수로 돌아와야 한다 |
| 파괴 뒤 접근 | 연출 도중 창·패널을 닫아 본다 — `MissingReferenceException`이 없어야 한다 |
