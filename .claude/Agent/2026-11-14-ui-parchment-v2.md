---
date: 2026-11-14
title: UI 양피지 2차 — 참고 이미지 색감에 맞춤
tags: [client, ui, art]
---

# UI 양피지 2차 — 참고 이미지 색감에 맞춤

## 목적 / 배경
- 사용자: 1차 양피지가 참고 이미지와 색감이 다르고 스타일이 뒤죽박죽 — 화면을 다 열어 보며 색감·이미지를 맞춰라. 강화·판매 카트·특성 트리·팝업도 찍어서 확인.
- Unity AI 크레딧 0 — 그림은 Claude가 PIL 코드로 그렸다(`make_ui.py`·`make_slots.py`). 스킬 문서는 열지 않았다.

## 변경
- 그림 저장소: `make_ui.py` 양피지 함수 — 얇은 진갈 외곽선 + 안쪽 밝은 줄 + 귀퉁이 못 · 붉은 리본(금실 없음) · 새 `ui_paper`(종이 결 32px). `make_slots.py` — `rarity_bg` 검정 반투명 속 + 2px 등급 테두리 · 빈/잠긴 작업 칸 갈색.
- 팔레트: 양피지 `D6BC8E`/`CBAF80` · 칸 `7B6345` · 리본·핵심 버튼 `A13732` · 버튼 `E8D5A6` · 글씨 진갈 `3A2814`/크림 `F4E8CE` (표는 Art 규칙).
- 코드: `UiSkin.AddPaper`(창 바탕에 종이 결) · `FxSprites.UiPaper` · `UIRichText.Paint`가 TextMain·Sub·Disabled를 글씨 기본색으로 이어받음 · `GoldOnDark` · `UIThemePalette.OfFor` · `UIThemeColor` 밝은 버튼 잠김색 반반 · `SlotView` 찾기 제외 세피아 · 판매 줄 금액·Lv·★ 밝은 금.
- 씬·프리팹: 큰 목록 바탕 Slot → PanelSub · 창 안 작은 상자 29곳 Overlay → Slot · 밝은 바탕 글씨 TextDark/HighlightDark로 · 흐린 TextDark 투명도 0.62 이상 · 아틀라스 다시 굽기(옛 rarity_bg가 남아 있었다).
- 문서: Art 규칙(양피지 2차 표) · Shared 규칙(글씨 색 이어받기·OfFor·GoldOnDark·안쪽 상자).

## 확인
- 플레이 캡처 — 메인·인벤 3탭·판매 카트·응축·강화·설정·우편·경매 4탭·거점·건물 업그레이드·아티팩트·확인/수량/알림 팝업. 바탕화면 `UI 전후 비교 6차`.
- 특성 탭은 Lv0 건물이라 열리지 않아 찍지 못했다. 아티팩트 캡처는 세피아 수정 전 화면이다.

## 남은 일
- 알림 창이 좁다(전부터의 배치). 경매 작은 머리글 글씨가 여전히 옅다.
