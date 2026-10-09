---
date: 2026-10-09
title: 폰트를 Pretendard로 교체 · EN/KR Static 에셋 분리 (T-100)
tags: [client, ui, font, tmp]
---

# 폰트를 Pretendard로 교체 · EN/KR Static 에셋 분리 (T-100)

## 목적 / 배경
- 검색에 낱자모(ㅁ·ㅇ)를 치면 □ — 옛 `neodgm_pro SDF`를 구울 때 호환 자모 `3130-318F`를 빼먹었다(완성형은 다 있었다).
- 흐림 — neodgm은 16px 격자 픽셀 폰트인데 씬 글자 크기가 16 배수가 아니다. 픽셀 폰트를 유지하는 한 못 고친다 → 일반 폰트로.
- 48MB 한 파일이라 기호 하나 더해도 통째로 다시 써졌다 → EN/KR로 분리.
- 언어 전환(KR/EN)은 시트 구조가 먼저라 T-135(진우 · 이슈 #64) · T-136(클라)로 뗐다.

## 변경 내용
- `Assets/Resources/Fonts/` — `Pretendard-Regular.ttf` · `Pretendard OFL.txt` · `Pretendard EN SDF`(기본, 48pt/2048) · `Pretendard KR SDF`(EN의 Fallback, 32pt/4096). neodgm SDF·머티리얼 2개 삭제
- `TMP Settings.asset` — 기본 폰트 = EN
- 프리팹 15개 · 원본 씬 305곳의 TMP 폰트·머티리얼 교체 (에디터 C# 일괄)
- `UI 규칙.md` 폰트 절 · `Inventory 규칙.md` · `TraitPresenter`·`SlotView` 주석 — neodgm 전제 문구 정리

## 주요 결정 / 근거
- Pretendard 선택(사용자) — UI용, Noto Sans KR보다 폭이 좁아 기존 배치에 덜 넘친다.
- KR은 32pt/padding 4/4096에서 11,251자로 **아틀라스가 꽉 찼다** — 글자를 더할 여유가 없다. 기호는 EN에 더한다.
- `neodgm_pro.ttf`는 남겼다 — 서버 콘솔(에디터 IMGUI 로그 창)이 직접 쓴다.
- 굽기: `CreateFontAsset(Dynamic)` → `TryAddCharacters` → `Static`으로 바꿔 저장. 다시 구울 때는 같은 경로·GUID로 덮어써야 참조가 안 끊긴다.

## 후속 작업 / 주의사항
- 실측 남음 — 자모 입력 · 1x 가독성 · 글자 폭이 바뀌어 넘친 곳.
- `★`·`→`가 이제 나오지만 ★ 스프라이트·`▶` 표기는 그대로 뒀다(바꿀지 별개).
- 이모지·`⏸`·`✕`·`ℹ`는 Pretendard에 없다.
- `unity cmd eval_file`이 무거운 작업에서 "Main thread operation timed out after 5000ms"를 내도 에디터에선 계속 돈다 — 파일로 결과를 확인한다.
