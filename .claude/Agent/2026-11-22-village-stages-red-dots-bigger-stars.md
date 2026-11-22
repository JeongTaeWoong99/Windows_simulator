---
date: 2026-11-22
title: 마을 그림 단계(건물 5단계 · 바탕 5단계) · 빨간 점 · 거점 → 마을 · 별 키우기 · 큐브 자동 편의 줄
tags: [client, outpost, art, ui, inventory, data]
---

# 마을 그림 단계 · 빨간 점 · 거점 → 마을 · 별 키우기

## 목적 / 배경
- 사용자: 캐릭터 칸 별을 경험치 막대처럼 위아래 안 닿게 크게 · 화면 이름을 진짜 '마을'로 · 올릴 건물/찍을 특성이 있으면 빨간 점 · 건물마다 5단계 + 전체 레벨로 마을 바탕 성장(건물 자리 고정 · **지형은 바뀌면 안 된다** — 강이 사라지는 식 금지).
- 같은 날 지시: 플레이·서버는 내가 직접 끄고 켠다(메모리 `no-compile-during-user-play`에 적음).
- 커밋하지 말라는 지시가 이어진다 — 변경만 해 둠.

## 변경 내용
- **별** — `SlotView.prefab` Star Row 12×42(별 10×10 넷, y 1·11·21·31). 적성 알약(위 27)과 '배' 표시(71) 사이.
- **거점 → 마을** — `Loc_UI.xlsx` `UI.Main.Outpost` · `UI.Outpost.HelpTitle` = 마을 / Village, 파이프라인 돌림. 치트 검색어에 마을·village 추가 · `UIManager` 툴팁 · 기획 `building/README.md` 이름 줄 확정.
- **빨간 점** — `PlayerDataModel.CanLearnAnyTrait(tid)` · `HasVillageNotice()`. `MenuPresenter`(구독 5개 · `outpostNoticeDot`) · `OutpostMapPresenter`(메뉴 두 버튼) · `OutpostBuildingPresenter`(탭 둘 · 구독 4개 추가). 그림 `Assets/Sprites/ui/notice_dot.png`(14px 도트). 씬에 `Notice Dot` 5개(하단 18px · 메뉴/탭 18px, 오른쪽 위 안쪽).
- **그림 단계** — 생성기 `make_outpost.py`에 `ground(stage)` · `TIERS`(빈 터 · 천막 · 오두막 · 집 · 정식 그림 · 완성). 출력 `building_<TID>_0~5` 30장 · `ground_1~5` 5장, 옛 `building_1~5` · `ground` 삭제(참조 0 확인 뒤). 임포트 설정은 옛 그림에서 복사(Point · 무압축 · Single FullRect).
- `OutpostArtStage.cs`(새 파일) — 건물 단계 1·20·50·90·130 / 바탕 단계 합 50·200·400·600. `OutpostBuildingView.tierSprites`+`art` · `OutpostMapPresenter.groundImage`+`groundStages`. 프리팹 `OutpostBuildingView` 그림도 새 것으로.
- 문서 — `Outpost 규칙.md`(그림 단계 · 빨간 점 절) · 생성기 머리말.

## 주요 결정 / 근거
- 지형 고정은 생성기에서 보장 — 나무·덤불 자리를 **늘 5단계 길 폭·전 단계 소품**을 피해 한 번만 고르고, 소품 그리기 난수를 소품마다 따로 굴린다(소품이 빠져도 나무 색이 그대로).
- 건물 단계는 모두 같은 캔버스·바닥선·**문 x**(`DOOR_X`) — 바닥의 문앞 길이 그 문으로 들어온다.
- 4단계 = 검수 끝난 옛 정식 그림 그대로, 5단계 = 그 위에 돌 마당 · 처마 깃발 줄 · 금 등 · 반짝임.
- 하단 메뉴 배지 금지(P1)는 [마을] 점만 사용자 예외로 주석에 남겼다 — 시간이 아니라 할 일이 있을 때만.

