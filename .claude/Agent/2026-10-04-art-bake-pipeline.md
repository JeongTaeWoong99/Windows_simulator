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
