---
date: 2026-10-10
title: 뽑기 화면을 풀마다 카드로 (T-143)
tags: [client, ui]
---

# 뽑기 화면을 풀마다 카드로 (T-143)

## 목적 / 배경
- 거래 열 뽑기 화면이 버튼 8개(풀 4 × 1회·10회)를 격자로 나열해 어느 버튼이 어느 풀인지 글자를 읽어야 알았다.
- 사용자가 확정한 배치(슬레이어 키우기식 · 탭 없이 한 목록 · [P] 확률)대로 구현했다.

## 변경 내용
- `UI/Market/GachaPresenter/GachaPoolCardView.cs`(신규) — 카드 한 장. `Bind` · `SetState(gold, isWaiting)` · `DrawClicked(isMulti)`
- `GachaPoolSummary.cs`(신규) — 풀 하나의 괄호 · 종수 · 대표 아이콘 · 등급 확률 툴팁
- `GachaPresenter` — `DrawEntry[] draws` → `PoolEntry[] pools`(풀 Id + 카드). 요청은 카드마다 2개(`_requests[k*2]`·`[k*2+1]`)
- 프리팹 `Assets/Prefabs/GachaPoolCardView.prefab` · 씬: `Gacha Presenter`의 `FlexibleGridLayoutGroup` → VLG, `Pool Scroll Panel`(ScrollRect) 아래 카드 4장, 옛 `Gacha Send Button` 8개 삭제
- 문서: `Market 규칙.md` · `UI 배치 현황.md` · `UI 규칙.md` 폴더 트리

## 주요 결정 / 근거
- **[P]는 팝업이 아니라 툴팁** — 이 프로젝트의 확률 표시 선례가 큐브 확률 툴팁(`EquipLabel.BuildCubeRuleTooltip`)이다. 팝업이면 오버레이 Presenter + `UIManager` 중개가 하나 더 생긴다.
- **확률 분모는 네 시트 합** — 서버 `GachaPoolCatalog`가 아이템·캐릭터·장비·골드 시트를 같은 GachaId로 한 추첨기에 섞는다. 등급은 정의 테이블에서 읽는다(`Description` 안 읽음).
- 대표 아이콘은 임시 글자 대신 **가장 높은 등급의 첫 항목** — 그림이 이미 있다. 장비는 능력치 칸을 빼고 그린다(뽑기 전이라 빈 칸뿐).
- 테이블에 없는 풀의 카드는 끈다 — 켜 두면 버튼이 고장 난 것처럼 보인다.
- `Constants.GachaDrawSingle/Multi`가 `long`으로 생성된다 — 패킷 `DrawCount`(int)로 캐스팅.

## 후속 작업 / 주의사항
- 플레이 모드 확인: 카드 4장 · 확률 값 테이블과 일치 · 무기 10회 요청 → 결과창 → [10회 더 뽑기]. 계정 `1234`로 실제 뽑기 1회(무기 10회) 했다.
- 남은 실측: 툴팁 마우스 표시 · 골드 부족 잠금 · 작은 창 스크롤
- 풀이 늘면 `Content`에 프리팹 하나 + `Pools` 한 줄. 탭은 그때 나눈다.
- 낚시 교체(T-142 · #66)가 끝나면 무기·장비 이름이 바뀔 수 있지만 카드 문구는 테이블에서 읽어 손댈 곳이 없다.
- → tasks/T-143
