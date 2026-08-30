---
date: 2026-08-30
title: 로그인 화면에 종료 버튼 연결
tags: [client, ui, login]
---

# 로그인 화면에 종료 버튼 연결

## 왜

로그인 화면이 떠 있는 동안은 상태 패널의 종료 버튼이 가려져 손이 닿지 않는다.
타이틀바를 끈 보더리스라 창 `X`도 없어, 나가는 길이 ESC 하나뿐이었다.

## 한 것

- `LoginPresenter`에 `quitButton` 필드 추가 → `WindowManager.QuitApplication` 연결
  (`StatePresenter`와 **같은 단일 경로**. ESC·상태 패널 종료 버튼과 같은 곳으로 모인다)
- 씬에서 참조 할당 — `LoginPresenter.quitButton` → Button `&174295555`
- 오브젝트 이름 오타 수정: `Eixt Button` → `Exit Button` (사용자 승인)
- `Login 규칙.md`에 "종료 버튼이 여기에도 있다" 항목 추가

## 주의사항

- ⚠️ **OnClick은 인스펙터에서 비워 둔다.** 이 프로젝트는 버튼 연결을 전부 코드
  (`AddListener`)로 한다 — 인스펙터에도 걸면 두 번 호출된다.
- 씬 diff에 **레이아웃 재계산 116줄**이 함께 들어갔다. 에디터가 RectTransform을 다시 계산한
  값이라 그대로 뒀다. 다만 같이 저장돼 있던 **패널 활성 상태 뒤바뀜은 되돌렸다** —
  `WorkStation Select Presenter`가 꺼지고 `Setting Presenter`가 켜진 채로 저장돼 있어,
  그대로 커밋하면 **시작 화면이 설정 패널로 바뀌는** 의도치 않은 동작 변경이 됐다.
  🔴 씬을 커밋할 때는 `m_IsActive` 변경을 항상 따로 확인할 것.

관련: [[2026-08-30-window-size-instrumentation]]
