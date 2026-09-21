---
date: 2026-09-22
title: 창고 탭 — 꺼진 채 저장된 SUB VIEW를 탭 줄이 깨우게 (특성 탭 빈 화면 수정)
tags: [client, ui, storage]
---

# 창고 탭 초기화 — `WakeTabScreens`

## 목적 / 배경

"창고를 처음 열 때 하위 View의 활성/비활성이 제대로 안 잡힌다"는 보고.
실제 결함은 **초기화가 SetActive를 전혀 하지 않는다**는 것이었다 —
초기 상태가 **씬에 저장된 `m_IsActive`에 통째로 의존**하고 있었다.

작업 시점의 씬에서 `Trait Presenter (↓ SUB VIEW)`가 `m_IsActive: 0`으로 저장돼 있었다
(커밋본은 `1`, 워킹 카피만 `0`). 그 결과:

- 꺼진 오브젝트는 `Start`가 돌지 않는다 → `TabChanged`를 **구독하지 못한다**
- 특성 탭을 눌러도 켜 줄 사람이 없다 → **영영 빈 화면**
  (탭 버튼은 멀쩡히 눌린다 — `HasScreen`이 보는 `traitScreen` 참조는 오브젝트가 꺼져도 non-null)

첫 진입만 보면 "Trait만 꺼져 있다"가 우연히 기대와 일치해서 증상이 늦게 드러난다.

## 변경 내용

- `UI/Storage/StorageTabPresenter/StorageTabPresenter.cs` — `Start`에서 `ShowTab` **직전에** `WakeTabScreens()`
- `UI/Storage/TraitPresenter/TraitPresenter.cs` — "씬에 켜진 채로 저장한다" 요구를 주석에서 제거
- `UI/Storage/Storage 규칙.md` — 새 절 "꺼진 채 저장돼도 깨어난다"

씬은 건드리지 않았다. 이제 `m_IsActive`가 무엇이든 결과가 같다.

## 주요 결정 / 근거

**깨우기(wake)를 골랐고, 탭 줄이 활성 상태를 직접 소유하는 안을 기각했다.**
기존 설계가 "화면을 여닫는 일은 각 Presenter가 스스로 한다"인데(`Storage 규칙.md`),
탭 줄이 `SetActive`까지 쥐면 같은 판단이 두 곳에 생겨 한쪽만 고쳐진다.
깨우기는 그 설계를 유지한 채 **"꺼진 채 저장하면 안 된다"는 숨은 불변식만** 없앤다.

**깨우고 → `ShowTab` 순서가 안전한 이유** (이 순서를 뒤집으면 안 된다):
이 시점엔 **아직 아무도 `TabChanged`를 구독하지 않았다.** 그래서 방금 켠 화면이
이벤트로 곧바로 다시 꺼지는 일이 없다 — 꺼지면 `Start`가 또 안 돌아 같은 덫에 빠진다.
각 화면은 자기 `Start` 끝의 `ApplyTab(CurrentTab)`(이미 있던 자기치유 호출)로 스스로 물러난다.
그 `Start`는 첫 `Update` 전 같은 프레임에 돌아 깜빡임이 없다.

**참조를 인스펙터에 늘리지 않고 타입 검색으로 찾는다** (`GetComponentInChildren<T>(true)`).
직렬화 필드를 늘리면 씬 배선이 필요한데, **미배선이면 수정이 조용히 무효가 된다** —
이 수정은 "배선 실수에 안 당하게 하는 것"이 목적이라 배선을 더 요구하면 앞뒤가 안 맞는다.
탭 줄과 화면들은 모두 `#Storage Canvas` 직속 형제라 `transform.parent`가 안정적인 기준이다.

**격자는 깨우지 않는다.** 구독이 아니라 `ShowTab`이 직접 부르고 초기화도 지연
(`EnsureInitialized`)이라 이미 스스로 깨어난다.

## 후속 작업 / 주의사항

- ⚠️ **탭에 따라 스스로 꺼지는 화면을 추가하면 `WakeTabScreens`에도 넣어야 한다.**
  지금 셋(Tool · Sell Cart · Trait)은 타입으로 하드코딩돼 있다. 빠뜨리면 증상이 같다.
- **Unity 에디터가 안 떠 있어 컴파일을 확인하지 못했다.** 다음에 에디터를 열 때 확인할 것.
- 실측 미완 — 특성 탭 진입/복귀, 첫 진입 시 Trait만 꺼짐, 탭 왕복이 확인 대상이다.
- 같은 세션에서 등록한 일감: `tasks/T-078` · `T-079` · `T-080` (전부 클라, 서버 변경 없음).
