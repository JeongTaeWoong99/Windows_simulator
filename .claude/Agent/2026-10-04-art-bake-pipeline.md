---
date: 2026-10-04
title: 그림 가공 파이프라인 · 슬롯 연출 임시 그림 · 게임UI 2.5 새 방향 (T-120 · T-097)
tags: [client, art, editor, ui, design]
---

# 그림 가공 파이프라인 · 슬롯 연출 임시 그림

## 목적 / 배경
- 슬롯 연출(T-097)을 "캐릭터 오른쪽 제자리 달리기 · 배경 패럴랙스 · 대상이 왼쪽에서 다가옴 · 공격 · 처치 반복"으로 다시 정했다.
- 팩마다 크기·방향·발 위치가 달라, 원본은 `_source/`에 두고 굽기로 같은 규격을 만든다(사용자 요청: "통일성 있게 정리").
- 런타임 루프 플레이어는 **사용자가 가공 결과를 확인한 뒤** 붙인다 — 이번엔 그림·도구·문서까지.

## 변경 내용
- `Assets/Art/` 신설 — `_source/`(팩 4개를 `AssetDatabase.MoveAsset`으로 옮김, GUID 유지) · `characters/`·`backgrounds/`·`targets/` · `VisualCatalog.asset`. 규칙은 `Art 규칙.md`.
- `Editor/art-import/` — 레시피 SO 3종 · `ArtBaker` · `ArtImportPostprocessor` · `ArtValidator` · `ArtImage` · `ArtSpec`.
- `UI/Shared/visual/` — `VisualCatalog`·`CharacterVisual`·`BackgroundVisual`·`TargetVisual` (런타임이라 Editor 밖).
- 게임UI 2.5 재작성 · 게임기획코어 연출 줄 갱신 · T-097 범위 갱신 · T-120 등록 · T-102에 연출 근거 추가.

## 주요 결정 / 근거
- **좌우 반전은 이미지에 굽는다** — 코드 `flipX`를 쓰지 않아 화면 코드가 원본 방향을 모른다.
- **모션 길이는 고정, 속도는 타격 횟수를 줄인다** — 옛 2.5의 "기준 시간 × 실효 속도"를 버렸다. 시간은 진행도에서 매 프레임 계산(상태 없음), 공격은 판정 순간에서 거꾸로 센다 → 마지막 타격 = 판정.
- **짧은 주기는 다가오기를 줄인다**: 다가오기 = min(1초, 주기 − 타격까지). 하한(사용자 제안 1초)은 서버 밸런스라 T-102(진우)에 근거만 붙였다 — 1초 하한이면 타격 준비 시간이 없어 연출상 약 1.5초가 온전.
- 카탈로그는 Addressables 없이 SO 직접 참조. 캐릭터는 개체 번호가 아니라 **TID**로 찾는다.
- 아이템 떠오름·위젯 머리는 이번에 뺐다(사용자 결정) — T-097 "이번에 뺀 것"에 기록.

## 후속 작업 / 주의사항
- **자동 크롭은 쓸 만하지 않았다** — BK 몸통에 발 불꽃이 들어가고 머리가 잘렸다. 두 캐릭터 모두 레시피에 손으로 칸을 적었다(idle 첫 프레임을 픽셀 격자로 그려 보고 정함).
- Unity CLI eval 함정: `using`·제네릭 로컬 함수·`'\'` 문자 리터럴 불가 · internal 타입 접근 불가(→ `ExecuteMenuItem`) · 메인 스레드 5초 타임아웃이 나도 작업은 끝난다(`Temp/` 표식 파일로 확인) · `delayCall` 안 돈다.
- 그림(`_source/`·가공 결과·레시피)은 공개 저장소라 커밋하지 않는다(.gitignore). 보관 방식은 팀 결정 대기.
- 다음: T-097 루프 플레이어 — `WorkStationSlotView`에 RawImage 층 · 진행도 기반 타임라인 · 흰 번쩍임 · Presenter가 TID 조회.

## 추가 — 연속 공격 · 그림 비공개 (같은 날)

