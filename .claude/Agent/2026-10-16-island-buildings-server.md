---
date: 2026-10-16
title: 섬 건물 서버 — 레벨·트리·속도/산출량 노드·자원 비용 (T-166)
tags: [server, data, design]
---

# 섬 건물 서버 (T-166)

## 목적 / 배경
- T-105에서 정한 섬 산업 건물의 첫 구현. 범위는 레벨 + 트리 + 수치 노드(속도·산출량). 희귀도·기능 노드는 뒤로(진우).
- 설계 → `GameDesign/design/building/README.md` 3장 · 해금 규칙 → `unlock/README.md` 2.1·2.5·3장

## 결정
- 건물 레벨·노드 = `UnlockTable` 행(`4xxx`) — 상태 저장·패킷·마이그레이션 없음. 열린 해금 목록이 곧 건물 상태
- 자원 비용 `ItemTIDs`·`ItemCounts` — 지불 수단이 아니라 함께 내는 조건. 골드·자원을 전부 확인한 뒤 뺀다(되돌리는 코드 없음)
- 노드 효과는 `UnlockCatalog`가 역색인(`TryGetBuildingNode`) — 테스트가 표를 주입하는 기존 이음새를 그대로 쓴다
- 속도는 슬롯 내역 `TraitAddPermille`에 합침(특성·아티팩트와 같은 자리) — 패킷 모양 유지
- 수치 노드를 열기 전 정산, 속도면 연 뒤 `ApplyWorkStationSpeed` — `ApplyUnlock`에 둬서 `GrantUnlock`도 같은 경로

## 함정
- openpyxl `insert_cols`는 피했다(값만 옮김). `cell(r, c, None)`은 값을 지우지 않는다 — `.value = None`
- **`format-excel.py`가 ClosedXML의 묶인 열(min~max)을 풀지 않아 `Localization.xlsx`의 열 정의가 겹쳤다** → 생성기가 번역 시트를 저장할 때 `same key: 5`. 새 번역 키가 생길 때마다 터진다. `fit_widths`에서 낱개로 풀게 고쳤다

## 검증
- 서버 테스트 1014개 통과 (`UserBuildingTest` 11 · `UnlockCatalogTest` +5, 실데이터 해금 6 → 36)
- 새 미러 파일 4개의 `.meta`는 손으로 만들었다(새 GUID)

## 남은 것
- 클라 건물 화면 — 이슈 초안 확인 대기
