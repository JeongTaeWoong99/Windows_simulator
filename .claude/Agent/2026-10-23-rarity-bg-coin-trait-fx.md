---
date: 2026-10-23
title: 칸 등급 배경 그림 · 판매 골드 동전 · 특성 배우기 반짝임 (+ T-052·T-039 실서버 확인)
tags: [client, ui, fx]
---

# 칸 등급 배경 그림 · 판매 골드 동전 · 특성 배우기 반짝임

## 목적 / 배경
- 사용자 요청 "연출 보강 + 실플레이 확인 + 등급 스프라이트"를 MCP로 화면을 보면서 진행했다.
- 연출이 없던 화면 중 판매·특성을 골랐다. 우편은 이미 공개 연출(`RewardRevealFx`)이 있어 뺐고, 경매장은 이번에 하지 않았다.

## 변경 내용
- 그림(그림 저장소) — `make_fx.py`에 `rarity`(`rarity_bg` 64px · 9-slice Border 10을 `.meta`에) · `coin`(`coin_body` · `coin_spark`). 목록 다시 쓰기로 `VisualCatalog`에 올렸다.
- `Shared/visual/FxSprites.cs` — `RarityBackground` · `CoinBody` · `CoinSpark`.
- `Shared/SlotView.cs` — `ApplyRarityBackground`(Awake): 그림이 있으면 `rarityImage`를 Sliced로 깐다. 색은 그대로 `RarityPalette`. 없으면 예전 단색. TODO 삭제.
- `Shared/RarityPalette.cs` — TODO를 "그림 한 장 + 이 색" 설명으로.
- `Overlay/CoinFlyPresenter` 신설 + 씬 `!Overlay Canvas` 1번(경매장 위 · 팝업 아래), `target` = 상태바 `Gold Image`.
- `Managers/ServerWaitManager.cs` — `GoldGainRaised` · `RaiseGoldGain(from, amount)` (토스트와 같은 단방향 창구).
- `Inventory/SellCartPresenter.cs` — 응답 둘(자원·개체)의 골드를 모아 대기가 닫힐 때 한 번 `RaiseGoldGain(sellButton)`.
- `Inventory/TraitPresenter` — 성공하면 그 칸 `TraitNodeView.PlayLevelUp()` → `SlotGainShine.PlayLevelUp` 재사용.
- 문서: `Overlay 규칙.md`(계층 · 표) · `Art 규칙.md`(fx 폴더).

## 주요 결정 / 근거
- 등급 배경은 등급마다 그림을 두지 않고 흰 그림 한 장 + 색 — fx 규칙과 같다. 프리팹이 아니라 코드에서 넣는다(메인 저장소 파일이 그림 GUID를 가리키지 않게).
- 동전 층은 `!Overlay Canvas` — 인벤토리 열에서 상태 캔버스까지 열을 건넌다. 숫자 굴림은 상태바(`NumberRoll`)가 이미 하므로 그림만 난다.
- 특성 반짝임은 인벤토리 캐릭터 레벨업과 같은 모양 — "레벨이 올랐다"를 같은 말로.

## 확인
- 실서버(`ServerRunner`) · 테스트 계정 `claudefx` · 치트는 `C_CheatRequest` 직접 · 캡처는 `ScreenCapture.CaptureScreenshot` + `editor_focus`(카메라·`screenshot` 명령은 검게 나온다).
- T-039 해금(요청 → 성공 → 자물쇠·번짐·빈 칸) · T-052 레벨업(슬롯 기둥·`LV.26` · 인벤토리 칸 금빛) 실서버 흐름 정상.
- 등급 6종 배경 · 판매 동전(판매 버튼 → 상단 골드, 숫자 굴림) · 특성 칸 반짝임 캡처 확인. 콘솔 게임 오류 없음.

## 후속 작업 / 주의사항
- ⚠️ 씬 저장 — `unity cmd save_scene`도 레이아웃 구동 값을 구워 2,700줄 diff가 났다(플레이를 한 번 돈 뒤라서로 보인다). 씬을 되돌리고 **YAML에 오브젝트 블록만 직접 넣어** +50줄로 맞췄다. 스크립트로 `OpenScene`을 다시 부르면 에디터가 응답 없이 걸렸다(백그라운드로 밀림).
- 그림 저장소(`Assets/Art`)는 따로 커밋해야 한다 — 코드보다 먼저 푸시.
- 관찰: 골드 1억 자리에서 상태바 골드 글자가 [우편] 버튼에 닿는다. 슬롯 레벨업 `LV.26` 글자가 오른쪽 위 `1.25배`와 살짝 겹친다.
- 동전은 판매에서만 부른다 — 우편 골드 수령·경매 정산에 붙이려면 `RaiseGoldGain`만 부르면 된다.
