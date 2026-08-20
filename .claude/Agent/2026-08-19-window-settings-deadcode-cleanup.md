---
date: 2026-08-19
title: 죽은 저장 쓰기·미사용 API 정리 (P3)
tags: [client, window, ui]
---

# 창 설정 죽은 코드 정리 (P3)

## 목적 / 배경
- P1/P2 후속 검토에서 남은 저위험 정리 3건.

## 변경 내용
- `Managers/WindowManager.cs` — `SetTitleBar`/`SetTransparent`/`SetDynamicClickThrough`의
  `WindowSettings.SaveBool(...)` **3곳 제거**. 이 세 설정은 UI 토글을 걷어낸 고정값이라 `LoadSettings`가
  저장값을 읽지 않는데, 쓰기만 남아 읽히지 않는 죽은 값이었다. 재활성화 방법을 주석으로 남김.
- `Settings/WindowSettings.cs` — 세 키(`TitleBarKey`·`TransparentKey`·`DynamicClickThroughKey`)를
  "예약(현재 미기록)"으로 분리 표기. 실제 저장·로드에 쓰는 키(Topmost·Scale·Anchor)와 구분.
- `UI/System/SystemCanvasView.cs` — `Show(bool)`가 현재 호출처 없음을 명시(오버레이 상주라 UIManager가
  여닫지 않음). 다른 `XxxCanvasView`와 API를 맞춘 **예약**임을 주석화(제거하지 않음).
- `Settings 규칙.md` §1 예외 문구를 "저장은 되지만"→"저장·로드 어느 쪽도 쓰지 않는다"로 정정.

## 주요 결정 / 근거
- **SettingPresenter 토글 배선은 손대지 않았다** — 씬 점검 결과 titleBar/transparent/dynamicClickThrough
  토글은 오브젝트가 `(미사용)…Toggle`로 **비활성**이고 참조는 **유효**했다(RequireRef 안전). 삭제된 게
  아니라 "재활성화 여지"로 남긴 것이라 정리 대상이 아니다(사용자 P3 조건 "패널에서 실제로 빠졌으면"에 안 걸림).
- `SystemCanvasView.Show`는 제거 대신 **예약으로 문서화** — 형제 `CanvasView`들과 대칭 API라 남겨 두는 편이
  일관적이고, 언젠가 오버레이 전체를 끌 때 그대로 쓸 수 있다.

## 후속 작업 / 주의사항
- 세 키는 이제 쓰는 곳이 없다(예약). 재활성화 시 `Set*`의 `SaveBool` + `LoadSettings`의 읽기를 함께 되살린다.
- 없음(런타임 영향 없는 정리라 별도 검증 불필요 — 컴파일·콘솔 에러 0 확인).
