---
date: 2026-09-29
title: 창고 정렬 기준 추가 · 찾기(검색·필터) (T-073 · T-069)
tags: [client, ui]
---

# 창고 정렬 기준 추가 · 찾기(검색·필터) (T-073 · T-069)

## 목적 / 배경
- 자원 150종 이상 — 등급 정렬 하나와 눈으로 훑기로는 못 찾는다(2026-09-20 QA) → `tasks/T-073` · `tasks/T-069`

## 변경 내용
- `StorageSlotSource` — `StorageSortKey`(등급·이름·수량) · `SetFilter` · `GatherMatches`, 새 `StorageFilter`
- 공급자 3개 — 수량 비교(자원) · 산업 판정(자원 `ItemType` · 장비 `Industry`) · 캐릭터는 산업 필터 없음
- `StorageGridPresenter` — `SortCurrent(key, order)` · `FilterCurrent` · 0건 문구 · 탭 전환 때 떠나는 탭 필터 해제
- `StorageToolPresenter` + 씬 — 도구 줄 두 줄(75), 2줄에 검색·산업·등급·[초기화]
- 규칙은 `Storage 규칙.md` "정렬" · "찾기" · "도구 줄" 절

## 주요 결정 / 근거
- **필터는 "맞는 것 앞 → 안 맞는 것 뒤에 흑백"**. 처음 넣은 "맞는 것만 표시"는 실측에서 창고가 비어 보여 되돌렸다. 제자리+흐리게(WoW식)는 결과가 흩어져 기각
- 흑백은 **반투명이 아니라 회색 + 글씨 옅게** — 반투명은 빈 칸과 헷갈린다(`SlotView.AwayTint` 주석과 같은 이유)
- **자리 기억(`_place`)은 필터와 무관하게 전체 기준으로 계산**한다 — 거르는 중 채취·판매·정렬이 있어도 풀면 원래 배치
- 이름 비교는 `CompareOrdinal` — 한글 음절 유니코드 순서 = 가나다 순, 문화권 영향 없음
- 도구 줄에서 `HorizontalLayoutGroup`을 지우고 `VerticalLayoutGroup` + `Tool Row 1/2`로 바꿨다(사용자 확인 후)

## 후속 작업 / 주의사항
- ⚠️ 도구 줄이 40→75가 되며 판매 목록이 375→357.5로 줄었고, **특성 정보 영역(고정 높이)도 357.5로 다시 맞췄다.** 도구 줄·캔버스 높이를 또 바꾸면 같이 잰다
- ⚠️ T-044(드래그)가 오면 찾기 중에는 드래그를 막아야 한다 — 일감 파일에 적어 뒀다
- ⚠️ 흑백은 색 곱셈이라 아이콘 스프라이트가 들어오면 흑백 머티리얼이 필요하다
- 검색창은 수량 팝업의 `Amount InputField`를 복제했다 — 이벤트는 비웠고 `ContentType.Standard`로 바꿨다
