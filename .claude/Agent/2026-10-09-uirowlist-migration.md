---
date: 2026-10-09
title: 목록 줄 풀을 UIRowList로 옮김 (T-133)
tags: [client, ui, refactor]
---

# 목록 줄 풀을 UIRowList로 옮김 (T-133)

## 목적 / 배경
- 화면마다 복제된 "늘리고 남는 줄은 끄기" 코드를 `Common/object-pool/UIRowList<T>`로 모은다 → tasks/T-133
- 동작은 그대로인 리팩토링

## 변경 내용
- 판매 카트 · 우편 · 특성 · 툴팁 · 가챠 결과 · 작업대 선택(캐릭터·효율·장비 줄 3종) — `GetOrCreateRow`/`HideRowsFrom` 계열 삭제, `_rows.Get(i)` · `_rows.HideFrom(n)` · 전체 순회는 `_rows.All`
- 줄 구독(클릭 등)은 `onCreated`, `Clear()`는 `onHide`로 옮겼다

## 주요 결정 / 근거
- `UIRowList`는 생성자에 프리팹·부모가 필요해 필드 초기화가 안 된다 → 각 `Start`의 RequireRef 직후에 만든다
- 가챠 결과: 공개 연출 목록 `_revealFxs`는 인덱스로 쓰는 곳이 많아 그대로 두고 `onCreated`에서 추가한다(줄이 앞에서부터 하나씩 생겨 순서가 같다). `onHide`는 인덱스를 모르므로 `GetComponent<RewardRevealFx>()`로 찾는다
- 툴팁: `FitColumns`는 켠 줄만 `_rows.All[i]`로 잰다 (0..count-1이 켠 줄)

## 후속 작업 / 주의사항
- ⚠️ 작업대 선택은 `Start`의 `CloseEquipPicker()`가 장비 줄을 비운다 — 줄 목록 생성을 그보다 앞에 둬야 한다(NRE)
- `HideFrom`은 이미 꺼진 줄에도 `onHide`를 부른다 — `Clear()`가 여러 번 불려도 안전해야 한다
- 범위 밖: 인벤토리 격자·작업대 목록·위젯 띠의 `Instantiate`는 칸 자리(프레임)·키별 배치라 이 풀이 맞지 않는다
- 실측 남음 — 목록이 늘고 줄 때 옛 내용이 비치지 않나 · 줄 클릭이 중복으로 불리지 않나
