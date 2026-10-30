---
date: 2026-10-31
title: 소리 구조 · 아이콘 흰 테두리·이름 점검 · 큐브·골드 아이콘 · 견습 발굴꾼 다시 만들기 · 숙련 채굴꾼
tags: [client, editor, ui, data, docs]
---

# 소리 구조 · 아이콘 점검 · 캐릭터 2명

## 목적 / 배경
- 사용자 피드백: 흰 테두리 아이콘이 남았다 · 이름과 그림이 안 맞는다 · 소리가 없다 · 삽 모션이 어색하다(아래→위 퍼 올리기로) · 곡괭이 변형 캐릭터 추가.
- 일감 → `tasks/art/T-189` · `tasks/art/T-174` · `tasks/archive/T-134`. 엑셀은 PR #101.

## 주요 결정 / 근거
- **흰 테두리는 다시 그리지 않고 굽기에서 벗긴다**(`ArtImage.StripLightRim`, 가장자리 70%). 기준을 내리면 밝은 파랑 통·상자(37~50%)까지 벗겨진다 — karsiori 보석(41%) 1개는 그래서 남겼다.
- **이름 점검 결과는 레시피 줄에 둔다**(`fit`·`checkedName`·`checkedOn`). 표만 문서에 두면 엑셀 이름이 바뀌어도 아무도 모른다 — `ArtValidator.CheckIconNames`가 지금 이름과 비교해 "이름 바뀜"을 알린다.
- **소리 키는 파일 이름**(이펙트 `fx/`와 같은 방식). 목록은 `ArtCatalogSync`가 쓴다 — 손으로 채운 목록은 파일을 덮어 바꿀 때 어긋난다.
- **채널 4개 = AudioSource 3개 + 마스터 곱**. UI·SFX를 가른 것은 "버튼 소리만 끄기"를 위해서. 음량 저장은 `DisplayManager`처럼 에디터도 저장값(소리는 에디터에서도 들림).
- **자주 나는 소리(채취 타격)는 넣지 않았다** — 바탕화면 상주 앱이라 거슬린다.
- **캐릭터 권장 프레임 4·6·5** — 기존 27명 평균 7·7·8이지만 4프레임짜리도 어색하지 않았다. 크레딧은 시트 1장 단위라 프레임 수와 무관 — 아끼려면 시트 수를 줄인다(변형은 기준 그림 편집).
- 이름: 사용자는 "숙련 발굴꾼"을 골랐지만 "곡괭이는 채굴이 높아야" 해서 **숙련 채굴꾼**(채굴 4)으로 했다.

## 함정
- `EditSpriteWithPrompt`는 `savePath`를 무시하고 **원본을 덮는다** — 복사 후 git으로 원본 복구했다.
- 프레임 수를 바꿔 다시 구워도 결과 SO의 프레임 배열은 예전 개수 그대로다(비어 있을 때만 채움) — 배열을 비우고 다시 굽는다.
- 효과음 생성 모델은 1초를 채워 뒤가 무음 — `_source/audio/trim_sound.py`로 자른다.
- 워크트리에서 파이프라인을 돌리면 `MikaSourceGen.dll`이 다시 빌드돼 커밋에 섞인다 — PR #101에서 따로 되돌렸다.

## 후속 작업 / 주의사항
- 게임 안 실측 대기: 큐브·골드 아이콘 · 소리·음량 슬라이더 · 두 캐릭터 모션. 1031은 PR #101 머지 후 보인다.
- 공격 아래쪽이 칸 밖으로 잘린다는 굽기 경고(두 캐릭터) — 사소, 남겨 둠.
- 아이콘 대체 174 · 아이콘 없는 10종 → T-189.

## 업데이트 (2026-10-31) — SoundManager Common 이동 · 슬라이더 색 · 크롭 편집
- **SoundManager·SoundCatalog → `Common/sound/`** (git mv로 GUID 유지 — 씬 컴포넌트·`SoundCatalog.asset` 참조가 산다). 게임 의존 둘을 뗐다: 음량 키는 `SoundManager.PrefsKey`(`Audio.<채널>` 그대로 — 저장값 유지), 시작 BGM은 `Sounds.BgmIdle` 대신 인스펙터 `startBgmKey`. 마스터에 복사·문서 등록만 하고 **툴킷 커밋은 안 했다**.
- **오디오 슬라이더가 회색이던 원인은 색이 아니라 `UIThemeColor.role`(TextDisabled)** — Graphic 색을 바꿔도 테마가 런타임에 되돌린다. 색을 바꿀 땐 역할을 바꾼다(채움 Accent · 값 TextMain).
- RunCommand에서 `result.RegisterObjectModification`을 쓰면 `SceneDirtyTracer` 경고가 실패로 잡혀 변경이 되돌아간다 — `SerializedObject`/직접 대입 + `SaveScene`으로 한다.
- **크롭 편집 `CharacterCropPanel`** — 크롭 사각형의 진실은 레시피라 [적용]이 레시피에 쓰고 그 캐릭터만 굽는다. 자동과 같으면 0으로 둬서 템플릿 변화를 계속 따른다.
