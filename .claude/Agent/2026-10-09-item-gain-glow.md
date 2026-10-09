---
date: 2026-10-09
title: 아이템 획득 연출 — 아이콘 뒤 등급 빛
tags: [client, ui, dotween, fx]
---

# 아이템 획득 연출 — 아이콘 뒤 등급 빛

## 목적 / 배경
- 작업슬롯(큰 창)·위젯 미니 슬롯에서 수확 아이콘만 덩그러니 떠올라, 무엇을 얻었는지 눈에 잘 띄지 않았다. 보상 공개 연출의 빛을 아이콘 뒤에 깔았다.
- 바탕화면 목업(`획득연출_목업.html`)에서 사용자가 **등급 3단계**(일반·고급 / 희귀·영웅 / 전설·신화)를 골랐다. 위 두 단계가 비슷해 보인다고 해서, 전설·신화에 다른 종류의 빛을 얹은 F(빛 기둥)·G(겹광선)를 다시 만들었다. 사용자는 "이렇게 넣어보자"라고만 해서 **둘 다 넣고 인스펙터에서 고르게** 했다(기본 F).

## 변경 내용
- `UI/Shared/ItemGainEffectView.cs` — 아이콘 Image 풀을 **묶음(`ItemGainFx`) 풀**로 바꿨다. 묶음이 떠오르고 가로로 따라간다. `Play`가 그림 대신 `Gain`(그림 + 등급)을 받는다. `ReadGainIcons` → `ReadGains`(등급은 `GameDataLoader.GetItemRarity`). Sequence 만들기는 async 밖 `BuildSequence`로 옮겼다(CS4014 경고 해소). 인스펙터에 `glow`·`popSettleDuration`을 추가했다.
- `UI/Shared/ItemGainFx.cs` (새 파일) — 아이콘 한 개 묶음. 자식 순서로 앞뒤를 정한다(빛 기둥·번짐·광선·겹광선·고리 → 아이콘 → 섬광·별 조각·반짝이). 원본은 코드로 만든다(`CreateTemplate`).
- `UI/Shared/ItemGainGlowSettings.cs` (새 파일) — 조정값. 크기는 **아이콘 대비 배율**이다. `TopStyle { Pillar, DoubleRays }`, 등급 → 단계 `TierOf`.
- `RevealSprites` → `UI/Shared/visual/FxSprites.cs`로 옮기고 이름을 바꿨다(`git mv`라 GUID는 그대로다). `gain_pillar`(세로 빛 기둥)·`gain_ring`(고리) 모양을 추가했다.
- 호출부: `WorkStationListPresenter`·`WorkStationSlotView.PlayGain`·`WidgetPresenter`·`WidgetMiniSlotView.MarkHarvested`의 타입만 바꿨다.
- 문서: `Shared 규칙.md`·`System 규칙.md`·`Art 규칙.md`(그림 저장소).

## 주요 결정 / 근거
- **조정값을 아이콘 대비 배율로 둔다.** 큰 창(36px)과 위젯(18px)이 같은 값으로 같은 모양이 된다. 그래서 프리팹 값을 따로 맞추지 않았다.
- **묶음 단위로 움직인다.** 빛을 아이콘과 따로 띄우면 떠오름·따라가기를 빛마다 다시 계산해야 한다. 묶음 하나가 움직이고, 빛은 묶음 안에서 제자리 움직임만 한다.
- **`FxSprites`를 Shared로 옮겼다.** 공용(Shared)이 System 폴더의 클래스를 보면 소유가 거꾸로 된다(`Shared 규칙.md`).
- 빛 기둥·고리 그림은 굽지 않았다 — 코드 모양으로 돈다. 그림 저장소에 `fx/gain/gain_pillar.png`·`gain_ring.png`를 두면 그쪽을 쓴다.

