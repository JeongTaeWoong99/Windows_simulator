---
date: 2026-11-14
title: 작업슬롯 2줄(4칸) 보기 · 배경 위쪽 잇기 · 캐릭터 크기 전부 중형 · PixelOE·Codixel 이식
tags: [client, ui, art, pipeline]
---

# 작업슬롯 2줄 보기 · PixelOE·Codixel

## 목적 / 배경
- 사용자: 1080p에서 작업슬롯이 3줄(6칸) 보여 무대가 작다 — 4칸이 보이게 세로를 늘리자.
- 1차로 배우까지 칸 높이 비율로 키웠더니(약 2배) 사용자: "원래 비율에서 위가 더 보이길 원했다 · 대·중·소 구분 없이 일단 중에 맞춰".
- 쇼츠 2편의 8가지 중 PixelOE·Codixel을 이식하고 샘플을 만들고 커밋 (사용자 지시).

## 변경
- `Common/ugui-layout/FlexibleGridLayoutGroup` — `_fitRowsInParent`(>0): 셀 세로 = (부모 높이 − 위아래 패딩 − 줄 간격) ÷ N. 부모 높이만 바뀌면 알림이 없어 `Update`에서 살핀다. **툴킷 사본 — 마스터 반영은 `/unity-skill-sync`로 확인 후.**
- 씬: 작업슬롯 `Content` 그리드 `Fit Rows In Parent` 2. 1080p 칸 263×211 → 263×330, 무대 243×111 → 243×230.
- `SlotStageView` — 배경 배율 = 배우 배율(k). 예전 `max(k, H ÷ stageHeight)`(2026-10-10)는 칸이 높아지자 배경만 확대돼 버렸다. 층을 패널 높이까지 늘리고 uv 세로를 1 너머로 — 배경 텍스처가 세로 Clamp(24장 전부 확인)라 맨 윗줄(하늘·나무 기둥)이 위로 이어진다. 배경 5종 시뮬레이션으로 확인.
  1차의 `referenceHeight`(배우를 칸 높이 비율로 키움)는 지웠다.
- 캐릭터 SO 30종 전부 `sizeClass` 중형 + 중형 템플릿 재적용(`ArtBaker.ReapplySizeTemplate`) — 소형(박쥐·슬라임·쥐)은 커지고 대형(골렘·죽음 인도자)은 작아진다. 중형이던 캐릭터의 손 조정값(martial-hero 1.3 등)도 템플릿 값으로 덮였다.
- **PixelOE 경량판** `Art/_source/pixeloe_lite.py` — 원본(torch·opencv) 대신 numpy + Pillow로 legacy 알고리즘(대비 기반 선 굵히기 · 대비 기준 축소 · Lab 색 맞춤 · 웨이블릿 저주파)을 옮김. `ai_to_pixel.py --oe [--outline]`. 라이선스 `_source/licenses/PixelOE-LICENSE.txt`. numpy를 기본 site-packages에 설치(`python -I`가 사용자 site를 안 본다).
- **Codixel** `Art/_source/fx/codixel~/` — `core/pixel.js`(MIT) · 원 문서 · `samples/make_samples.js`(나무 흔들림 · 허수아비 맞음 · 보석 3종 · 모닥불) · `records/` git 제외. `claude_gen.py`에 `codixel` 생성기. 스킬 `.claude/skills/client/codixel/SKILL.md` · CLAUDE.md 표.
- 캡처 `Desktop\UI 전후 비교 10차` · 샘플 `Desktop\리소스 도구 샘플 (PixelOE·Codixel)`.

## 결정 근거
- PixelOE vs 기존 최빈색(아이콘 3 · 광부 64px): PixelOE가 지도 길·열쇠 무늬·외곽선을 살린다. 평평한 그림은 차이가 작아 기본은 그대로 두고 `--oe`를 선택지로.
- Codixel `P.Gem`은 24px에서 덩어리로 뭉갠다 → 40px. `P.Smoke`는 이 크기에서 흰 기둥 → 모닥불에서 뺐다. 허수아비는 기둥을 고정 뼈로 두지 않으면 기둥째 꺾였다.

## 남은 것
- 쇼츠 나머지: sprite-gen의 AI 없는 후처리(크로마 분리·격자 검출)가 AI 시트 정리에 쓸 만함 — 사용자 결정.
- 창 높이가 아주 낮으면 칸이 납작해진다(줄 수 고정) — 실제 창에서 확인.
- 캐릭터 크기 중형 통일 후 머리 잘림·멈춤 거리 실측.
