---
date: 2026-10-28
title: 아티팩트 아이콘 22종 · Special 칸 무지개 · 결과 팝업 골드 동전 (T-151)
tags: [client, ui, fx, art, artifact]
---

# 아티팩트 아이콘 22종 · Special 칸 무지개 · 결과 팝업 골드 동전

## 목적 / 배경
- 사용자 요청: "아트 없이(또는 임시 리소스로) 느낌을 살리는 작업"을 MCP로 이어서 진행
- 고른 셋: ① 아티팩트 아이콘 자리(지금은 이름 첫 글자) ② Special 칸 애니메이션(목업) ③ 골드 받기 동전 연출 확대(지금은 판매 목록 한 곳뿐)
- 일감 → `tasks/T-151` (①②) · ③은 일감 없이 바로 처리

## 변경 내용
- 아이콘: `ArtSpec`·`IconRecipe`·`ArtBaker`·`ArtCatalogSync`에 `artifacts` 칸 → `VisualCatalog.ArtifactIconOf` → `ItemIconContent.ForArtifact`·`EquipSlotSource`·`GachaResultPresenter`·`ArtifactCodexPresenter`·`AuctionText`가 사용. 레시피 22줄은 MCP로 채우고 구웠다(대응표 → `Art 규칙.md` 임시 배치 표)
- Special: `UI/Shared/SpecialSlotFx.cs` 신설 · `SlotView.ApplyTint` 끝에서 켜고 끈다 · 무지개 그림 `fx/special/special_rainbow`(`make_fx.py special`)
- 동전: `GachaResultPresenter.Show`에 `gold` 인자 — 골드 칸이 튀어나온 뒤(`FlyGoldAfterAsync`) 또는 스킵 때 `RaiseGoldGain`. 상자 개봉 · 우편 받기 · 감정 · 가챠가 한 번에 덮인다

## 주요 결정 / 근거
- **아티팩트는 대체 아이콘을 두지 않는다** — 자원·장비처럼 0번 그림으로 버티면 22종이 같은 그림이 된다. 없으면 null → 칸이 이름 첫 글자(지금까지의 모양)
- **도감의 본 적 없는 번호는 아이콘도 가린다** — 이름을 가리는 것과 같은 이유
- **무지개 그림은 흰색 규칙의 예외** — Special 한 등급의 고정색. 흰 원뿔 + 색상 순환 대안도 있었지만 목업의 원뿔 그라데이션과 달라 색을 그림에 넣었다
- Special 테두리는 공개 연출(`RewardRevealFx`)의 "띠 마스크 + 도는 원뿔" 구조를 그대로 빌렸다 — 띠 `reveal_frame`, 빛 줄 `reveal_shine`, 별 `reveal_sparkle` 재사용
- **딤·흑백 칸에서는 무지개를 끈다** — 도감 미장착(흑백)·꺼짐(딤) 칸이 돌면 켜진 칸과 구분되지 않는다
- 동전은 **결과 팝업 한 곳**에 붙였다 — 우편·상자·감정이 모두 이 팝업으로 보상을 보여 주므로 화면마다 붙이지 않는다. 골드 칸은 `Key == 0`으로 찾는다(골드는 TID가 없다)

## 후속 작업 / 주의사항
- **굽기가 유물 아이콘 7장(item_100061~67)을 다시 인코딩한다** — 같은 원본이라 그림은 같고 바이트만 다르다. 이번엔 `git checkout`으로 되돌렸다. 레시피 전체 굽기를 돌릴 때마다 생길 수 있다
- 그림 저장소(`Assets/Art`) 변경은 커밋하지 않았다: `icons/artifacts/` · `icon_recipe.asset` · `VisualCatalog.asset` · `fx/special/` · `make_fx.py` · `Art 규칙.md`
- Special 연출은 칸 크기를 **처음 켤 때** 한 번 읽는다(빛 줄 굵기·별 크기) — 칸 크기가 바뀌는 화면이 생기면 다시 잰다
- MCP 확인: 1234 계정 장비 탭 아티팩트 9칸 아이콘 · 칸 하나를 Special로 그려 캡처(테두리 확인 · 빛 줄·별은 정지 화면이라 확인 못 함) · 우편 1092(3,046 G)를 받아 동전 이벤트 확인. **우편 1092는 받은 상태로 남았다**
- 테스트하려고 서버 둘(`WSGameServer` · `AuctionServer`)을 띄웠다. 경매 서버는 `DOTNET_ROOT=~/.dotnet`이 있어야 뜬다
