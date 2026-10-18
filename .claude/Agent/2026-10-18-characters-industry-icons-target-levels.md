---
date: 2026-10-18
title: 새 팩으로 캐릭터 27종 · 산업 아이콘 · 산업 레벨별 대상 그림을 넣는다
tags: [client, art, excel]
---

# 캐릭터 27종 · 산업 아이콘 · 레벨별 대상

## 목적 / 배경
- 사용자가 추가한 캐릭터·아이콘·소품 팩으로 캐릭터, 산업 아이콘(T-054), 산업 레벨 1~5마다 다른 대상을 넣는다.

## 변경 내용
- 코드
  - `TargetRecipe.levels`(레벨마다 원본 + `downscale`) → `ArtBaker`가 `<키>_L<n>.png`·`_flash`를 굽고 `TargetVisual.levels`에 쓴다.
    `TargetVisual.GetSprite/GetFlash(level)` — 레벨 그림이 있으면 색(`levelTints`)을 곱하지 않는다. `SlotStageView`가 레벨로 그림을 고른다.
  - 산업 아이콘 — `ArtSpec.IndustryIconsRoot`(`icons/industries/industry_<EIndustryType 값>`) · `IconRecipe.industries` · `ArtCatalogSync`(대체 칸 없음) ·
    `VisualCatalog.IndustryIconOf` (없으면 `fallbackItemIcon` = 빈 자루) · `WorkStationSelectPresenter.industryIcons`(씬의 `Icon (임시)` 5개 연결).
  - `ArtImportPostprocessor` 최대 텍스처 4096 → 8192 — forest-slime 공격 19칸 띠(4256px)가 줄어들어 칸이 215.6px이 됐다(아트/검사 ❌).
  - `CharacterRecipe.idleSprites` — 대기 원본을 스프라이트 배열로(클립 없는 팩).
- 그림 저장소: 캐릭터 레시피 23개 새로 + 기존 4개 TID 재배정, 대상 5개(`excavation` 새 키 · 목록의 발굴이 `fishing` → `excavation`), 아이콘 레시피 산업 5개.
- 문서: `Art 규칙.md`(레벨 그림 · 산업 아이콘 · 캐릭터 ↔ TID 표) · `art-import 규칙.md` · T-054 · T-148.

## 주요 결정 / 근거
- **CharacterTable 30행·등급·적성 구조는 그대로** 두고 그림·이름만 슬롯에 맞췄다 — 뽑기·`Constants`(시작 캐릭터 1001)가 TID를 가리킨다.
  등급 = 공격 모션 수(4연타 → 신화, 3연타 → 전설·영웅, 2연타 → 희귀, 1개 → 고급·일반) + 외형. 1008·1010·1014는 맞는 그림이 없어 대체(medieval-warrior).
- 대체 캐릭터를 black-knight → medieval-warrior(평범한 전사 · 시작 캐릭터 1001)로.
- 새 팩은 전부 오른쪽을 본다 → `sourceFacesRight`. 원본이 큰 medieval-warrior·evil-wizard-2·evil-wizard는 축소 2. enemy-galore 6종은 너무 작아 결과 SO `characterScale` 2(사람이 다시 다듬어도 된다).
- 산업 이름은 바꾸지 않았다 — 같은 화풍(cainos-rpg-icons)의 삽·곡괭이·도끼·활이 발굴·채굴·벌목·사냥에 그대로 맞는다. 농사만 농기구가 없어 순무(free-pixel-food, 외곽선 없는 화풍).
- 대상은 같은 크기 비율을 유지하려고 전부 축소 1. 채굴 3~5레벨(광물 아이콘 32px)은 바위보다 크지 않다 — 더 커져야 하면 `targets/mining/mining.asset`의 `scale`로.

