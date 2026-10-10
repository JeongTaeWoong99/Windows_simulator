---
date: 2026-10-10
title: 큐브 창 — 상급 큐브 BEFORE·AFTER 고르기 · 자동 큐브 · 공개 연출 (이슈 #53)
tags: [client, ui, enchant]
---

# 큐브 창 — 상급 큐브 고르기 · 자동 · 공개 연출

## 목적 / 배경
- 이슈 #53이 클라로 돌아왔다 — 서버(T-127)는 상급 큐브 결과를 보류하고 `C_EquipEnchantChooseRequest`로 고르게 한다.
- 사용자 요청으로 목업 3회(바탕화면 `큐브_개편_목업.html`)를 거쳐 고르기 화면 + 자동 큐브 + 연출을 함께 붙였다.

## 변경 내용
- `ServerPacketHandler` · `PlayerDataModel` — 고르기 응답 이벤트 `EquipEnchantChooseCompleted`
- `EquipEnchantPresenter` — 보류 그리기(캐시 기준) · [선택] · 떠나기 막기 · 보류 중 [사용] = 고르기(이전 값) → 큐브 · 자동 루프 · 연출 배선
- 신규: `EnchantAutoTarget`(목표·판정·PreferNew) · `EnchantForecast`(예상 확률) · `EnchantAutoPanelView`/`EnchantChipView`(설정 칩) · `EnchantRevealFx`(공개 연출)
- `InventoryTabPresenter.AddLeaveGuard` · 격자 우클릭 담기가 `TryClose` · `InventorySlotSource.GetBadge` → 장비 칸 `선택` 배지
- `GameDataLoader.EnchantOptions` · `ResultMessages` 615·616
- 씬: Compare Row(BEFORE|AFTER 좌우) · Action Row([사용] + [자동] 150px) · Auto Panel(4줄 칩, Compare Row와 같은 높이) · After Box에 `EnchantRevealFx`
- 문서: `Inventory 규칙.md` 큐브 창 절 · T-095

## 주요 결정 / 근거
- 상급 버튼은 [선택]×2 + [사용] 하나(메이플식) — 처음 목업의 버튼 넷은 사용자가 기각.
- 고르기 전 창 떠나기는 **막는다**(경고 팝업만 띄우는 안 기각) — 실수로 꺼질 위험. 막을 수 없는 종료는 서버가 보류를 남기고 배지로 다시 연다.
- 자동은 별도 빠른 경로가 아니라 **수동과 같은 요청·같은 연출**을 1.5배 빠르기로 — "즉시"·속도 선택은 사용자가 기각(너무 빠르다). 단축키 없음(마우스 게임).
- 비교 상자·설정 칸은 **같은 높이**로 번갈아 켠다 — 자동 중 상자가 출렁인다는 지적. 등급 사다리 UI는 삭제.
- 예상 확률은 등급 하나를 상태로 둔 마르코프 연쇄 — 등급이 안 내려가고 상급 자동도 오른 등급은 늘 남겨 두 큐브가 같은 식이다. 기획 1.4 평균(2,794.75개)과 맞는다.
- 연출 그림은 기존 fx(`gain_glow`·`reveal_sparkle`·`reveal_shine`) 재사용 — 새 PNG는 만들지 않았다.

## 후속 작업 / 주의사항
- ⚠️ AFTER 글씨는 `afterFx.SetResult`로만 쓴다 — 직접 `afterOptionsText.text`를 쓰면 돌기가 끊긴다.
- ⚠️ `afterFx.Play`는 대기 손잡이 `Succeed()` **앞**에서 — 닫힘이 Redraw를 불러 결과가 먼저 박힌다.
- 칸 조건 ✔ 표시(목업)는 넣지 않았다 — TMP 폰트에 ✔ 글리프가 있는지 확인이 먼저(기호는 EN 아틀라스를 다시 굽는다).
- 실측 남음(사용자) — 플레이 확인은 계정 1234로 했다: 고르기·떠나기 막기·일반/상급 자동·멈춤·목표 달성.
- `Server/Shared/game.sqlite3` 변경은 시험 플레이 흔적 — 커밋 제외.

## 업데이트 (2026-10-11) — 피드백 반영 · 프레임 튐 원인
- 자동 2배(`AutoSpeed` 2) · [자동 시작] 초록 / [■ 멈춤] 빨강(`PaintButton` — 의미색은 35% 어둡게 눌러 흰 글씨가 읽히게)
- 칸 조건 표기 `전체/낚시/낚시`, 규칙 줄 첫 줄에 목표(`DescribeCombos` — `[a] [b]`, 켠 것끼리 OR). 규칙 글씨 자동 크기 12~16
- `EnchantRevealFx` 재작성 — 목업 연출(줄별 돌기·차례 멈춤 · 등급 테두리 상시 · 번쩍·도장·입자 16/30 · 목표 입자 40 · [선택] 금색). BEFORE 상자에도 붙였다(`beforeFx`)
  - 돌기 글씨는 **결과 등급의 옵션만**(`RollFakeLine`) — 처음엔 8번 다시 뽑다 실패하면 아무 줄이라 다른 등급이 스쳤다
  - 처음 구현에서 연출을 줄였던 게 지적됐다 — **목업에 있는 연출은 빼지 말 것**
- 글씨·높이: 상자 제목 16 · 칸 20 · 규칙 16 · 칩 16 · 비교/설정 높이 108→132 · 규칙 줄 32→44
- 🔴 프레임 튐(프로파일 `ProfilerCaptures/…23-47-*.data`): 무거운 프레임(약 25ms)의 절반이 `CanvasUpdate.Layout` 8ms + `PreRender` 5ms(TMP 289개 재생성).
  큐브 창 → `Body Panel`(VLG) → `#Inventory Canvas`(그룹 없음)에서 레이아웃 더티가 멈춰 **루트가 `Body Panel`** — 격자 칸 전부가 같이 다시 잰다.
  큐브 창 글씨(돌기 45ms마다 · 진행 줄 · 버튼 글씨)가 바뀔 때마다 일어난다. 팝업으로 빼는 대신 **큐브 창 안쪽에 그룹 없는 부모를 한 겹** 두는 것을 추천(ugui-layout §7 1행) — 사용자 결정 대기

## 업데이트 (2026-10-11 ②) — 프레임 튐 전체 수정 · 등급 상승 자동 선택 · 공개 중 잠금
- 🔴 **레이아웃 경계 21곳** — `Body Panel`(메인·인벤토리·거래)·위젯 `Top Panel` 바로 아래 Presenter의 그룹을 꽉 채운 자식 `<이름> Panel`로 옮겼다(Presenter엔 LE·배경·스크립트만).
  경매 안쪽 Presenter·`Auction Page`·`Auction Buy Page`도 같다. `Trait Presenter` LE ph -1 → 0(내용을 따라가지 않게).
  자주 바뀌는 글씨는 상자로 감쌌다 — `WorkStationSlotView.prefab`의 `Remain Panel/Remain Text`(0.1초마다) · 큐브 `Before/After Box/Options Panel/Options Text`(돌기 45ms마다, 정렬 TopLeft로).
  규칙 글씨 자동 크기 제거(두 줄 고정). 재검사: Body Panel 루트에 물린 TMP 0개(전: 인벤토리 37 · 메인 48 · 거래 15).
  확인: `.Find("`·`GetChild(` 경로 의존 코드 없음. `GetComponentInChildren`·`transform.parent` 쓰는 곳(`InventoryTabPresenter.WakeTabScreens`)은 영향 없음.
- 등급 상승이면 새 값을 **알아서 고른다**(수동 포함) — `afterFx.Revealed` → Redraw → `ShouldKeepRankUp` → `RequestChoose(true)`.
  ⚠️ 고르기 응답이 동기화보다 먼저 와 고르기가 두 번 나갔다(`EnchantNoPending`) → `_chosenEquipId` + `IsPending`으로 틈을 막음. 고른 뒤 다시 굴리면 `OnEnchantCompleted`가 푼다.
- 공개 중(`afterFx.IsSpinning`) 큐브·[사용]·[선택]·[자동] 잠금(`interactable`, 자동 도는 중은 제외) — 실측: 돌기 중 [사용] 연타가 요청 1건만 보냄.
- 상자 제목 짧게(`BEFORE · 전설` · `AFTER · 영웅 ▶ 전설`) + 줄바꿈 없음/말줄임. 규칙 줄 두 줄 고정 — 설정: 목표+예상 / 칸 조건+한 번 확률, 도는 중: 진행+목표 / 칸 조건. 전부 켠 조합은 `칸마다 전체 또는 낚시`.
- 눈길 배분(사용자 결정): BEFORE 테두리 0.3 · AFTER 0.9 + **테두리를 따라 도는 빛**(`reveal_conic` + 테두리 마스크, 자기 Canvas — 회전이 큰 캔버스 배치를 매 프레임 다시 짜지 않게). 인스펙터 `emphasized`.
- 새 일감 T-145(위젯 알림 표시).

## 업데이트 (2026-10-11 ③) — 아래 창 넘침 · 마무리
- 경계를 둔 뒤 큐브 창 내용(391)이 격자와 나눈 몫(357.5)보다 커서 [사용]·[■ 멈춤]이 인벤토리 프레임 밖으로 빠졌다(사용자 지적).
  `LayoutElement.minHeight`로는 남는 자리가 min 위에 더해져 늘 커진다 → 공용 `ContentFloorLayoutElement`(Common `ugui-layout`, 마스터 반영) —
  몫을 부모 크기·형제 preferred/flexible로 계산해, 내용이 크면 그 축에서 min = preferred = 내용 · flexible 0. 자기 크기는 보지 않는다.
  큐브·응축·판매 목록 Presenter에 붙임(content = `<이름> Panel`). 실측: 큐브 391로 늘고 격자 357.5 → 324 · 응축(295)은 반 그대로.
- 스킬 `ugui-layout` §7 행 4개(경계 아래 넘침 · 자주 바뀌는 글씨 상자 · Auto Size · 도는 연출 자기 Canvas) — 사본 · 마스터 같은 편집.
- 사용자 실측 완료 → T-095 · T-127 보관, 이슈 #53 닫음.
