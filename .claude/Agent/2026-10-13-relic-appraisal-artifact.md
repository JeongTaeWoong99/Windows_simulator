---
date: 2026-10-13
title: 미감정 유물 · 감정 · 아티팩트 기획과 첫 데이터 (T-150)
tags: [design, data, mockup]
---

# 미감정 유물 · 감정 · 아티팩트 기획과 첫 데이터 (T-150)

## 목적 / 배경
- 유적 발굴(T-142)의 고유 콘텐츠 — 상자 자리에 미감정 유물이 나오고, 골드로 감정하면 상자 같은 보상 + 낮은 확률로 아티팩트(보유만으로 계정 영구 효과).

## 변경 내용
- 기획: `GameDesign/design/gathering/excavation/유물감정.md` (구상) · 목업 `GameDesign/design/ui/유물감정목업.html` (아이콘 7종 · Special 칸 애니메이션 · 감정 연출 · 보관함=도감).
- enum: `GlobalRarity.Special`(7) · `ItemType.Box`(8)·`Consumable`(9)로 상자·소모품 재분류(`Special`(7)은 비움) · `UserTraitEffect.AppraiseCostDiscount`(4)·`RelicChanceAdd`(5).
- 새 엑셀 `Artifact.xlsx` / `ArtifactTable` — 일반 12 · 고급 10 = 22행 (번호 001~022). Unity `.meta` 2개는 손으로 만들었다(새 GUID).

## 주요 결정 / 근거
- 유물은 희귀도당 1종(레벨 없음) — 레벨은 확률만 가른다. 상자를 **대신**하고 합계 확률은 상자의 절반쯤.
- Special 유물은 판정에서 안 나오고 **다른 유물 감정에서만** — 판정 주기 차이로 Lv1이 81배 유리해지던 문제가 사라진다. 확률은 감정 비용 비례(추천, 미확정).
- 아티팩트 번호 = **도감에 실린 순서**. 등급 대역(5자리 → 1만 칸 → 100번대)을 거쳐 폐기 — 등급을 고치면 DB 번호를 옮겨야 하고, 등급은 색으로 이미 보인다.
- 중복 보유 가능 · 효과는 1개분 · 인벤토리 아닌 전용 보관함(= 도감 한 화면) · 효과 합계 표시 없음 · 세트 효과 없음 · 상한 없음(감정 비용 할인만 50%).

## 서버 구현 (같은 날 이어서)
- 데이터: 유물 7종 `100061~100067`(`ItemTable.유물` · `ItemType.Relic`=10) · `ItemTable.OpenCostGold` 컬럼(전 시트, 0 = 무료) · 감정 풀 25~31(상자 풀 9·13·17·20·23 복사 · 가중치 ×10 · **황금 열쇠는 뺐다** — 열 수 있는 것이 섞이면 고리) · 새 시트 `GachaArtifactTable`(아티팩트 줄 합계 ≈5%) · Special 유물 줄(일반~신화 풀) · 발굴 `CommonRewardOverrideTable` 24행(덮어쓰기라 발굴은 상자가 안 나온다).
- `GachaService.OpenBox` — 잔액 확인 → 유물 차감 → 골드 차감 순(차감 실패는 오지 않는 경로, Error 로그). 아티팩트는 `GrantArtifacts`가 창고·우편을 거치지 않고 보관함으로 — 우편 넘침 경로에서도 지급된다.
- `User.Artifact.cs` — 처음 켜지는 번호가 속도·산출량·유물 확률을 바꾸면 **넣기 전에 `SettleWorkStation`**(소급 방지), 속도면 `ApplyWorkStationSpeed`. 효과는 `TraitAddPermille`·산출량 가산에 더하고(특성과 같은 자리), 유물 확률은 `CommonRewardCatalog.Roll(chanceAddPermille)`에 곱한다(발굴만).
- 패킷 `S_ArtifactListResponse`(74) · `ArtifactInfo` · `GachaRewardInfo.ArtifactTid`(끝에 붙임) · `EGachaRewardType.Artifact`.
- DB `0003-user-artifact.sql` — 로컬 DB는 python으로 같은 SQL + `user_version = 3`을 한 트랜잭션에 적용(서버 기동과 같은 결과).
- T-153(처음 번호 T-149 — main의 T-149와 겹쳐 옮김) 해결 — `BoxSheetTest`의 상자 = `ItemType.Box`, 무한 개봉은 고리 검사로. 서버 테스트 879/879.

## 후속 / 주의
- 클라 T-151(태웅) — `S_ArtifactListResponse` 핸들러가 없어 MIKA001 경고. 판매·거래소 T-152.
- 남은 결정: 레벨별 확률 값 · Special 확률 · 희귀 이상 아티팩트 — 전부 테스트값으로 돈다.