## 후속 작업 / 주의사항
- 엑셀 — `Character.xlsx` `Name`(KR)과 `Loc_Character.xlsx`(KR·EN) 26행을 그림에 맞춰 바꿨다(1008·1010·1014·1029는 그대로). KR을 Loc 파일에도 같이 써 두어 `🔄 EN` 상태가 생기지 않는다. 처음엔 자동 모드가 엑셀 쓰기를 막아 사용자 승인 뒤 진행했다.
- 씬(`DesktopWindow_Control.unity`)의 `industryIcons` 연결은 **저장되지 않았다** — 씬에 이미 저장 안 된 변경이 있어 함께 저장하지 않았다.
- `SlotView.SetAptitudes`의 적성 칸 산업 아이콘(TODO)은 남았다.
- `targets/fishing`은 목록에서 빠졌고 지워도 된다.

## 추가 — 사용자 피드백 반영 (같은 날)
- **크기 템플릿 소·중·대** — `CharacterSizeTemplate`(Editor) · 에셋 `Art/characters/_templates/size-small·medium·large` · 레시피 `sizeTemplate`(27개 지정, 비면 몸 키 24 이하 → 소, 나머지 → 중, 대는 손으로).
  굽기가 SO를 처음 만들 때·템플릿이 바뀔 때 `characterScale`(목표 키 30/52/66 ÷ 몸 키, 너비 상한 40/70/90) · `pixelScaleOverride`(1.4/1.4/1.2) · `stopGapOffset`(타격 프레임 사거리 평균 × 배율 × 0.55/0.5 − `stopGap`)을 채운다. `CharacterVisual.sizeTemplate`에 이름을 적어 한 번만.
  목표 값은 사람이 다듬은 warrior·hero-knight·black-knight(몸 키 × 배율 ≈ 52)·bringer-of-death(≈ 65)에서 역산했다. 이 넷과 mushroom(작업 중 사용자가 조정)은 값을 그대로 두고 이름만 적었다.
  `characterScale` 범위 0.5~2 → 0.25~4 (골렘 26px → 2.55배).
- **작은 캐릭터 크롭은 몸 전체** — 템플릿 `cropWholeBody`. 박쥐가 얼굴이 없다시피 해 가까이 자르면 못 알아봤다(사용자).
- **대상 레벨별 배율** — `TargetVisual.Level.scale`(× 전체 `scale`, 0은 1) · `GetScale(level)` · 굽기는 0인 칸만 1로.
- 다시 굽기 경고(칸 밖 74건)는 전과 같다.

## 추가 2 — 크기 등급 표시 · SO 드롭다운 (같은 날)
- 등급을 SO에 enum으로 — `ECharacterSize`(None·Small·Medium·Large) · `CharacterVisual.sizeClass`(문자열 `sizeTemplate` 대체) · `CharacterSizeTemplate.sizeClass`.
  굽기는 SO 등급이 None일 때만 템플릿 값을 채운다. SO 등급이 있으면 크롭 방식도 그 등급 템플릿을 따른다.
- `CharacterVisualEditor` — SO 인스펙터 맨 아래 드롭다운. 고르면 `ArtBaker.ComputeSize`(구운 그림으로 계산)로 세 칸을 채우고, 값이 템플릿과 다르면 "커스텀 수치 적용 중" + [템플릿 수치로 되돌리기].
  템플릿 [전부 다시 적용]은 다시 굽지 않고 같은 계산으로 그 등급 SO만 덮는다.
- 화면 표시 — `CharacterSizeLabel`(VisualCatalog의 그림 등급) · `SlotData.SizeBadge` → `SlotView` 'Size Badge'(레벨 배지 복제, 바로 위 · 인벤토리·창고 — 가챠 결과는 이상해 보여 뺐다(사용자)) · 캐릭터 툴팁 '크기' 줄(인벤토리·경매 2곳) · 배치 목록 이름 뒤.
  문구 `Loc_UI.xlsx` `UI.Character.Size*` 7행(크기 · 소형/중형/대형 · 소/중/대 — EN Size · Small/Medium/Large · S/M/L).
- 사용자가 다듬었던 5종(black-knight·bringer-of-death·hero-knight·warrior·mushroom)도 템플릿 값으로 바꿨다(사용자 요청). 이전 → 이후 차이는 멈춤 거리 2~5px · warrior 화면 1.3 → 1.4 정도.
- 그림 없는 1008·1010·1014는 대체 그림(medieval-warrior) 등급 = 중형으로 표시된다.

