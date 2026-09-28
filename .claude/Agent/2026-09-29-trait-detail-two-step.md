---
date: 2026-09-29
title: 특성 — 누르면 정보부터, [배우기]로 찍는다 (T-079)
tags: [client, ui]
---

# 특성 — 누르면 정보부터, [배우기]로 찍는다 (T-079)

## 목적 / 배경
- 노드를 누르는 즉시 팝업이 떠 읽고 결정할 자리가 없었다 → `tasks/archive/T-079-특성정보표시후해금.md`

## 변경 내용
- `TraitPresenter.cs` — 노드 클릭은 `_selectedTraitTid`만 바꾸고, 기존 판정·팝업은 `OnConfirmClicked`로 이동. 정보 영역은 `Redraw` 끝의 `RedrawDetail`이 그린다
- `TraitNodeView.cs` — 잠긴 노드도 누를 수 있게 · `Selected Mark`(4변 테두리) · 빈 칸(TID 0)만 비활성
- 씬 `Trait Presenter > Trait Detail Panel`(375 고정) · `TraitNodeView.prefab`에 `Selected Mark`

## 주요 결정 / 근거
- **정보 영역에 View 클래스를 두지 않았다** — `UI 규칙.md` §0: View 클래스는 반복 칸만. Presenter가 위젯을 직접 쥔다
- **잠김 회색을 `disabledColor`가 아니라 네 상태 전부에 칠한다** — 잠긴 노드를 누를 수 있게 하면 `disabledColor`는 쓰이지 않는다
- **[배우기]는 배운 특성에서만 숨긴다** — 조건 미달·포인트 부족이어도 눌러서 기존 알림 문구를 보게 한다
- **높이 375 = 자원 탭 판매 목록의 실제 높이**(격자와 flex 1:1로 나눈 값). 사용자 요청

## 후속 작업 / 주의사항
- ⚠️ `Trait Presenter` 세로 배치는 **패딩 0 · 간격 5**여야 판매 목록과 픽셀이 맞는다(패딩 4·간격 4일 때 4px씩 어긋났다). 창고 캔버스 간격·높이를 바꾸면 375도 다시 계산해야 한다
- 에디터에서는 창고 패널이 전부 켜진 채 저장돼, 375 고정 영역 때문에 격자 칸 RectTransform 값이 한꺼번에 바뀌었다(씬 diff 대부분이 그것)
