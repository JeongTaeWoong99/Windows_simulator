# Art 폴더 규칙

> 최종 업데이트: 2026-10-05 (칸 224×96 · 이펙트 자리 · 소수 배율 · 아이콘 폴더 · 목록 자동 등록 · 캐릭터 크기 배수 · 아이콘 레시피 · 배경 스프라이트 원본 · `skipBottom` · 땅 띠 `groundTiles` · 새 팩 임시 배치 · T-097) · 2026-10-04 (연속 공격 · 시간 값을 `SlotStageSettings`로 · 대기 띠 · 개인 배율 · 타격 맞추기 · T-097) · 대상: `Assets/Art/`

**그림은 받은 그대로 쓰지 않는다.** 팩마다 크기·방향·발 위치·임포트 설정이 제각각이라,
원본은 `_source/`에 그대로 두고 **레시피 → 굽기**로 같은 규격의 결과물을 만든다.
화면 코드는 결과물(`*.asset`)만 본다 — 원본 팩을 바꿔 끼워도 화면 코드는 그대로다.

가공 도구의 코드는 [`Scripts_Client/Editor/art-import/`](<../Scripts_Client/Editor/art-import/art-import 규칙.md>),
연출 규칙(언제 무엇을 그리나)은 [게임UI](../../GameDesign/design/ui/README.md) 2.5에 있다.

## 폴더

```
Assets/Art/
├─ _source/                  원본 팩 — 받은 그대로. 손대지 않는다 (임포트 설정도 팩 것 그대로)
├─ characters/<키>/          캐릭터 — 레시피 + 굽기 결과
├─ backgrounds/<키>/         배경 — 레시피 + 굽기 결과
├─ targets/<키>/             대상 — 레시피 + 굽기 결과 (키 = 산업 이름)
├─ icons/
│  ├─ items/                 자원 아이콘 — item_<ItemTID>.png · item_0.png = 대체 (아이콘 없는 자원)
│  └─ equips/                장비 아이콘 — equip_<EquipTID>.png · equip_0.png = 대체
└─ Resources/VisualCatalog.asset   목록 — "이 TID·이 산업은 어떤 그림으로". 화면이 Resources로 찾는다
```

- **⚠️ 이 문서 말고는 git에 올리지 않는다**(`.gitignore`) — 공개 저장소이고, 원본 팩은 재배포 금지가 많다. 보관 방식은 팀 결정 대기.
- **폴더 이름이 곧 키다** — 결과물 이름이 전부 `<키>_…`로 붙는다. 폴더는 영문 소문자 kebab-case.
- 한 폴더 = 레시피 1개(`<키>_recipe.asset`) + 결과물. **결과물은 손으로 고치지 않는다** — 다시 구우면 덮인다.

## 규격

| 종류 | 결과물 | 규격 |
|---|---|---|
| **캐릭터** | `<키>_idle.png` · `<키>_run.png` · `<키>_attack_1.png` … (가로 띠 — 공격은 연속 공격 타마다 하나) | 한 칸 **224×96** · 발이 **(112, 0)** — 이펙트 든 공격이 발에서 왼쪽으로 100px 넘게 뻗는다 · **왼쪽을 본다**(오른쪽을 보는 원본은 굽기가 뒤집는다) · 칸 이름 `<키>_run_0` … |
| | `<키>_portrait.png` | **64×64** · 몸통 크롭 — 인벤토리용 |
| | `<키>_head.png` | **32×32** · 머리 크롭 — 위젯용 |
| | `<키>.asset` (`CharacterVisual`) | 프레임 · 타마다 타격 프레임 · 크롭 |
| **배경** | `<키>_L0.png` … (먼 층부터) | 높이 **128** · 가로 반복(Repeat) · 양 끝이 이어진다 |
| | `<키>.asset` (`BackgroundVisual`) | 층별 속도 비율 · 땅 높이. **땅 층 = 속도 1.0**(대상과 같은 속도) |
| **대상** | `<키>.png` · `<키>_flash.png` | 투명 테두리 잘라 냄 · 흰 실루엣(맞을 때 번쩍임) |
| | `<키>.asset` (`TargetVisual`) | 레벨별 색 — 1레벨 흰색(원래 색) · 노랑 · 하늘 · 보라 · 빨강 |