- **연속 공격**: 사용자 요청 "연속 공격 소스가 있으면 되도록 사용". 레시피 `attacks[]`(클립/스프라이트 + 타별 `hitFrameOverride`, 0 = 자동) → `CharacterVisual.attacks[]`(`frames`·`hitFrame`). 띠는 `<키>_attack_N`, 남는 띠·예전 `_attack`은 굽기가 지운다.
  - 흑기사 `BK_attack_1~4`(타격 자동 3·4·7·2) · 브링어 `Attack-NoEffect` 하나. `heavy_attack`은 안 씀.
  - 재생 규칙: 판정에서 거꾸로 k칸 앞 = `(n-1-k) mod n`번 공격 → 판정엔 늘 마지막 타. 목업에 반영.
- **그림 비공개**: 저장소가 공개라 원본뿐 아니라 가공 결과도 올리면 안 된다(사용자 지적). push 전이라 커밋 3개를 다시 써서 그림을 뺐다 — `5370d73` → `4e27f51`(규칙 문서·.gitignore만). `/Assets/Art/*` 무시, `Art 규칙.md`만 추적.
  - 보관 방식(저장소 비공개 전환 vs 별도 비공개 그림 저장소)은 팀 결정 대기.

## 추가 — 처치 경계 이어짐 · 인게임 슬롯 무대 (같은 날)

- **처치 경계가 끊겨 보였다**(사용자 지적): 목업이 판정 순간 마무리 공격을 잘랐고, 대상이 즉시 사라졌고, 문서가 "진행도만 보는 무상태"라 유니티에서 배경이 주기마다 처음 자리로 돌아갈 설계였다.
  - 한 주기 = [여운 0.3초 — 마무리 공격 남은 프레임 · 배경 멈춤 · 대상 그 자리에서 0.3초 사라짐] → [달리기] → [공격]. 짧은 주기는 여운부터 줄인다.
  - **땅 거리만 누적**(double) — 나머지는 주기 안 순간에서 매번 계산. 게임UI 2.5 반영.
- **인게임 무대**: `UI/Shared/visual/`에 `SlotStageView`(자식 RawImage 층 uv 스크롤 · 대상 둘(쓰러짐·다가옴) + 흰 실루엣 자식 · 캐릭터) · `SlotStageTimeline`(목업 timeline 이식, 상태 없음) · `SlotStageSettings`(Resources, 목업 값 그대로) · `SlotStagePreview`(서버 없이).
  - 전체 세팅 = `Assets/Resources/SlotStageSettings.asset`(git 추적 — 숫자뿐). `VisualCatalog`의 시간 칸은 뺐다(그림과 함께 git 밖이라).
  - 개인 세팅 = `CharacterVisual.stopGapOffset` — 굽기가 쓰지 않는 칸이라 다시 구워도 남는다.
  - 매 프레임 `SlotStageSettings.Current`를 읽어 플레이 중 인스펙터·CLI·MCP 수정이 바로 보인다.
  - 배선: 프리팹 `WorkStationSlotView`의 `Visible Panel`에 `RectMask2D` + `SlotStageView`(catalog = `Assets/Art/VisualCatalog.asset` — 그림 없는 PC에선 null이라 바탕만). `Bind(slot, name, tid)` · `Tick(progress, remain, cycle)`로 시그니처 변경.
  - 확인: 플레이 모드에서 미리보기 3칸(채집/낚시/채굴 · 주기 4·2.5·1초) 캡처 — 다가옴·공격·번쩍임·사라짐·레벨 색 정상.
- 후속: 빈 슬롯 배경(빈 칸엔 슬롯 뷰가 없다) · 실서버 확인 · 그림 비공개 저장소 [T-121](../../tasks/archive/T-121-그림비공개저장소.md).

## 추가 — 숨 · 개인 배율 · 타격 맞추기 · 판정 하한 일감 (같은 날)

- **bringer 머리 잘림**: 보이는 패널 높이 약 120px인데 배율 2라 머리가 넘쳤다. `CharacterVisual.pixelScaleOverride`(개인 조정, 0 = 전체)를 두고 bringer = 1. 무대(배경·대상) 전체가 같은 배율로 작아진다 — 캐릭터만 줄이면 땅·대상 크기가 어긋나서.
- **달리기 ↔ 공격 전환이 뚝뚝**: 타임라인에 숨(`EPhase.Idle`, `idleSeconds` 0.1) 두 번 — [여운] → [숨] → [달리기] → [숨] → [공격]. 짧은 주기는 여운 → 숨 → 달리기 순으로 줄인다. `Result`에 `RunStart`·`RunEnd`·`AttackStart`.
  - 굽기가 대기 클립을 `_idle` 띠로 구워 `idleFrames`에 넣는다(없으면 달리기 첫 프레임). `DrawIdle`도 대기 자세.