## 추가 3 — 몸 영역 `bodyRect` (같은 날)
- 그림자 마법사(evil-wizard-2, 1019)가 중형인데 다른 중형의 절반 크기 · 상반신·머리 크롭이 지팡이 위 보라 연기에 쏠렸다(사용자).
  원인: 몸 = 대기 첫 프레임의 불투명 테두리라 연기·지팡이까지 47px로 쟀다(마법사 몸은 26px).
- 레시피 `CharacterRecipe.bodyRect`(결과 칸 좌표, 너비 0이면 자동) — `ArtBaker.BodyOf`. 템플릿 배율·자동 등급·자동 상반신·머리가 모두 이 영역을 쓴다.
  SO 드롭다운 계산(`ComputeSize(visual)`)은 같은 폴더 `<키>_recipe`의 `bodyRect`를 읽는다.
- evil-wizard-2: `bodyRect (106,0,14,26)` → 배율 1.1 → 2 · 멈춤 0 → +27 · 상반신·머리 (104,10~11,16).
- 나머지 26종은 배율 기준 시트로 눈으로 비교 — 테두리가 몸을 크게 벗어난 것은 이 하나였다.

## 추가 4 — 머리·상반신 크롭 전수 점검 (같은 날)
- 사용자가 결과를 하나하나 보고 13종을 짚었다 — 박쥐·게 머리가 칸에 작게 뜸 · evil-wizard-2 연기 점 · evil-wizard-3 지팡이 쪽 쏠림 · field-rat 머리 잘림(머리가 앞 끝) · goblin·golem·medieval-king-2·medieval-warrior 머리 반쯤 잘림.
  **27종을 굽고 결과 PNG를 나란히 놓고 보지 않은 채 보고했다** — 점검표를 `Art 규칙.md`에 적고 메모리(character-crop-criteria)에도 남겼다.
- 코드: 머리도 `Upscale`(32 캔버스 `Fit` 삭제 — 작은 크롭이 여백 속에 작게 떴다) · 자동 머리 가로 = 위쪽 (크기/2)줄 평균 x(발 x 대신) · 작은 캐릭터 머리 = 몸 긴 변의 80%.
  → cave-bat·crab·goblin·medieval-warrior는 자동으로 맞았다.
- 레시피에 적은 것 9종: evil-wizard(머리·상반신) · evil-wizard-2 · evil-wizard-3(머리·상반신) · field-rat(머리·상반신) · horn-rat · golem · medieval-king-2 · medieval-warrior-2 · skeleton — 값은 `Art 규칙.md` 표.
- 미리보기: Python으로 굽기 크롭을 흉내 내 값을 먼저 정하고 구웠다. 구운 결과와 픽셀 단위로 같음을 확인(skeleton diff 0).
- goblin 레시피 `headRect.x 30`(너비 0이라 무시되던 값, 사용자 실험)은 0으로 되돌렸다 — 자동이 맞게 잡는다.

## 추가 5 — 위젯 머리 어깨선 · 가챠 배지 제거 · 커밋 정리 (같은 날)
- 위젯 머리가 몸 전체·허리까지 담겨 다른 캐릭터와 달라 보였다(사용자) — hero-knight-luizmelo `(104,28,20)` · evil-wizard-2 `(106,17,10)`(상반신도 연기 점 빼고 `(106,10,16)`) · forest-slime `(101,0,18)`. 점검표 ①에 "어깨선까지"를 넣었다.
- 가챠 결과 칸의 크기 배지는 이상해 보여 뺐다(사용자) — `GachaResultPresenter`를 원래대로. 인벤토리·창고 칸에만 남는다.
- 뿔쥐(horn-rat, enemy-galore-1 Rat) 공격은 팩에 하나뿐이다 — 앞으로 덮치며 무는 8프레임(`Rat_Attack`). `Rat_Ability`는 일어서서 두리번거리는 동작이라 공격이 아니다.
  `Rat_Attack_FX`(먼지·할퀴기)가 있지만 프레임 3~7만 잘려 있어 아직 붙이지 않았다.
- 커밋이 많다는 요청으로 두 저장소를 묶었다(메인 2 · 아트 2).