## 후속 작업 / 주의사항
- 검증: 플레이 모드에서 Root Canvas 아래에 테스트 칸 3개(36px F · 18px · 36px G — 큰 창은 이후 27px로 줄였다)를 만들어 고급·영웅·신화를 `timeScale 0.03`으로 캡처했다. 단계별 빛이 의도대로 나왔다. 실제 수확(로그인 후)으로는 보지 않았다.
- ⚠️ 테스트 칸은 처음에 화면에 안 보였다 — Root Canvas 안의 하위 캔버스(로그인·인벤토리)가 위에 그려졌다. 테스트 칸에 `overrideSorting`을 주고서야 보였다. 실제 칸은 각 캔버스 안이라 해당 없음.
- 풀 원본은 처음 재생 때 `glow.SparkCount`로 별 조각 수를 정한다. 플레이 중 이 값을 바꿔도 이미 만든 묶음에는 반영되지 않는다.

## 업데이트 (2026-10-09) — 그림만 쓴다 · 더 진하게

### 변경
- **코드로 그리던 대체 모양을 모두 지웠다.** `FxSprites`는 이제 그림 이름표다 — 연출별 그림 이름을 속성으로 모으고, 목록(`VisualCatalog.FxOf`)에서 찾는다. 결과는 캐시하지 않는다. 그림이 없으면 null이고, 쓰는 쪽은 그 Image를 켜지 않는다(빈 Image는 흰 네모로 그려진다).
  - `RewardRevealFx` — 테두리 띠·원뿔이 둘 다 있을 때만 테두리를 만든다. 띠 없이 `Mask`를 만들면 칸 전체가 마스크가 돼 원뿔이 칸을 덮는다. 번짐·빛 줄은 그림이 있을 때만 켠다.
  - `RewardBurstFx` · `ItemGainFx` — 켤 때 `image.enabled = image.sprite != null`.
- **임시 그림 생성기** `Assets/Art/_source/fx-placeholder/make_fx.py`(그림 저장소, PIL). `fx/reveal/` 6장과 새 `fx/gain/` 5장(`gain_glow`·`gain_rays`·`gain_sparkle`·`gain_pillar` 1:2·`gain_ring`)을 굽는다. reveal은 예전 PNG와 알파 차이가 최대 1로 같은 그림이다 → 다시 구운 것으로 바꿨다.
- 획득 연출은 `gain_*`만 쓴다. 공개 연출 그림과 나눠, 한쪽을 바꿔도 다른 쪽이 바뀌지 않는다.
- 더 진하게: `gain_glow`는 속이 진하고 천천히 꺼진다. 빛 색을 흰색 쪽으로 20% 당겼다(`GlowWhiten` — 파랑·보라가 어두운 바탕에 묻혀서). 광선 알파를 1로, 번짐 페이드 시작을 0.6으로 늦췄다. 기본 배율도 키웠다(번짐 1.9→2.6 · 광선 2.3→3 · 꼭대기 번짐 2.4→3.2 · 꼭대기 광선 3.4→4.2 · 기둥 (1.1, 3.2)→(1.4, 3.6) · 별 0.45→0.6 등).

### 결정
- **사용자 지시(2026-10-09): 이펙트는 이미지로 넣고 코드로 그리지 않는다.** 교체하기 쉬워야 하고, 임시 그림을 그대로 쓸 수도 있다. 앞 결정("그림 저장소가 없어도 같은 모양으로 돈다")을 뒤집었다 — 그림 저장소가 없는 PC에서는 효과만 빠지고 움직임은 돈다.
- 모양을 다듬을 때는 PNG가 아니라 스크립트를 고친다. 원본은 스크립트다.

### 주의
- 프리팹 두 개에는 `glow` 값이 아직 직렬화되지 않아 코드 기본값을 쓴다. 인스펙터에서 한 번 고쳐 저장하면 그 뒤로는 기본값을 바꿔도 따라오지 않는다.
- 검증: 같은 테스트 칸으로 플레이 모드에서 캡처했다. 확실히 진해졌다. 가챠 공개 연출은 다시 돌려 보지 않았다(그림은 그대로이고 바뀐 것은 빈 그림 처리뿐).
- 사용자 확인 뒤 큰 창 슬롯 아이콘을 0.75배로 줄였다(`WorkStationSlotView.prefab` `iconSize` 36 → 27). 빛은 아이콘 대비 배율이라 함께 줄어든다.
