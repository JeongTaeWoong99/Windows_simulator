---
date: 2026-08-19
title: System 오버레이 2단 구조 통일 + CanvasGroup 토글·로딩 지연 표시 (P2)
tags: [client, ui]
---

# System 오버레이 구조 통일 + 로딩 깜빡임 (P2)

## 목적 / 배경
- 사용자 검토 2건: (1) `!System Canvas`만 `Presenter→Panel→Box→내용` 3단 중첩이라 다른 캔버스
  (`(MAIN VIEW)`→`(↓ SUB VIEW)`→내용 2단)와 달랐다. (2) 로딩/알림이 빠른 응답에서 깜빡였다.
- 이 중첩은 코드가 요구한 게 아니라 "항상 켜진 래퍼 + 토글 자식" 설계 탓이었다(프레젠터는 직렬화
  참조로만 붙잡고 이름을 찾지 않음). 깜빡임 원인은 배경색이 아니라 **빠른 응답에도 즉시 표시**.

## 변경 내용
- `UI/System/LoadingPresenter/LoadingPresenter.cs`, `.../NoticePresenter/NoticePresenter.cs`
  - 직렬화 필드 `GameObject panel` → `CanvasGroup group`. 토글을 `SetActive` →
    `alpha`(0/1)·`blocksRaycasts`·`interactable`로. **오브젝트는 상주** → 자기를 끄지 않아 이벤트 수신 유지.
  - Loading에 **0.15초 grace 지연 표시**(`ShowDelaySeconds`) 추가 — grace 안에 대기가 끝나면 코루틴
    취소로 한 번도 안 뜬다. Notice는 즉시 표시(확인형이라 깜빡임 대상 아님).
- 씬(`Assets/Scenes/Original`, MCP로 수행): `Presenter→Panel→Box`를 `(↓ SUB VIEW)` 2단으로 평탄화.
  각 SUB VIEW 오브젝트에 스크립트 + `CanvasGroup` + 전체화면 blocker `Image`(alpha 0, raycastTarget).
  Loading은 텍스트를 직속으로, Notice는 다이얼로그 패널만 직속 자식으로 유지(이름은 다른 UI에 맞춰 `Panel`로 통일). 중간 래퍼 삭제, 개명,
  `group`/`messageText`/`closeButton` 재배선, Notice가 Loading 위(형제 뒤).
- 문서: `UI 규칙.md` §7(2단 구조·CanvasGroup 토글·지연 규약), `UI 배치 현황.md`(트리에 System 추가).

## 주요 결정 / 근거
- **토글을 CanvasGroup으로** — 구독 프레젠터는 오브젝트를 끄면 다시 켤 이벤트를 못 받는다.
  CanvasGroup은 오브젝트를 켜 둔 채 숨기므로 래퍼 없이 2단으로 접을 수 있다(#1·#2를 한 번에 해결).
- **blocker Image는 alpha 0이라도 raycastTarget으로 뒤 UI를 막는다**(uGUI는 알파를 히트테스트에 안 씀).
  실제 차단 on/off는 `CanvasGroup.blocksRaycasts`가 쥔다 — 숨김 시 false라 유휴엔 클릭이 통과한다.
- **깜빡임의 근본 해결은 grace 지연** — 빠른 왕복은 아예 안 띄운다. 배경 alpha만 0으로 두는 이전
  방식은 로딩 몸통 깜빡임은 남겼다.

## 후속 작업 / 주의사항
- 런타임 검증(서버 필요)은 계획 파일의 후속 라운드 검증 절대로: 빠른 성공=로딩 안 뜸, 느린 왕복만 표시,
  표시 중 뒤 클릭 차단, 실패→Notice. → `~/.claude/plans/c-users-asus-desktop-md-a-2-a-5-dynamic-scone.md`
- 남은 것: P3(죽은 `SaveBool` 제거·미사용 `SystemCanvasView.Show`·SettingPresenter 죽은 토글 배선 점검).
- 관련: 로딩 지연 상수 `ShowDelaySeconds`(0.15s)는 LoadingPresenter 상단 상수 1개로 조정.
