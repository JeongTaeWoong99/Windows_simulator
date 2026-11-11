---
date: 2026-11-11
title: 화면 폰트 갈무리11 · 등급 귀퉁이 장식 제거 · 등급 프레임 시안
tags: [client, ui, font, tmp]
---

# 화면 폰트 갈무리11 · 등급 귀퉁이 장식 제거 · 등급 프레임 시안

## 목적 / 배경
- 이슈 #121 — Pretendard가 기본 폰트처럼 보여 도트 게임에 맞는 무료 폰트로 바꿔 보자는 진우 의견.
- 사용자: 칸 귀퉁이 장식(쇠 꺾쇠·금 귀퉁이)이 별로 · 작업슬롯 위에 빈 줄이 생겼다.

## 변경 내용
- `Assets/Resources/Fonts/` — `Galmuri11.ttf`(v2.40.4, OFL) · `Galmuri OFL.txt` · `Galmuri EN SDF`(48pt/pad 6/2048 · 860자) · `Galmuri KR SDF`(24pt/pad 3/4096 · 11,299자). 폴백: Galmuri EN → Galmuri KR → Pretendard EN(갈무리에 없는 기호 717자) → Pretendard KR
- `TMP Settings` 기본 폰트 · 프리팹 21개 · 원본 씬의 폰트·머티리얼(`m_sharedMaterial`) · 입력칸 `m_GlobalFontAsset`을 Galmuri EN으로
- `RaritySkin` — 귀퉁이 장식(`Rarity Frame` 자식) 제거, `Dress`/`Set(bg, rarity)`만. 6개 View의 `_rarityFrame` 제거 · `FxSprites.RarityFrame(Gold)` 제거
- 바탕화면 `UI 전후 비교/` — 비교8~10 · `등급프레임_시안.png`(현재 · A 입체 테두리 · B 아래 빛 · C 등급 단계 · D 등급 보석)

## 주요 결정 / 근거
- 작업슬롯 빈 줄의 원인은 장식 자식 — 루트가 `VerticalLayoutGroup`이라 자식이 한 줄을 차지했다. 장식을 빼며 함께 사라졌다.
- KR을 24pt(픽셀당 2텍셀)로 — 36pt는 4096 아틀라스에 안 들어간다. EN은 48pt(4텍셀).
- 갈무리는 11px 격자라 11의 배수 크기에서 가장 또렷하다(T-100에서 neodgm을 버린 이유와 같은 성질) — 실측으로 판단.

## 후속 작업 / 주의사항
- 원본 씬은 에디터에 저장 안 된 변경(WorkSlotFrame 색 오버라이드 · 자동 크기)이 있어, 커밋엔 폰트 교체만 넣고 나머지는 작업 트리에 남겼다.
- `fx/rarity/rarity_frame(_gold).png`는 안 쓰지만 남겼다 — 프레임 시안이 정해지면 지우거나 바꾼다.
- 지뢰: `unity cmd eval`의 `delayCall`이 이번엔 돌지 않았다(에디터가 뒤에 있을 때) — 굽기는 그냥 동기 실행하고 5초 타임아웃 뒤 결과 파일로 확인했다.