공통: PPU 100 · 점 필터 · 무압축 · 밉맵 없음. **임포트 설정은 도구가 고정한다** — 인스펙터에서 바꿔도 다음 임포트에 되돌아온다.

**모션 길이는 그림이 정하지 않는다.** 프레임 수가 달라도 달리기 한 바퀴 0.8초 · 공격 한 번 0.6초 · 다가오기 1초로
재생한다. 시간·거리 값은 그림과 함께 git 밖에 있으면 안 돼서 `Assets/Resources/SlotStageSettings.asset`(전체 세팅)에 있고,
캐릭터마다 다른 값은 `<키>.asset`의 **개인 조정** 칸에 있다 — 굽기가 덮지 않는다.

| 개인 조정 | 언제 |
|---|---|
| `stopGapOffset` | 대상과 너무 붙거나 떨어진다 — 무기가 길면 +, 맨손이면 − (아트 픽셀) |
| `characterScale` | **캐릭터만** 작거나 크다 — 0.5~2배(1 = 굽기 규격 그대로). 발 위치 기준으로 그림만 키워서 무대·대상 크기는 그대로다. 키우면 공격이 대상에 더 깊이 들어가 보일 수 있으니 `stopGapOffset`과 함께 본다 |
| `pixelScaleOverride` | **키가 커서 머리가 잘린다** — 낮춘다(소수 가능, 예 1.1 · 0 = 전체 세팅 `pixelScale`). 무대가 통째로 작아진다. 정수가 아니면 점 크기가 한 칸씩 들쭉날쭉할 수 있다 |

**이펙트는 공격마다 두 자리가 있다** (레시피 공격 칸 → 굽기가 `<키>.asset`의 공격 칸에 넣는다). 비우면 그리지 않는다.

| 자리 | 레시피 | 그리는 곳 |
|---|---|---|
| 공격 이펙트 | `effectClip` · `effectSprites` — 모션과 이펙트가 **따로 든** 팩용. 캐릭터와 같은 크기·원점, 띠 `<키>_attack_N_fx` | 캐릭터 위에 겹쳐 같은 진행도로 |
| 타격 이펙트 | `hitEffectClip` · `hitEffectSprites` — 가공 없이 그대로 | 타격 순간 대상 가운데에서 한 번(`hitEffectSeconds` 0.3초) |

모션에 이펙트가 **이미 그려진** 팩(bringer-of-death의 `Attack`)은 그 클립을 공격으로 그대로 쓴다 — `-NoEffect` 클립을 고르지 않는다.

**달리기 ↔ 공격 사이에는 대기 자세 한순간(`idleSeconds` 0.1초)이 끼어든다** — 바로 맞붙으면 뚝뚝 끊겨 보인다.
대기 그림은 레시피의 대기 클립(`<키>_idle`)이고, 없으면 달리기 첫 프레임으로 대신한다.

### 타격(번쩍임) 타이밍 맞추기

공격 한 번은 프레임 수와 무관하게 **0.6초**(`attackSeconds`)다.

- 프레임 하나 = 0.6 ÷ 프레임 수 (9프레임 ≈ 0.067초 · 11프레임 ≈ 0.055초)
- 타격 순간 = 타격 프레임 ÷ 프레임 수 × 0.6 — 이때부터 대상이 0.08초(`flashSeconds`) 하얗다. 마무리 공격의 타격 = 판정
- 타격 프레임은 굽기가 자동으로 고르는데(왼쪽 끝이 가장 크게 뻗는 프레임) **이펙트 파편에 속기도 한다**

번쩍임이 늦거나 이르면 레시피(`<키>_recipe`)의 그 타를 고치고 다시 굽는다.

| 증상 | 고칠 칸 | 효과 |
|---|---|---|
| 번쩍임만 그림과 어긋난다 | `hitFrameOverride` — 그림에서 칼이 닿는 프레임 번호(0부터) | 번쩍임만 옮긴다. 동작 속도는 그대로 |
| 동작이 늘어지거나 쓸데없는 준비·마무리가 길다 | `trimStart` · `trimEnd` — 앞뒤에서 잘라 낼 프레임 수 | 남은 프레임이 0.6초를 나눠 가져 **프레임 하나가 길어진다**. 타격 번호는 잘라 낸 뒤 기준 |

