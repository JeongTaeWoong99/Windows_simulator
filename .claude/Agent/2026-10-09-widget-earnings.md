---
date: 2026-10-09
title: 위젯 상단 줄 — 수익 집계 · 초기화 버튼 · 아이콘화
tags: [client, ui]
---

# 위젯 상단 줄 — 수익 집계 · 초기화 버튼 · 아이콘화

## 목적 / 배경
- 위젯만 띄워 두고 방치할 때 "얼마나 벌었나"를 보이려고 자리만 있던 시간당·누적 칸을 채웠다.
- 환산 기준은 사용자와 정했다 → `GameDesign/design/ui/README.md` 6장 15.

## 변경 내용
- `PlayerDataModel` — `GatherValueEarned(long)` 이벤트 · `SumGainedValue`
- `UI/Widget/WidgetPresenter/WidgetEarningTracker.cs` 신설 — 누적·시작 시각만 갖는 순수 클래스(시계는 부르는 쪽이 넘긴다)
- `WidgetPresenter` — `resetButton`·`elapsedText` 추가, 측정 시간은 기존 Update에서 초가 바뀔 때만 다시 씀
- 씬: Top Panel 글자 4개를 `<X> Stat`(아이콘+글자) 그룹으로 감싸고 `Elapsed Stat` 추가. `Reset Button` = 열기 버튼 복제, 왼쪽 44px
- `Assets/Sprites/ui/widget_*.png` 7종 — 16×16 흰색 임시 아이콘(PIL로 그림)

## 주요 결정 / 근거
- 보유 골드 증감이 아니라 **수확 증가분 × BasePrice** — 판매·가챠가 섞이지 않는다.
- 세션 기준(저장 없음) — 사용자가 추천안을 받아들였다. 이어 가기는 저장소가 필요해 미뤘다.
- 열기 버튼의 ↑는 글자가 아니라 이미지 — 다른 칸과 같은 방식으로 나중에 그림만 덮으면 된다.
- `/h`는 글자로 남김 — 단위는 글자가 빨리 읽힌다.

## 후속 작업 / 주의사항
- ⚠️ 증가분은 `ApplyItemChanges` **전에** 센다(Count가 총량). 순서를 바꾸면 늘 0.
- 서버에 붙어 실제 수확이 누적되는지는 사용자 실측 필요 — 에디터에서는 레이아웃·시간 흐름만 확인했다.
- `unity cmd capture_game_view`의 `save_path`는 `Assets/` 아래에 저장된다(`Temp/x.png` → `Assets/Temp/x.png`) — 찍은 뒤 지울 것.
- eval 스크립트에서 `Transform.Find("Open/Close Button")`는 슬래시를 경로로 읽어 null — 자식을 돌며 이름으로 찾는다.

## 업데이트 (2026-10-09)
- 로그인 전에도 측정 시간이 흐르던 것 수정 — `WidgetEarningTracker.Begin`을 로그인 성공 때 부른다. 시작 전 Reset은 멈춘 채로 둔다.
- 정보 칸 5개·버튼 2개에 `TooltipTrigger` 고정 문구. 툴팁은 Win32 커서 레이캐스트로 찾으므로 아이콘·글자의 `raycastTarget`을 켰다(버튼 위 아이콘만 끔).
- ⚠️ 첫 작업에서 Python 텍스트 모드 쓰기로 `WidgetPresenter.cs`·문서 두 개가 CRLF로 바뀌었던 것을 LF로 되돌렸다 — Windows에서 `open(p,'w')`는 줄바꿈을 바꾼다. `newline` 인자로 LF를 고정할 것.
