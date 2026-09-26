---
date: 2026-09-25
title: 럭키 상자 표시 · 수량 팝업 문구 · 일감 전수 점검
tags: [client, ui, docs]
---

# 럭키 상자 표시 · 수량 팝업 문구 · 일감 전수 점검

## 목적 / 배경
- 상자를 좌클릭하면 수량 팝업이 "몇 개를 **팔까**?"로 물었다 — 팝업에 판매 문구가 박혀 있었다.
- 상자 드롭을 `CommonRewardTable`(산업·레벨 무관)에서 `<산업>BasicTable`의 신화 아래 2줄로 옮기기로 했다(사용자 결정).
  데이터 이관은 서버 몫이라 [이슈 #33](https://github.com/JeongTaeWoong99/Windows_simulator/issues/33)으로 넘겼다 → `tasks/T-030`.
- 같은 세션에서 끝났는데 남아 있던 일감을 정리했다(T-010·T-029·T-065 완료, T-022 폐기) → `tasks/archive/README.md`.

## 변경 내용
- `AmountInputPresenter.Open` · `UIManager.AskAmount` — 묻는 말(`question`)을 인자로 받는다. 창고 격자가 판매/개봉별로 넘긴다.
- `WorkStationSelectPresenter.BuildIndustryLevelTooltip` — 상자 줄(`GameDataLoader.IsBox`)을 `■ 럭키 상자` 묶음으로 자원 뒤에 뗀다.

## 주요 결정 / 근거
- **확률 분모는 자원+상자 합친 가중치다.** 서버가 한 풀에서 뽑으므로 묶음별로 나누면 합이 100%를 넘는다.
- 상자 판별은 드롭 테이블에 표식 컬럼을 새로 두지 않고 `ItemTable.OpenGachaId`(기존 `IsBox`)로 한다 — 서버와 같은 판정이고 스키마 변경이 없다.
- 팝업은 enum이 아니라 문자열로 묻는 말을 받는다 — 용도가 둘뿐이고 팝업이 용도를 알 필요가 없다.

## 후속 작업 / 주의사항
- 서버가 #33대로 데이터를 옮기기 전까지 툴팁에 `■ 럭키 상자`는 **나타나지 않는다**(지금 BasicTable에 상자가 없다). 이관 뒤 실측 → T-030.
- 에디터 컴파일만 확인했다. 팝업 문구·툴팁은 플레이로 확인하지 않았다.
