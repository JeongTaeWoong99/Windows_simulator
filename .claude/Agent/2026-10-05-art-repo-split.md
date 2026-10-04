---
date: 2026-10-05
title: 그림을 비공개 저장소로 분리 (T-121)
tags: [client, art, infra]
---

# 그림을 비공개 저장소로 분리 (T-121)

## 목적 / 배경
- T-120에서 그림을 `.gitignore`로 빼 내 PC에만 있었다 → 팀원은 그림 없는 화면을 본다.
- 메인 저장소는 공개 · 원본 팩은 재배포 금지가 많다 · 메인을 비공개로 돌리면 Pages·Actions 무료 시간을 잃는다.

## 변경 내용
- 비공개 저장소 `JeongTaeWoong99/Windows_simulator_Art` 생성, `Assets/Art/` 자리에서 `git init` → 첫 커밋(2,742개, `.meta` 포함) → 푸시. 진우·규빈 쓰기 권한 초대.
- 그림 저장소 `.gitignore`: `Art 규칙.md`(+meta)는 메인이 갖으므로 무시. `README.md`에 받는 법.
- 메인: 무대 프리팹(`WorkStationSlotView`)의 `SlotStageView.catalog` 참조를 비움 → 메인 추적 파일 중 그림 GUID를 가리키는 것 0개(전수 grep). 화면은 `VisualCatalog.Current`(Resources)로 찾는다.
- `Art 규칙.md` "그림 저장소" 절(받는 법 · 평소 흐름 · 푸시 순서) · T-121 In Progress.

## 주요 결정 / 근거
- 서브모듈이 아니라 **같은 자리에 독립 저장소** — 메인은 그 폴더를 무시할 뿐. 그림만 바꿀 때 메인 커밋이 필요 없다.
- `Assets/Art/`에 이미 `Art 규칙.md`가 있어 `git clone`이 안 된다 → `git init` + `fetch` + `checkout -t origin/main`. 임시 폴더에서 실측(변경 0).
- **Windows 경로 260자**: 실측 중 `Filename too long` — dark-forest·cainos 팩의 긴 폴더 이름을 `pack`으로 `AssetDatabase.RenameAsset`(GUID 유지, 다시 굽기 결과 동일). 저장소 안 최장 경로 135 → 101자.
- Git LFS 안 씀(사용자 결정) — 21MB. 1GB에 가까워지면 다시 본다.

## 남은 것
- 메인 커밋 푸시는 사용자 보류 중 — 이슈는 메인 푸시 뒤에 올린다(본문이 `1cee1dc`를 가리킨다).
- 팀 전파(이슈) · 그림 없이 열었을 때 대체 화면 실측.