- **BK 2·3타 번쩍임이 늦었다**: 자동 타격 탐지(왼쪽 끝이 가장 크게 뻗는 프레임)가 이펙트 파편에 속았다 — 2타 4→1, 3타 7→6을 레시피 `hitFrameOverride`로 고정.
  - 레시피 `AttackSource`에 `trimStart`·`trimEnd` 추가 — 공격 시간 0.6초 고정이라 자르면 프레임 하나가 길어진다. 타격 번호는 자른 뒤 기준. 맞추는 법은 `Art 규칙.md` "타격 타이밍 맞추기".
- **판정 최소 주기**: 서버엔 속도 하한(`MinWorkSpeed = 1`)뿐 주기 하한은 없다. 서버·클라 공용 엑셀 설정값으로 받을 클라 연동 일감 [T-122](../../tasks/T-122-판정최소주기클라연동.md) — ⏸ T-102(진우 검토).
- 확인: 전부 다시 굽기·검사 통과 · BK 주기 4초 타임라인 샘플(여운 0~0.32 · 숨 · 달리기 0.4~1.42 · 숨 · 공격 1.52~) · 플레이 미리보기 캡처에서 bringer 머리 온전.

## 추가 — 소수 배율 · 이펙트 자리 · 앞 자투리 방어 (2026-10-05)

- **개인 배율 소수**: `pixelScaleOverride`를 float로(전체 `pixelScale`은 정수 유지). 화면 크기·땅 높이를 정수 칸으로 반올림(`Snap`). bringer 1.1 — 이펙트 키(85) + 땅(22) = 107 × 1.1 ≈ 118 < 패널 120.
- **bringer 공격 = 이펙트 든 `Attack.anim`**(전엔 `Attack-NoEffect`). 이펙트가 발에서 왼쪽으로 약 105px 뻗어 160 칸(왼쪽 80)에서 잘렸다 → **칸 160 → 224**(발 x 112). 레시피 크롭 좌표 x +32.
- **이펙트 자리**: `AttackMotion.effectFrames`(캐릭터 칸 규격, 캐릭터 위 같은 진행도) · `hitEffectFrames`(타격 순간 대상 가운데, `hitEffectSeconds`). 레시피 `effectClip/Sprites`(굽기 `_attack_N_fx` 띠) · `hitEffectClip/Sprites`(가공 없이). 지금 채운 캐릭터는 없다.
- **앞 자투리 공격**: 공격은 판정에서 거꾸로 0.6초 칸을 쌓아, 공격 구간이 0.6으로 나눠떨어지지 않으면 맨 앞 칸이 **중간부터** 재생됐다 — 자투리가 짧으면 "공격 끝 한두 프레임 → 같은 공격 처음부터"(공격 하나뿐인 bringer에서 두드러짐).
  - 방어: 자투리 < `minLeadAttack`(0.5) × 공격 시간이면 숨을 늘려 메우고, 그 이상이면 그 공격을 처음부터 자투리 길이에 맞춰 빠르게. 공격은 늘 0프레임에서 시작.
  - `Result.Flash` → `SinceHit`·`HitCombo`(타격 이펙트용). 주기 2.47~4초 샘플에서 모든 공격 시작 프레임 0 확인.
- 확인: 전부 다시 굽기·검사(잘림 경고 없음) · 플레이 캡처에서 bringer 이펙트 손 온전·머리 온전.

## 추가 — 공격 순서 1타부터 · 되감기 방어 (2026-10-05)

