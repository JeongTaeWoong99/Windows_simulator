---
date: 2026-10-19
title: 화면 산업 순서 — 발굴을 맨 끝으로 (농사·채굴·벌목·사냥·발굴)
tags: [client, ui, industry]
---

# 화면 산업 순서 — 발굴을 맨 끝으로

## 목적 / 배경
- 사용자 지시(이슈 #79 논의 중): 발굴은 특수 산업이라 일반 산업 넷 오른쪽 끝에 둔다. 여러 화면을 MCP로 같이 바꾼다.

## 변경 내용
- `UI/Shared/IndustryLabel.cs` — `DisplayOrder` 신설. 화면 공용 표시 순서의 단일 출처
- 코드가 순서를 만드는 곳을 전부 이 순서로: 작업슬롯 선택 `BuildIndustryList` · `InventoryGridPresenter.StripIndustries`(= `DisplayOrder` 별칭 — 인벤 툴 필터·칸 툴팁·경매 문구가 탄다) · `TraitPresenter.Rows` · `AuctionSearchPresenter.IndustryOptions` · 큐브 자동 칩 `EnchantAutoPanelView.FocusSteps` · 치트 콘솔 특성 줄
- 씬(MCP): 작업슬롯 선택의 산업 버튼 줄·장비 필터 줄에서 발굴 버튼을 맨 끝 형제로, 인스펙터 배열(`industryButtons`·`industryIcons`·`equipFilterButtons`)도 같은 순서로. 경매장 분류 트리의 `Item Excavation Node Button`을 사냥 다음으로
- 프리팹 `CharacterStateRowView` — 적성 스트립 `Excavation Cell` 맨 끝 + 배열 재정렬(씬 카드 인스턴스도 따라옴 확인)

## 주요 결정 / 근거
- **enum(`EIndustryType`·`EnchantAutoFocus`·`AuctionSearchNode`) 값 순서는 안 바꿨다.** `EIndustryType`은 서버·DB 미러이고, 클라 enum도 씬에 정수로 직렬화됐을 수 있어 순서를 바꾸면 조용히 어긋난다 → 표시 배열로만 순서를 정한다
- 인스펙터 배열 순서 = 산업인 화면(산업 버튼·적성 칸·필터)은 **배열과 형제 순서를 같이** 바꿔야 한다 — 배열만 바꾸면 클릭이 다른 산업으로 간다

## 후속 작업 / 주의사항
- ⚠️ 씬 `DesktopWindow_Control.unity`는 작업 전부터 미저장 변경이 있어 **저장하지 않았다** — 사용자가 저장해야 씬 변경이 파일에 남는다
- 새 화면에서 산업을 늘어놓을 때 `Enum.GetValues(EIndustryType)` 대신 `IndustryLabel.DisplayOrder`를 쓴다
- `ArtValidator`(에디터 검사)의 산업 목록은 순서 무관이라 그대로 둠
