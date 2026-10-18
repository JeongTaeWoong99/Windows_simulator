---
date: 2026-10-18
title: 원본 팩을 _source/<종류>/<팩>/ 선반으로 정리하고 원본 정리 규칙을 세운다
tags: [client, art, docs]
---

# 원본 팩 정리 규칙 (`_source/<종류>/<팩>/`)

## 목적 / 배경
- 사용자가 그림·UI·음악 팩 20개를 `Assets/` 바로 아래에 풀어 넣었다 — 메인(공개) 저장소의 미추적(`??`) 상태라 잘못 커밋될 위험이 있었다.
- 앞으로 사운드·UI·이펙트 원본이 늘어날 것에 대비해 `_source/`를 종류별 선반으로 나눴다.

## 변경 내용
- 기존 `_source/<팩>` 15개 + 새 팩 20개 → `_source/{characters,environment,icons,ui,fx,audio/bgm}/<팩>/` (`.meta` 함께 이동, Unity 닫힌 상태에서 파일로).
- `_source/fx/fx-placeholder/make_fx.py` — 한 단계 깊어져 `FX_ROOT`를 `../../../fx`로.
- `Art 규칙.md` — "원본 정리" 절(종류·규칙·원본 목록) 추가, 경로 참조 갱신. `FxSprites.cs` 주석 경로.

## 주요 결정 / 근거
- 종류는 "팩이 무엇을 담았나"로 — 용도(캐릭터로 쓸지 몬스터로 쓸지)는 바뀌므로 영웅·몬스터를 `characters/` 하나에 둔다.
- 제작자 폴더에 여러 팩이 폴더를 나눠 쓰는 `karsiori`는 쪼개지 않았다(애니↔스프라이트가 상위 폴더를 공유).
- 이름 충돌: LuizMelo `Hero_Knight` → `hero-knight-luizmelo` (기존 `hero-knight`는 다른 팩).
- 아트 도구(`ArtSpec.SourceRoot`)는 `_source` 아래 경로를 보지 않고 GUID로 원본을 잡아 옮겨도 굽기가 깨지지 않는다.

## 업데이트 (2026-10-18) — 사용자 결정 반영
- 음악 wav → ogg Vorbis(압축 0.4 ≈ q6), 182MB → 29MB. wav는 지웠다.
  - 변환 함정: `soundfile`(libsndfile 1.2.2)로 곡 전체를 한 번에 `sf.write`하면 Windows에서 **스택 오버플로(0xC00000FD)로 출력 없이 죽는다** → `sf.blocks`로 4096프레임씩 쓰면 된다.
- 팩 안 `.cs`·`.asmdef` 전부 삭제(enemy-galore-1 · hero-knight 데모·ColorSwap · cainos-village-props · free-pixel-art-forest · pixel-ui-icons). 굽기는 그림·클립만 써서 영향 없다.
- 영웅·몬스터는 `characters/` 하나 유지.

## 업데이트 (2026-10-18) — 팩을 쓸 재료만 추려 같은 모양으로
- 팩 안을 `sprites/`·`anims/`·`audio/` + 루트 라이선스 문서로 통일. 씬·프리팹·타일·컨트롤러·폰트·PSD·데모/미리보기 그림·확대본 1,417개(48.6MB) 제거.
- 정리기 `Assets/Art/_source/tidy_source.py` — 규칙을 코드로. 레시피·결과물이 GUID로 가리키는 원본(131개)과 남긴 애니가 쓰는 그림은 무조건 남긴다. 다시 돌려도 바뀌는 것 없음(멱등).
- `karsiori`를 팩별(gem·chest·potion·food·rock-pile·spark)로 나눴다 — 어차피 폴더를 다시 짜는 김에 "팩 하나 = 폴더 하나"로.
- 검증: 레시피·결과 SO 29개의 끊긴 참조 0 · `_source` 의존 131개 그대로.
- 함정
  - `.anim` 스프라이트 키 판정을 `m_PPtrCurves:` 다음 줄이 `- curve:`인지로 했다가 `- serializedVersion: 2`로 시작하는 형식(karsiori 등) 86개를 버릴 뻔했다 → 다음 줄이 `- `로 시작하는지만 본다.
  - 에디터가 켜져 있으면 `_source` 폴더 자체의 이름 바꾸기는 잠겨 실패한다(WinError 32) → 폴더째가 아니라 안의 항목을 옮긴다.
  - 아트/검사의 배경 ❌(높이가 stageHeight가 아니다)는 이번 작업과 무관 — 사람이 조정한 무대 높이(138·85·80…)와 그림 높이 128이 원래 다르다.

## 후속 작업 / 주의사항
- 그림 저장소의 **과거 기록**에는 지운 파일이 남는다(이미 푸시된 옛 팩 분). 새로 받는 사람의 작업 폴더만 작아진다 — 기록까지 줄이려면 히스토리 재작성이 필요하다.
