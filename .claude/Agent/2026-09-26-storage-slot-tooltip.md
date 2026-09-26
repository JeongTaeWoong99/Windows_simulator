---
date: 2026-09-26
title: 창고 칸 툴팁 — 칸에 다 못 담은 정보를 올리면 띄운다
tags: [client, ui, docs]
---

# 창고 칸 툴팁 (T-050)

## 목적 / 배경
- 좌클릭 상세 패널 자리를 판매 목록이 가져간 뒤(T-032) 칸 정보를 볼 곳이 없었다.
- 호버 본체는 T-088 공용 툴팁으로 이미 섰다 — 이번엔 칸에 트리거를 달고 내용만 만든다.

## 변경 내용
- `StorageSlotSource.BuildTooltip(key)` 가상 메서드 + `AddRarityRow` 보조 — 탭별 공급자가 내용을 만든다
  - 자원: 등급 · 보유 · 판매가(개당 · 전부) · 조작 안내(상자면 좌클릭 열기)
  - 캐릭터: 등급 · 레벨/경험치% · 배치처(`슬롯 N · 산업 Lv.L`) · 산업별 적성 + 기본 속도
  - 장비: 등급 · 종류 · 효과 · 장착자(캐릭터 · 부위)
- `StorageGridPresenter.AttachTooltip` — 칸을 만들 때 `TooltipTrigger`를 코드로 붙이고, 띄우는 순간 `view.Key`를 읽는다
- `StripIndustries` 공개(툴팁 적성 줄이 같은 순서) · 레벨 문구를 `CharacterSlotSource.GetLevelLabel`로 합침
- 추가 `UI/Shared/RarityLabel.cs` — `StorageToolPresenter`의 등급 이름 배열을 여기로 옮김
- `EquipLabel.GetKindName` 추가
- 문서: `Storage 규칙.md` · `Shared 규칙.md` · `UI 배치 현황.md`

## 주요 결정 / 근거
- **내용은 격자가 아니라 공급자** — `IsAway`와 같은 이유(무엇을 보일지 탭마다 통째로 다르다).
- **트리거를 프리팹에 두지 않는다** — 가챠 결과가 같은 칸 프리팹을 쓰는데 거기선 툴팁 대상이 아니다. 씬·프리팹 변경 없음.
- 엑셀 `Description`은 기획 메모라 쓰지 않았다(플레이어 문구는 T-093).
- 기본 속도는 적성만의 값 — 장비·특성 가산은 빠진다(실제 속도는 작업슬롯 화면).

## 후속 작업 / 주의사항
- 2026-09-26 사용자 실측 확인 → T-050 완료·보관(같은 커밋).
- 인챈트 등급·옵션 표기는 T-095에서 장비 툴팁에 더한다.
- `RarityLabel`·`IndustryLabel`은 사본 — T-085에서 표시 이름이 엑셀로 가면 지운다.

## 추가 — 툴팁 열 폭을 내용에 맞춘다 (같은 날)
- 사용자 실측: 캐릭터 툴팁 `상태` 값(`슬롯 1 · 낚시 Lv.1`)이 `슬롯 1 ·…`로 잘렸다. 원인은 줄 프리팹의 값 80px · 보조 값 130px 고정폭.
- `TooltipRowView.MeasureColumns`/`SetColumnWidths` + `TooltipPresenter.FitColumns` — 툴팁마다 열별 최대 글자 폭(`GetPreferredValues`, 올림)을 재서 모든 줄에 같은 폭을 준다. 열 정렬은 유지되고 패널은 `ContentSizeFitter`로 늘어난다.
- 프리팹 값은 그대로 두었다(코드가 덮는다). 산업 레벨 툴팁도 같은 경로라 함께 바뀐다 — 보조 값 열이 130 고정보다 좁아질 수 있다.

## 추가 — 툴팁 NRE 수정 (같은 날, 커밋 `ac4a84d` 이후)
- 증상: 산업 레벨 버튼에 올리면 `TooltipRowView.SetColumnWidths`에서 NRE.
- 원인: 간단형 툴팁(줄 0개)이 `Row Panel`을 끈 뒤, 줄이 있는 툴팁이 **꺼진 부모 아래에 새 줄을 만들면 `Awake`가 미뤄진다** → `_valueLayout` 미할당. 창고 칸에서는 줄이 먼저 만들어져 드러나지 않았다. `_defaultColor`도 같은 잠복 버그였다.
- 수정: `Render`가 줄 영역을 **채우기 전에** 켠다 · `TooltipRowView`는 공개 메서드마다 `EnsureInitialized`로 스스로 보장(호출 순서에 기대지 않는다).
- 확인: 꺼진 부모 아래 인스턴스화 → Bind·Measure·SetColumnWidths가 예외 없이 돈다(에디터 재현).
