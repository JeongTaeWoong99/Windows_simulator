---
date: 2026-10-10
title: 루트 UI를 두 축으로 — !Horizontal Columns · !Overlay Canvas · 로그인을 오버레이 Presenter로 (T-140)
tags: [client, ui, structure]
---

# 루트 UI 두 축 정리 (T-140)

## 목적 / 배경
- `Root Canvas` 아래가 `!System Canvas` · `!Login Canvas` · `!Horizental Columns` 세 갈래 — 로그인만 덩그러니 따로였다(사용자 지적)
- 사용자 지시: 오버레이 축으로 접고, 폴더·스크립트까지 `Overlay`로, 오타 수정, **기능이 같아도 사람이 보기에 형태가 다르면 맞춘다**

## 변경 내용
- 씬: `!System Canvas (MAIN VIEW)` → `!Overlay Canvas`(마지막 형제 · Sorting 100) · `!Horizental Columns` → `!Horizontal Columns`(첫 형제) · `!Login Canvas` 해체 → `Login Presenter`(꽉 찬 어두운 판)를 오버레이 첫 형제로, 상자는 자식 `Panel`
- 폴더: `UI/System/` → `UI/Overlay/` · `UI/Login/LoginPresenter/` → `UI/Overlay/LoginPresenter/` · `System 규칙.md` → `Overlay 규칙.md`(로그인 규칙 흡수) · `UI/Login/` 삭제
- 코드: `SystemCanvasView`(호출처 없는 예약)·`LoginCanvasView` 삭제 · `LoginPresenter.Show` · `UIManager.loginPresenter` · 끌리는 그림 Sorting 10 → 200
- 문서: UI · UI 배치 현황(T-139의 `Body Panel`·`Tool Panel`도 반영) · Layout · Managers · 각 폴더 규칙 · 패킷 레퍼런스 · 기획 UI README · 열린 일감 T-031·T-071 경로

## 주요 결정 / 근거
- **로그인 = 오버레이의 Presenter** — "캔버스에는 Presenter를 담는다" 규칙에 오히려 맞는다. 다른 창형 오버레이와 같은 `Presenter(차단막) → Panel(창)` 구성
- 로그인만 `SetActive` — 켜진 채 시작하고 다시 켜는 손(`UIManager`)이 밖에 있어 `CanvasGroup` 강제 조건에 해당하지 않는다. 그래서 **꺼진 채 저장 금지**(`Overlay 규칙.md` ②)
- 오버레이 View 삭제 — 열 축처럼 표기·스크립트 없는 축으로 형태를 맞췄다(사용자 승인)
- Sorting은 규칙 문서의 "띄엄띄엄"에 맞춰 0 · 100 · 200 · 형제 순서도 깔리는 순서와 같게
- 로그인·오버레이에는 `Body Panel`이 필요 없다 — 부모에 `LayoutGroup`이 없어 레이아웃 변경이 이미 각 Panel에서 멈춘다

## 후속 작업 / 주의사항
- Test Copy 씬은 씬 복사로 맞춘다
- `ugui-mvp` 스킬(툴킷 사본)의 접두 예시에 `!Login Canvas`가 남아 있다 — 범용 예시라 손대지 않았다

## 추가 — 이름·순서 (T-141)
- `!Overlay Layer` → `!Overlay Canvas` — Presenter를 담는 것은 캔버스라는 걸 이름이 말한다. `(MAIN VIEW)`는 여닫는 캔버스(`...CanvasView`) 표기라 붙이지 않는다(`UI 규칙.md` 표기 표)
- 한 캔버스 유지 — 켜진 그래픽 49 vs 열 1,333 · FPS 글자 변경 0.01ms 차이(실측)
- 순서(뒤→앞): 결과 · 수량 · 확인 · 로그인 · FPS · 로딩 · 알림 · 툴팁. 사용자 안과 다른 점 — 알림 > 로딩(`RaiseFatal`이 대기를 끝내지 않는다)
- ⚠️ 사용자가 하이라키를 위→아래 = 앞→뒤로 읽고 재배치해 둔 상태였다(저장 안 됨) — UGUI는 반대다. 문서에 경고로 남겼다