예: 11프레임 중 앞 2장을 자르면 9프레임 × 0.067초가 되고, 원래 7번 프레임은 5번이 된다.

## 새 그림 넣기

1. 팩을 `_source/<팩-이름>/`에 넣는다.
2. 결과 폴더(`characters/<키>/` 등)를 만들고 **Create > DesktopWindowControl > Art Recipe**로 레시피를 만든다
   — 이름은 `<키>_recipe`.
3. 레시피에 원본을 끼운다.
   - 캐릭터: **`characterTids`** (이 그림을 쓸 CharacterTable TID들 — `0`을 넣으면 그림 없는 캐릭터의 대체 그림) · 달리기·공격·기본(idle) 클립 또는 스프라이트 — **팩에 연속 공격(1·2·3타…)이 있으면 순서대로 전부 넣는다**(대상마다 1타부터 순서대로 친다. 하나뿐이면 그것을 반복), 원본이 오른쪽을 보는지, 크롭 칸(비우면 자동).
   - 배경: 층 묶음마다 원본 텍스처들(**아틀라스 안의 스프라이트면 `spriteSources`**)과 속도 비율, 땅 높이.
     `cropBottom`(아래에서 남길 높이) · **`skipBottom`(그 전에 아래에서 버릴 높이 — 아래가 통짜 단색이라 슬롯에 그것만 보일 때)** · `downscale`은 원본 픽셀 기준.
     **원본 배경에 땅이 없으면 `groundTiles`** — 땅 타일(가로로 이어지는 가운데 타일)을 넣으면 바닥에 이어 붙여 맨 앞(속도 1.0) 층으로 깐다.
     축소는 `groundDownscale`로 따로(타일은 캐릭터와 같은 밀도인 1이 자연스럽다). `groundHeight`는 그 타일의 풀 윗면에 맞춘다.
   - 대상: 원본 스프라이트 한 장, 축소 배율.
4. 레시피 우클릭 **이 레시피 굽기** (전부는 메뉴 `Window/DesktopWindowControl/아트/전부 다시 굽기`).
5. 메뉴 **아트/검사**로 확인한다.
6. **목록은 손으로 잇지 않는다** — 전부 다시 굽기의 마지막 단계가 `Resources/VisualCatalog`를 다시 쓴다.
   캐릭터는 레시피의 `characterTids`, 아이콘은 파일 이름의 TID로. 배경·대상(산업마다 하나)만 목록에서 직접 고른다.

### 아이콘 넣기 (자원·장비)

두 길 중 하나 — 어느 쪽이든 결과는 `icons/items/item_<ItemTID>.png` · `icons/equips/equip_<EquipTID>.png`
(TID는 `Item.xlsx`의 `ItemTable.*` · `Equip.xlsx`의 `EquipTable`)이고, **전부 다시 굽기**하면 목록에 올라간다.

- **팩 스프라이트를 고른다** — `icons/icon_recipe.asset`(아이콘 레시피)에 `TID ↔ 원본 스프라이트`를 적는다.
  굽기가 투명 테두리를 잘라 위 이름으로 쓴다. 지금 임시 배치는 전부 이 레시피다.
- **파일을 직접 넣는다** — 다 그린 그림이면 위 이름으로 바로. 단 레시피에 같은 TID가 있으면 굽기가 덮는다.

임포트 설정(점 필터 · 한 장 · 가운데 기준)은 도구가 고정한다. 크기는 자유지만 정사각형을 권한다(칸이 비율을 지켜 맞춘다).

### 어디에 그려지나

