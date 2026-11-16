---
date: 2026-11-16
title: 작업슬롯 10칸 — 씬 칸 틀 2개 추가 · T-201 실측 · 아트 점검
tags: [client, editor, ui]
---

# 작업슬롯 10칸 — 씬 칸 틀 2개 추가

## 목적 / 배경
- #125로 서버·데이터가 10칸이 됐다(`805385d6`). 클라 작업슬롯 화면은 씬 칸 틀 개수(`slotParent.childCount`)만큼만 그려서 9·10번째 칸이 안 보였다 → tasks/T-202.
- 같은 세션에서 T-201 실측(응축·강화·알림 캡처 · 잘린 글자 0건)으로 보관, T-189 뽑기권 보류, 캐릭터·아이콘 아트 점검 → 레퍼런스는 바탕화면 `아트 레퍼런스/`(저장소 밖).

## 주요 결정 / 근거
- **프리팹을 새로 꺼내지 않고 `Work Slot (7)`을 복제했다.** 씬 인스턴스에만 `WorkSlotFrameView` 컴포넌트와 자식(art·stateIcon)이 덧붙어 있다(프리팹 `WorkSlotFrame`에는 없다). `InstantiatePrefab`으로 만들면 이것들이 빠진다.
- **에디터 저장분을 그대로 커밋하지 않았다.** 이 씬은 저장하면 레이아웃 구동 RectTransform 값(0으로 저장돼 있던 것)이 실제 값으로 다시 써져 diff가 2,500줄 넘게 난다. 원본 파일에 **새 블록 30개 + Content의 `m_Children` 두 줄만** 옮겨 붙였다(790줄 추가뿐). 블록 단위 병합 스크립트는 세션 스크래치에만 있었다 — 같은 일이 또 있으면 "원본에 없던 `--- !u!` 앵커 블록 + 그것을 참조하는 기존 블록만 새 쪽으로" 규칙으로 다시 짜면 된다.

## 후속 작업 / 주의사항
- 10칸을 다 채운 위젯 줄 실측은 남았다(T-202). 테스트 계정 골드 6천만을 써야 해서 손대지 않았다.
- 캡처: `capture_game_view`는 이 프로젝트에서 검게 나온다 → 플레이 중 `ScreenCapture.CaptureScreenshot` + `editor_focus`. 로그인은 로그인 버튼 `onClick.Invoke()`(계정 1234).
- 잘린 글자 검사기는 에디터 어셈블리 internal이라 `eval`에서 직접 못 부른다 → `Type.GetType("DesktopWindowControl.EditorTools.TextOverflowChecker, Assembly-CSharp-Editor")`로 `Run` 리플렉션 호출.
