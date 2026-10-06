---
date: 2026-10-07
title: 캐릭터 장착 네모(EquipPipsView) · 상반신 여백 제거
tags: [client, ui, editor, art]
---

# 캐릭터 장착 네모(EquipPipsView) · 상반신 여백 제거

## 목적 / 배경
- T-104 — 캐릭터가 무슨 장비를 꼈는지 인벤토리 캐릭터 칸·작업슬롯 칸에서 보이게. T-103과 묶어 진행 → `tasks/T-104`, `tasks/T-103`
- 사용자 제기: warrior 상반신이 칸 안에서 작게 떠 보인다(상하좌우 여백)

## 변경 내용
- `UI/Shared/EquipPipsView` + `Assets/Prefabs/EquipPipsView.prefab`(50x11, 네모 4개 13 간격) — `SlotView`·`WorkStationSlotView` 프리팹에 중첩
- `PlayerDataModel.FindWornEquip` · `EquipLabel.WornSlots`·`ReadWornGrades`·`AddWornRows` — 두 화면이 같은 판정을 쓴다. `WorkStationSelectPresenter.FindWornEquip`도 이것을 부른다
- `RarityPalette.EmptySocket` — `SlotView`·`ItemIconView`에 각자 있던 #2A2A2A를 한 곳으로
- `SlotView.TintColor`(static) — 칸 안 네모도 같은 딤·흑백 규칙
- `Editor/art-import/ArtBaker` — 상반신을 `Upscale`(캔버스 덧대지 않음) · `AutoPortrait` = 발에 붙인 최소 정사각형. hero-knight·warrior 다시 구움(그림 저장소)

## 주요 결정 / 근거
- 위치는 바탕화면 HTML 목업(시안 A~D)으로 사용자가 골랐다: 캐릭터 칸 = **LV 배지 줄 · 오른쪽 끝을 적성 스트립 오른쪽 끝(x -1)에** — 왼쪽 게이지가 스트립 왼쪽 끝과 맞는 것과 짝. 세로(C·D)는 능력치 칸과 형식이 갈려 기각
- 슬롯 설정 캐릭터 카드에는 두지 않는다 — 장비 칸 4개가 바로 옆에 보인다(사용자 결정)
- ~~네모 간격 13~~ → 업데이트에서 14로(배지 폭을 줄였다)
- 여백의 원인은 레시피 값이 아니라 `Fit`이었다 — 64 캔버스 가운데에 정수 배로만 키우니 크롭 33~63px은 1배로 남았다. 비정수 배율(점 크기 들쭉날쭉) 대신 캔버스를 없앴다. 화면이 전부 UI Image `preserveAspect`라 결과 크기가 캐릭터마다 달라도 된다
- ~~hero-knight·warrior는 레시피를 0(자동)으로 둔 채~~ → 업데이트에서 레시피에 직접 적었다. black-knight·bringer-of-death는 직접 적은 값 그대로(결과 64 동일)

## 후속 작업 / 주의사항
- **실측 못 했다** — 플레이 모드가 로그인 화면에서 멈췄고 투명 창이라 캡처가 검다. 사용자 실측 필요(T-104 할 일)
- 그림 저장소 다시 굽기 중 `AssetDatabase.SaveAssets()`가 사용자가 에디터에서 다듬어 둔 배경 SO 5개(`groundHeight`·`stageHeight`)를 디스크에 저장했다 — 사용자 값이다, 되돌리지 말 것
- 그림 저장소(`Assets/Art/`)는 커밋하지 않았다 — 코드보다 먼저 푸시해야 한다(`Art 규칙.md`)

## 업데이트 (2026-10-07) — 사용자 피드백 반영
- 크롭 기준을 사용자가 정했다 → `Assets/Art/Art 규칙.md` "상반신 · 머리 크롭"(표에 네 캐릭터 값). 앞 절의 "아래는 발에 붙인다"는 **틀린 추정이었다** — 상반신은 머리·상체 중심, 가로는 몸통 가운데(무기 잘려도 됨)
- 머리 여백의 원인: 크롭 14·12를 32 캔버스에 2배로 놓아 28·24 → 가운데 정렬 여백. 크롭을 16으로 넓혔다. `AutoHead`도 캔버스 약수로 올린다
- 작업슬롯 칸은 색 네모 대신 `EquipIconsView`(같은 스크립트, 아이콘 배열을 채운 프리팹). 인벤토리 칸은 색 네모 유지 — 100px 칸이라 아이콘이 안 읽힌다
- 인벤토리 칸 여백 4px: 네모를 능력치 칸과 같은 11px·14 간격으로 맞추느라 **LV 배지 폭 36 → 26**(LV.MAX는 글자 자동 축소). 네모를 줄이는 대안은 "장비처럼"과 어긋나 기각
- 슬롯 설정 장비 칸 아이콘이 안 보인 원인: `Icon (임시)` 이미지가 `EquipSlotButton`에 필드조차 없었다
- ⚠️ 씬을 `SaveScene`으로 저장하면 레이아웃이 계산한 RectTransform 1,600여 줄이 함께 바뀐다 — HEAD 씬에 참조 4줄만 직접 넣었다. 다음에도 씬 참조 연결은 YAML에 필요한 줄만 넣는 쪽이 낫다

## 업데이트 (2026-10-07) — 상반신 2차
- 사용자 기준: 상반신은 **몸 전체가 아니라 머리+상체가 꽉 차게**(bringer-of-death·warrior가 기준). black-knight 32 → 21, hero-knight 32 → 21. 네 캐릭터 모두 키의 약 60%를 위에서부터. 자동 크롭도 60%로
- ~~굽기 직후 중간 상태~~ → **틀린 진단이었다.** black-knight 공격 순서(1 2 1 3)는 **사용자가 SO에서 다듬은 값**이었고, 굽기가 레시피 순서로 덮은 것을 내가 다시 구워 HEAD로 되돌렸다. 사용자 값으로 복구했다

## 업데이트 (2026-10-07) — 굽기가 SO를 덮지 않게
- 사용자 지시: 에셋은 처음 한 번만 설정대로 만들고, 그 뒤 사용자가 다듬은 값은 절대 건드리지 않는다
- `ArtBaker` 캐릭터 굽기: 프레임·공격 칸·상반신·머리를 **SO가 새로 생겼거나 칸이 비었을 때만** 채운다(`FillArrayIfEmpty`·`FillReferenceIfEmpty`). 배경 `stageHeight`도 처음 한 번만. 로그에 "SO 값 유지" 표시
- 문서: `Art 규칙.md` 2절·타격 타이밍 절(SO의 `hitFrame`을 직접 고친다) · `art-import 규칙.md` 함정
- 상반신 black-knight (98,7,24) · hero-knight (99,16,24) — 21보다 양옆이 조금 더 보이게. 새 굽기로 black-knight 공격 순서가 유지되는 것을 확인했다
- ⚠️ 첫 세션의 hero-knight·warrior 다시 굽기도 (옛 코드라) 커밋 안 된 SO 조정이 있었다면 덮었을 수 있다 — 확인 불가, 사용자에게 알렸다