- **순서가 4·1·2·3·4처럼 마무리 타로 시작했다**(사용자 지적): 칸 자리뿐 아니라 순서까지 판정에서 거꾸로 셌기 때문. 이제 칸 자리만 거꾸로 잡고 순서는 대상에 닿은 첫 칸부터 1타 → …, 판정 칸은 마무리. 칸이 남으면 마무리 전 타만 1타부터 다시(1·2·3·1·4). 시뮬레이션(주기 2.2~5.5초) 모든 칸 번쩍임 확인.
- **"1·2·3·2·4"에서 번쩍임 없는 2**: 타임라인만 돌리면 재현되지 않았다(모든 칸 번쩍임). 실서버에서 정산 패킷이 기준점을 다시 잡아 진행도가 살짝 **뒤로** 가면, 방금 친 공격이나 판정 경계 너머 마무리 앞 프레임(타격 전 — 번쩍임 없음)이 한 번 더 나오는 것으로 봤다(실서버 재현은 못 함).
  - 방어: `SlotStageView.HoldOnRewind` — 뒤로 간 거리가 min(0.5초, 주기/4) 이내면 지난 순간에 멈춰 기다린다. 캐릭터가 바뀌면 기준을 버린다(정산마다 `Show`가 불려도 같은 캐릭터면 유지).

## 추가 — 공격 순서 다시 · 그림 배선 · 아이콘 구조 (2026-10-05)

- **순서가 여전히 뒤섞였다**(사용자 지적 — 화산도롱뇽 "1 2 / 1 2 4", 흙두꺼비 "1 2 1 3 … 4"): 앞 수정이 "마무리 = 마지막 타 고정 + 앞 칸은 1~3 순환"이라 칸 수에 따라 1·2·4, 1·2·3·1·2·3·…·4가 됐다. 사용자가 원한 것은 **순수하게 앞에서부터 1234 1234, 판정에 닿는 타가 처치 타**.
  - `SlotStageTimeline` 공격부 재작성: 공격 m번을 넣고 m번째 타격이 주기 끝에 닿도록 공격 길이를 조정(`Fit` — 길이가 0.6초에 가장 가까운 m). 자투리 칸이 없어져 `minLeadAttack` 설정 삭제. 여운은 처치 타 = (m−1)%n.
  - 시뮬레이션: 1.2초 → 1 · 2.73초 → 1 2 3 · 4.3초 → 1 2 3 4 1 · 9.3초 → 1234 1234 1234 1 2. 모든 타 번쩍임.
- **배선 정리**: 목록을 `Assets/Art/Resources/VisualCatalog.asset`로 옮겨(GUID 유지 — 무대 프리팹 참조 그대로) 화면이 `VisualCatalog.Current`로 찾는다. 없으면(그림 저장소 없는 PC) null → 예전 자리 표시.
  - 캐릭터: CharacterTable TID(Character.xlsx `CharacterTable`) — 1001 램볼 → black-knight, 1002 치키피 → bringer-of-death, 나머지 → 대체(black-knight, TID 0).
  - 레시피 `characterTids` + `ArtCatalogSync`(전부 다시 굽기 마지막 단계)가 목록을 다시 쓴다.
- **인벤토리 상반신 · 위젯 머리**: `SlotData.Icon` · `ItemIconContent.Icon`(목록 줄) · `WidgetMiniSlotView.Bind(slot, tid)`. 그림이 있으면 흰색 곱 + 비율 유지, 없으면 프리팹 네모 색·글자.
- **자원·장비 아이콘 구조**: `icons/items/item_<TID>.png` · `icons/equips/equip_<TID>.png`, 0번이 대체. 대체 아이콘 2장을 코드로 그려 넣었다(자루 + 물음표 · 검, 32×32, git 밖). 검사가 아이콘 없는 자원 176 · 장비 69를 알린다.
- 확인: 플레이 모드에서 인벤토리 칸 4종(램볼·치키피 상반신 · item_0 · equip_0) · 목록 줄 아이콘 · 위젯 머리 캡처.

## 추가 — 캐릭터 크기 배수 (2026-10-05)

- 굽기 규격(224×96 칸)은 그대로 두고 화면에서만 캐릭터 크기를 바꾸는 `CharacterVisual.characterScale`(개인 조정, 0.5~2, 기본 1). 무대 `PlaceCharacter`가 캐릭터·공격 이펙트 크기에만 곱한다(피벗이 발이라 땅에 붙은 채 커진다). 배경·대상까지 함께 바꾸는 `pixelScaleOverride`와 다르다.
- black-knight 1.3 — 플레이 미리보기에서 머리 온전 확인.
- 작업슬롯 배치 화면 캐릭터 줄(`CharacterStateRowView` — 목록 줄 · 배치된 카드)에도 상반신: `Bind(id, name, tid)` → `VisualCatalog.PortraitOf`. 그림 없으면 프리팹 네모 색. 플레이 캡처 확인.

