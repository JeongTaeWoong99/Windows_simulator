---
date: 2026-08-23
title: 로딩 오버레이 — 차단은 즉시, 표시만 0.15초 지연으로 분리
tags: [client, ui, overlay, serverwait]
---

# 로딩 오버레이 — 차단은 즉시, 표시만 0.15초 지연으로 분리

## 목적 / 배경

"`!System Canvas`의 두 오버레이만 `CanvasGroup`으로 여닫고 나머지는 `SetActive`인 게
일관성 없어 보인다 — 전부 `CanvasGroup`으로 통일하는 게 낫지 않나"라는 문제 제기에서 출발했다.

점검 결과 **두 방식은 섞인 게 아니라 기준이 있었고**, 통일하는 대신 기준을 문서에 명문화하는
쪽으로 갔다. 다만 점검 중 **실재하는 결함**을 하나 찾아 그것을 고쳤다.

## 변경 내용

- `UI/System/LoadingPresenter/LoadingPresenter.cs` — `SetVisible(bool)` 하나가 표시와 차단을
  함께 하던 것을 `SetBlocking`(즉시) / `SetShown`(0.15초 뒤) 두 축으로 쪼갰다.
- `UI/UI 규칙.md` §7 — 판별 기준을 표로 승격, 통일 불가 사유, 두 축 설명 추가.
- `UI/UI 배치 현황.md` — 트리 줄·각주·작업슬롯 대기 문단 갱신.

**씬 수정 없음** — 두 SUB VIEW에 `CanvasGroup`(alpha 0·blocksRaycasts 0)과 blocker
`Image`(`m_RaycastTarget: 1`)가 이미 붙어 있고 `group` 배선도 그대로다.

## 주요 결정 / 근거

### 왜 전부 `CanvasGroup`으로 통일하지 않았나 — 같은 제안이 다시 올라올 지점이다

가르는 축은 "보이냐"가 아니라 **"나를 다시 켜 줄 주체가 밖에 있는가"**다.
오버레이 둘은 자기 이벤트로 스스로 뜨는 게 유일한 입구라 자기를 끄면 영구 잠김이 된다.
나머지는 `UIManager`·형제 Presenter가 켜 주고, `OnEnable` 재구독 + `Refresh`(§6)가
꺼진 동안 놓친 것을 따라잡는다.

통일하면 깨지는 것 — **`CanvasGroup`은 레이아웃에서 빼 주지 않는다.**
`#Main Canvas`의 세 화면이 한 자리를 나눠 갖고, `CloseAllExceptWidget`이 캔버스까지 끄는
이유(900px가 위젯을 밀어낸다)도 무력화된다. 판단 근거는 `UI 규칙.md` §7 · §7-2.

### 고친 결함 — 거짓 무응답 알림

**대기 중 화면이 꺼지면 `OnDisable`이 구독을 끊어 응답을 놓치고, 실제로는 성공한 요청에
5초 뒤 "응답이 오지 않았습니다"가 뜬다.** 경로 둘 —

- 가챠 요청 중 하단 [거래] 버튼으로 열을 닫으면 `#Market Canvas`가 꺼진다 (`GachaPresenter`)
- 작업슬롯 대기 중 뒤로가기 — 정책상 잠그지 않아 열려 있다 (`WorkStationSelectPresenter`)

로딩이 0.15초 뒤 화면을 덮어 실질 재현은 어려웠지만 **그 0.15초가 뚫려 있었다.**

**사후 보정(`ServerWaitHandle.Abandon()` 같은 것) 대신 입력을 즉시 막는 쪽을 택했다** —
누를 수 없으면 꺼질 일도 없어 결함이 근본에서 사라진다. 깜빡임 제거라는 원래 목적도 유지된다.

### 지뢰 — 알아 두면 다시 파지 않는 것

- **`interactable`은 뒤 UI를 막지 않는다.** 이 `CanvasGroup` **안쪽**의 `Selectable`만
  잠근다. 로딩 몸통엔 버튼이 없어 차단에 아무 기여도 안 한다 — 그래서 표시 축에 묶었다.
  막는 것은 `blocksRaycasts` + 전체화면 blocker `Image`(alpha 0이어도 `raycastTarget`이
  켜져 있으면 계속 막는다)다.
- **차단 시점에 프레임 틈이 없다.** 각 Presenter가 `Send()` 직후 `_wait.Begin()`을 부르고,
  `Begin`이 `_activeCount` 0→1에서 `BusyChanged(true)`를 **동기로** 던진다.
- **실패 경로에서도 차단이 안 끊긴다.** `Resolve`가 `NoticeRaised`를 **먼저**,
  `BusyChanged(false)`를 나중에 던진다 — Notice가 차단을 넘겨받은 뒤 Loading이 내려간다.
  이 순서를 뒤집으면 뒤 UI가 눌리는 프레임이 생긴다.

### 손대지 않기로 한 것

- **`NoticePresenter`** — 지연 표시가 없어 두 축을 쪼갤 이유가 없다.
- **개별 버튼 잠금 유지** (`GachaPresenter._isWaiting`·`SetButtons`,
  `WorkStationSelectPresenter.ApplyWaitingLock`, `LoginPresenter.RefreshButton`).
  역할이 다르다 — 전역 차단은 "대기 중 전체"를 막고, 개별 잠금은 "이 버튼은 지금 못 누른다"를
  **보이게** 한다. 차단이 어떤 이유로 새도 버튼이 막아 준다.
- **`WorkStationListPresenter.Update`** — `#Main Canvas`의 SUB VIEW라 화면을 갈아 끼우거나
  접으면 함께 꺼진다. **이미 "보일 때만 돈다"가 성립한다.** 카운트다운은 시간이 흐르는 것
  자체가 표시 내용이라 이벤트로 대체할 수 없다.

## 후속 작업 / 주의사항

**에디터 플레이 검증이 아직이다.** 빌드는 필요 없다(창 제어가 아니라 UI 입력 차단이 대상).

1. 정상 왕복(로그인 → 빈 칸 → [배치])에서 **로딩이 한 번도 안 보여야 한다** — 보이면 회귀.
2. 즉시 차단 확인이 어려우면 `ShowDelaySeconds`를 임시로 `3f`로 올려 창을 벌린다 —
   화면은 정상인데 클릭이 안 먹어야 한다. **확인 후 반드시 `0.15f`로 되돌린다.**
3. 같은 `3f` 상태에서 가챠를 누르고 곧바로 [거래]로 열을 닫아 본다 — **닫히지 않아야 한다.**
   (변경 전에는 닫혔고 5초 뒤 무응답 알림이 떴다.)
4. 서버를 끈 채 로그인 → 5초 타임아웃 → Loading이 내려가고 Notice가 뜨는 사이에
   뒤 UI가 눌리는 프레임이 없어야 한다.

관련 로그: [2026-08-19 시스템 오버레이 평탄화](2026-08-19-system-overlay-flatten-canvasgroup.md) ·
[2026-08-18 서버 대기](2026-08-18-server-wait-loading-notice.md)