## 검증
- 플레이(로컬 서버 · 시험 계정 `claude-village` — 사용자 계정은 안 건드림) · 치트로 Lv 25/60/100/140/0 → 그림 2·3·4·5·0, 합 325 → 바탕 3. 작업장(특성 가능)에서 하단 [마을] 점 · 메뉴 [특성] 점 · 건물 창 [특성] 탭 점 켜짐, 나머지 꺼짐. 스크린샷 확인 후 플레이를 껐다.

## 후속 작업 / 주의사항
- 바탕 5장을 배열로 들고 있어 함께 메모리에 오른다(≈28MB 무압축). 무거우면 단계 하나만 불러오기로.
- 로컬 DB에 시험 계정 `claude-village`(건물 레벨 치트)가 남았다 — DB는 커밋 대상 아님.
- 큐브 자동 편의 토글 · '나올 수 있는 조합 N개 + 툴팁'은 의견만 답함(코드 변경 없음).

---

## 2차 (같은 날) — 건물 단계를 이름에 맞게 · 이름판 · 큐브 자동 편의

### 배경
- 사용자: 1단계가 모두 천막이라 느낌이 안 산다 — 팰월드(원시 작업대 → 상급 작업대 → 공장)처럼 **이름에 맞는 특색**으로 자라야 한다. 이름판·글자 키우고 그림 가운데에 · ▲ 초록 표시 대신 **빨간 점으로 통일** · 큐브 자동 의견(편의 토글 · 조합 개수 + 툴팁)대로 진행.
- 대답이 영어로 나간 일을 지적받아 전역 `CLAUDE.md`·메모리 `always-answer-in-korean`에 적었다.

### 변경
- 생성기 — 1~3단계를 건물마다 따로(`T1`·`T2`·`T3` — 표는 `Outpost 규칙.md` 그림 단계) · 5단계 건물별 하나 더(`GRAND_EXTRA`) · 그림 가로 가운데를 `_centers.json`으로. 그림 30장 덮어씀(같은 이름 · GUID 유지) + 스프라이트 피벗 x = 가운데.
- `OutpostBuildingView` — `upgradeMark`·`pointMark`·`pointText` 삭제 → `namePlate` · `noticeDot`. `Bind(name, category, level, hasNotice)` · 피벗으로 이름판 x. 프리팹: 표시 둘 삭제 · 이름판 y −26(바닥선 밑) · 글씨 26/23/19 · 여백 +4 · `Notice Dot` 20px 오른쪽 위 귀퉁이.
- `OutpostMapPresenter` — 점 = 올리기 가능 ∨ `CanLearnAnyTrait`.
- 큐브 자동 — `EnchantAutoTarget.StopOnMythic`·`StopAboveGoal`(기본 켬)·`StopsEarly`·`GetKindName` · `EnchantForecast.ListOutcomes` + 예상에 편의 멈춤 반영(실패로 셈) · 패널 `optionChips`(신화면 멈춤 · 넘으면 멈춤 · 칸 초기화) · 규칙 줄 둘째 줄 `나올 수 있는 조합 n가지` + `comboTooltip`(확률 순 10개 + 외 n가지) · 자동 루프 `AutoEndMythic`/`AutoEndHigher`. 씬 `Option Row`(Cap Row 복제) · 패널 248 · 창 577 · 규칙 글씨 raycast 켬 + `TooltipTrigger`.
- `Loc_UI.xlsx` — `UI.Outpost.Hint` 수정 + `UI.Enchant.*` 10개 추가. 파이프라인 돌림.

### 결정 / 근거
- 내 의견의 '상위 등급도 성공'은 이미 동작이었다(`Matches`가 등급 **이상**) — 그래서 '목표보다 높으면 칸 무시하고 멈춤'(`넘으면 멈춤`)으로 구현했다.
- 이름판 −14에서도 1단계 물건 발치가 가려져 −26으로 내렸다(스크린샷 확인).

### 검증
- 플레이(시험 계정) — Lv 5/10/30/1/40 · 25/60/100/140/0 두 벌 스크린샷: 건물별 그림 · 이름판 가운데 · 점 확인. 큐브 계산 — 조합 확률 합 = 한 번에 확률, `StopsEarly` 전설→신화만 참, 토글 끄면 거짓. 큐브 창 미리보기 렌더로 편의 줄 배치 확인. 큐브 자동을 실제로 돌려 보지는 못했다(시험 계정에 장비 없음).
