---
date: 2026-10-02
title: 산업 레벨 툴팁의 럭키 상자를 CommonRewardTable에서 읽기 (이슈 #49)
tags: [client, ui, data]
---

# 산업 레벨 툴팁의 럭키 상자를 CommonRewardTable에서 읽기 (이슈 #49)

## 목적 / 배경
- 서버가 상자를 드롭 테이블에서 빼 `CommonRewardTable`(레벨별 공통) + `CommonRewardOverrideTable`(산업별 덮어쓰기)로 옮겼다(10-01~02). 툴팁이 드롭 테이블의 상자 줄만 찾고 있어 `■ 럭키 상자` 묶음이 사라졌다.

## 변경 내용
- `GameDataLoader.GetIndustryBoxRewards` + `IndustryBoxReward` 구조체 — (산업, 레벨)의 상자 행. 캐시한다.
- `WorkStationSelectPresenter` — 자원은 드롭 테이블 전체, 상자는 위 함수로 따로 그린다(`AddBoxRow`). 옛 `IsBox` 분리 로직 삭제.
- `InventoryGridPresenter` — 개봉 수량 팝업 최대치를 `min(보유, Constants.BoxOpenMax)`로.
- 문서: `Main 규칙.md` 툴팁 표·설명, `Inventory 규칙.md` 상자 번호, T-033 상자 번호·개봉 수, `GachaResultPresenter` 주석의 99개.

## 주요 결정 / 근거
- 고르는 규칙은 서버 `CommonRewardCatalog.Roll`을 그대로 따랐다 — 덮어쓰기 행이 하나라도 있으면 공통 행을 **통째로** 대신한다(섞지 않는다). 규칙이 바뀌면 두 곳을 함께 고쳐야 한다.
- 상자 확률은 `ChancePerMillion / 1e6` 독립 확률이다. 자원 가중치 분모에 섞으면 자원 확률이 틀어진다.
- 0.03%가 있어 소수 셋째 자리까지 표시. `Count > 1`이면 이름 뒤 `×N`.
- 개봉 상한: 지금은 `MaxStack 50 = BoxOpenMax 50`이라 걸리지 않지만 엑셀에서 따로 바뀌므로 클라가 미리 자른다.

## 후속 작업 / 주의사항
- 실측(툴팁에 나무·은·황금 3줄 · 2% · 0.3% · 0.03%) 후 #49에 처리 코멘트.