| 그림 | 찾는 값 | 화면 |
|---|---|---|
| 캐릭터 연출(`<키>.asset`) | CharacterTable TID → 레시피 `characterTids` | 큰 창 작업슬롯 무대 |
| 캐릭터 상반신(`<키>_portrait`) | 〃 | 인벤토리 캐릭터 칸 · 가챠 결과 · 목록 줄 아이콘(경매·우편 등) · 작업슬롯 배치 화면 캐릭터 줄(`CharacterStateRowView`) |
| 캐릭터 머리(`<키>_head`) | 〃 | 위젯 미니 슬롯 |
| 자원 아이콘 | ItemTable TID → `item_<TID>` | 인벤토리 자원 칸 · 가챠 결과 · 목록 줄 아이콘 |
| 장비 아이콘 | EquipTable TID → `equip_<TID>` | 인벤토리 장비 칸 · 가챠 결과 · 목록 줄 아이콘 |
| 배경 · 대상 | 산업(`EIndustryType`) | 작업슬롯 무대 |

그림 저장소를 받지 않은 PC는 목록이 없어(`VisualCatalog.Current == null`) 예전 자리 표시(색 네모 · 이름 첫 글자)를 그린다.

> **크롭은 자동값을 믿지 말고 눈으로 본다.** 자동 크롭은 무기·이펙트를 몸으로 잡기 쉽다 —
> 결과가 어긋나면 레시피의 `portraitRect`·`headRect`(기본 idle 첫 프레임 기준 픽셀)를 직접 적는다.

## 빠진 그림

| 없는 것 | 대신 |
|---|---|
| 캐릭터 그림 | 목록의 **대체 캐릭터** — 레시피 `characterTids`에 `0`을 넣은 그림 (지금 black-knight) |
| 자원 아이콘 | `item_0` |
| 장비 아이콘 | `equip_0` |
| 산업 배경 | 기본 배경 |
| 빈 슬롯 전용 배경 | 기본 배경을 멈춘 채로 |
| 산업 대상 | 대상 없이 달리기만 |

빠진 목록은 **아트/검사**가 ℹ️로 알려 준다.

## 지금 들어 있는 것 (전부 임시)

| 키 | 원본 |
|---|---|
| `characters/black-knight` (1001 · 대체 캐릭터) | `_source/black-knight` |
| `characters/bringer-of-death` (1002) | `_source/bringer-of-death` |
| `characters/hero-knight` (1003~1016) | `_source/hero-knight` — 3연타 |
| `characters/warrior` (1017~1030) | `_source/warrior` — 이펙트 포함 시트 |
| `backgrounds/forest` (기본 배경 · 낚시) | `_source/free-pixel-art-forest` |
| `backgrounds/plains` (농사) | `_source/biome-plains` — 아틀라스 스프라이트 `Background5~1` + 땅 띠 `TileGround2`(같은 팩 타일) |
| `backgrounds/hills` (채광) | `_source/pixel-art-hills` — 미리보기 한 장 |
| `backgrounds/american-forest` (벌목) | `_source/biome-american-forest` — 〃 + 땅 띠 `TileGround2`(같은 팩) |
| `backgrounds/dark-forest` (사냥) | `_source/dark-forest-pack` + 땅 띠 `Tiles/Earth`(같은 팩, 축소 2) — ⚠️ L3 이음매가 어긋난다(검사 경고) |
| `targets/{mining·logging·farming·hunting·fishing}` | `_source/cainos-village-props` — 돌 · 나무 · 허수아비 · 훈련 인형 · 우물 |
| `icons/icon_recipe` — 자원 171 · 장비 69 | 채광 = `minerals-icons` 1~30 · 낚시 = 물약 · 농사 = 항아리 · 벌목 = 통 · 사냥 = 하트 · 구슬 = 동전 · 상자 = 상자(나무 빨강 · 은 파랑 · 황금 금색) — `pixel-item-pack`(Light Outline, 등급별 색) · 무기 = `pixel-art-icons-swords` 순환 · 보석 = `minerals-icons` 31~48 순환 · 장신구 = 하트. 지도·나침반·열쇠·큐브 5종은 `item_0` |

캐릭터 TID 나눔(1003~1016 · 1017~1030)은 느낌 보기용 임의 배정이다 — 진짜 짝은 캐릭터 그림이 정해질 때 레시피 `characterTids`에서 고친다.

> ⚠️ 원본 팩의 라이선스는 팩 폴더의 안내를 따른다. 배포 전에 다시 확인한다.