## 추가 — 새 팩 정리 · 임시 배치 (2026-10-05)

- 사용자가 팩 9개를 `Assets/` 루트에 넣음 → `_source/`로 옮김: biome-plains · biome-american-forest · dark-forest-pack · pixel-art-hills · hero-knight · warrior · minerals-icons · pixel-item-pack · pixel-art-icons-swords.
- **도구 확장**
  - `BackgroundRecipe.LayerGroup.spriteSources` — biome 팩은 층이 한 아틀라스(`Sprites.png`) 안 스프라이트라 텍스처로 못 넣었다.
  - `BackgroundRecipe.skipBottom` — biome 층은 아래 절반 이상이 앞 층 단색이라 슬롯에 단색만 보였다. 아래를 먼저 버린다.
  - `IconRecipe`(+ `ArtBaker.Bake(IconRecipe)`) — 팩 스프라이트를 TID에 짝지어 `item_/equip_<TID>.png`로 굽는다. 아이콘을 손으로 잘라 넣지 않으려고. 전부 다시 굽기에서 `ArtCatalogSync` 직전.
- **임시 배치(느낌 보기)**: 캐릭터 hero-knight(1003~1016, 3연타) · warrior(1017~1030) — 원본 오른쪽 보기. 배경 농사 plains(skip 110) · 채광 hills · 벌목 american-forest(skip 70) · 사냥 dark-forest(skip 30) · 낚시 forest. 아이콘 자원 171 · 장비 69 — 매핑은 `Art 규칙.md` "지금 들어 있는 것".
  - 레시피·목록 값은 eval(SerializedObject)로 넣었다. 그림·레시피는 전부 git 밖.
- 확인: 전부 다시 굽기 ✅(경고 1 — dark-forest L3 `background_4` 양 끝 이음매), 검사 — 아이콘 없는 자원 5종(지도·나침반·열쇠·큐브 2). 플레이 모드 무대 5종 캡처(농사 hero-knight · 채광 warrior · 벌목 BK · 사냥 bringer · 낚시 warrior), 아이콘 접촉 시트 확인.
- 대상은 그대로 cainos 소품(사냥 = 허수아비). 새 팩 소품(dark-forest `Decorations` 등)으로 바꾸는 건 하지 않았다.

## 추가 — plains 땅 띠 (2026-10-05)

- 사용자: "plains은 뒷배경만 있고 땅이 없다" → `BackgroundRecipe.groundTiles`·`groundDownscale` 추가. 타일을 가로로 이어 무대 높이 그림의 바닥에 붙여 맨 앞 층(속도 1.0)으로 굽는다.
- 땅은 같은 팩(biome-plains)의 `TileGround2`(풀+흙 가운데 타일, 축소 1) — 다른 팩보다 그림체가 맞다. 앞 실루엣 층 속도 1.0 → 0.8, `skipBottom` 110 → 70(땅이 나무 줄을 덮어서), `groundHeight` 12.
- 굽기 실패 발견: 플레이 모드를 한 번 돌린 뒤 biome 팩의 SpriteAtlas가 묶여 `sprite.texture`가 아틀라스(경로 없음)를 가리킴 → `FromTexture`에서 `Path is empty`. `FromSprite`가 스프라이트 에셋 경로의 원본 텍스처를 열도록 수정.
- 확인: 전부 다시 굽기 ✅(plains 층 6 · 높이 101), 플레이 모드 농사 무대 3종 캡처.
- 이어서 american-forest · dark-forest에도 땅 띠: american-forest = 같은 팩 `TileGround2`(축소 1 · 땅 12 · skip 70 → 40), dark-forest = 같은 팩 `Tiles/Earth` 64×64(축소 2 · 땅 29). 둘 다 앞 실루엣 층 속도 0.8. 플레이 모드 캡처로 확인.
