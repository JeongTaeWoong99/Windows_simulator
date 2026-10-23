---
date: 2026-10-22
title: 토스트 · 거절 흔들기 · 끌어 놓기 마무리 · 알림 창 연출 (T-177)
tags: [client, ui, fx]
---

# 토스트 · 거절 흔들기 · 끌어 놓기 마무리 · 알림 창 연출 (T-177)

## 목적 / 배경
- 짧은 안내·조작 거절까지 전부 확인 버튼 모달(`NoticePresenter`)이라 흐름이 끊겼다. 리소스 없이 되는 폴리싱 넷을 한 번에.

## 변경 내용
- `ServerWaitManager` — `ToastRaised` · `RaiseToast` · `RaiseRefusal(문구, 대상?)` 창구 추가
- `UI/Overlay/ToastPresenter` 신설 + 씬 `!Overlay Canvas` 7번(로딩 위 · 알림 아래)
- `UI/Shared/RefusalShake` 신설 · `ButtonPressFx.RecentlyPressed`
- `RaiseNotice` 41곳 분류 — 거절 30곳 → `RaiseRefusal` · 완료 안내 6곳 → `RaiseToast` · 모달 유지 4곳(큐브 대기 · 작업표시줄 · 보상 우편행 · 잠금 서버 거절)
- `InventoryGridPresenter` — 놓은 뒤 그림이 날아가 사라짐(`FlyGhost`) · 성공 시 놓은 프레임이 튐(`PlayLanding` · `s_landing`)
- `NoticePresenter` — `PanelTransition.SetVisible` + `panel` 참조
- 문서: `Overlay 규칙.md`(알림과 토스트 절) · `Shared 규칙.md` · `Inventory 규칙.md`

## 주요 결정 / 근거
- 흔들기는 **회전**이다 — 버튼·칸이 레이아웃 그룹 아래라 위치를 흔들면 도중 재배치에 엉뚱한 자리로 돌아간다. 회전은 `ButtonPressFx`의 크기 트윈과도 안 겹친다.
- 흔들 대상 기본값은 `ButtonPressFx`가 기억한 "0.5초 안에 누른 버튼" — 호출부 30곳에 버튼 참조를 넘기지 않으려고. 버튼이 없는 입력(칸 우클릭·끌기)만 칸을 넘긴다.
- 서버 실패 코드는 토스트로 내리지 않았다 — 클라가 몰랐던 사정이라 읽고 넘어가야 한다.
- 튀는 대상은 `SlotView`가 아니라 프레임 — 응답 뒤 다시 그려도 프레임은 그 자리에 있다. `s_landing`은 `Succeed` **전에** 소비한다(닫히면 `OnSlotWaitClosed`가 비운다).

## 후속 작업 / 주의사항
- ⚠️ **씬을 `RunCommand` 안에서 `EditorSceneManager.SaveScene`으로 저장하면 레이아웃 구동 값(앵커·크기)이 전부 구워져 diff가 3,000줄이 된다.** 기존 씬은 그 값을 0으로 저장한다. 씬을 되돌리고 `unity cmd save_scene`으로 저장하니 추가 310줄만 남았다.
- ⚠️ 씬 오브젝트를 복제(`Instantiate`)하면 하위 `LocalizedText`가 편집 중에 켜져 NRE가 콘솔에 남는다(언어표 미로드) — 판·글자를 새로 만들고 `UIThemeColor`만 `CopySerialized`로 옮겼다.
- `unity cmd eval`은 일부 런타임 필드를 빈 값으로 읽는다(`_frames` 0개) — 직렬화 필드(`slotParent`)로 우회했다. MCP `RunCommand`는 `System.Reflection`을 막는다.
- 치트 창 안전장치(`CheatGuard`)는 직접 띄운 서버를 인식하지 못해 모달을 띄우고 에디터를 멈춘다 — 테스트는 `C_CheatRequest`를 직접 보냈다.
- 실측 남음: 실제 마우스로 버튼 거절 흔들기 · 끌어 놓기 체감.
