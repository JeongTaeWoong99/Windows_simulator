---
date: 2026-10-20
title: 도움말 아이콘(?) 넓히기 — 경매장 · 판매 등록 · 특성 · 응축
tags: [client, ui, tooltip, auction, trait, condense, localization]
---

# 도움말 아이콘(?) 넓히기 (T-144)

## 목적 / 배경
- 사용자 방향(2026-10-10, 2026-10-20 재지시): 자세히 설명해야 하는 규칙은 상시 안내문이 아니라 `?`로 뺀다.

## 변경 내용
- `AuctionText.BuildGuideTooltip`(경매장 제목 뒤) · `BuildRegisterRuleTooltip`(판매 등록 제목 뒤).
- `TraitPresenter.BuildHelpTooltip` — 머리 줄 왼쪽 `Spacer`(표 모서리) 안에 아이콘. `UI.Trait.DescYield`를 한 줄 요약으로 줄임.
- `CharacterCondensePresenter.BuildHelpTooltip` — 머리 줄 ★ 뒤 · [닫기] 앞. 단계별 누적·작업속도 표.
- 각 Presenter에 `helpTooltip`(`TooltipTrigger`, 필수 참조) — 씬에 `HelpIcon` 프리팹 인스턴스 4개 배치·연결.
- 경매장 머리 줄·판매 등록 머리 줄: 제목 flexW 1 → 0, 아이콘 뒤 `Spacer`(flexW 1) — 아이콘이 제목 바로 뒤에 붙게.
- `Loc_UI.xlsx` — 새 키 32개(`UI.Auction.Guide*`·`Rule*`, `UI.Trait.Help*`, `UI.Condense.Help*`·`StarN`), 수정 3개(`DescYield`·`CountHelp`·`RegisterEmptyCard`).

- 실측 수정: 특성 아이콘은 레이아웃 밖 자식이라 프리팹 기본 100×100이 그대로 나왔다 → `sizeDelta` 22×22.
  응축 머리 줄은 `childForceExpandHeight`라 아이콘이 48 높이로 늘었다 → 22폭 `Help Slot`(LayoutElement)에 넣고 아이콘은 가운데 22×22.
- 위젯 상단 줄에서 보유 골드(`Gold Stat`·`goldText`) 삭제 — 사용자 결정. 추정치(누적·시간당)만 남기고 보유 골드는 상태 패널. `Widget 규칙.md` 갱신.

## 주요 결정 / 근거
- 남긴 것: 입력에 따라 바뀌는 값(가격 범위·총액), 이 대상의 막힘 사유(응축 알림 줄), 일시 팝업 문구(`BuyNote`), 짧은 빈 상태 안내(시세 `PriceEmpty`), 우편 보관 한 줄.
- 수치는 `Constants`·`CharacterStarTable`에서 읽는다. 특성 포인트 "레벨당 1점"은 데이터가 아니라 서버 규칙이라 숫자 없이 적었다.

- `HelpIcon`을 `childForceExpandHeight` 줄이나 레이아웃 밖에 넣을 때는 감싸는 자리를 두거나 크기를 직접 준다 — `LayoutElement`만으로는 늘어난다.

## 후속 작업 / 주의사항
- 사용자 실측(툴팁 폭·줄바꿈, 특성 모서리 아이콘 위치) 후 커밋.
- #84(응축 계승)가 정해지면 `UI.Condense.HelpLost` 문구도 함께 바꾼다.
