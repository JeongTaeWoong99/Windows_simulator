---
date: 2026-11-22
title: 채취 6단계 클라 대응 · 테마 상자 6종 그림 · 장비 칸 아이템 레벨
tags: [client, audio, workstation, industry-level, art, icons, equip, appraise, item-use, guild, auction, T-208, T-200, T-211, T-212, T-213]
---

# 채취 6단계 클라 대응 · 테마 상자 6종 그림 · 장비 칸 아이템 레벨

## 목적 / 배경
- PR #151(T-208)이 클라 수정 없이 main에 머지돼 레벨 버튼이 깨진 상태였다(#150). 산업 레벨이 1~5 연속 번호에서 실제 값 1·20·40·60·80·100으로 바뀌었다.
- 테마 상자 6종(100081~100086) 그림이 없었다. 옛 상자 15종(100019~100033)은 데이터에서 지워졌다.
- #146: 장비 칸에 아이템 레벨을 보여 준다.

## 변경 내용
- `GameDataLoader` — `GetIndustryLevels`(낮은 순 목록) · `GetIndustryLevelStep`(레벨 → 단계 번호) · `GetIndustryLevelAtStep`(단계 → 레벨) 추가. `GetMaxIndustryLevel`·`GetBoxLevel` 삭제.
- `PlayerDataModel.IsIndustryLevelOpen` — 개척 특성 레벨 ≥ `RequiredTraitLevel` (서버 `User.IsIndustryLevelUnlocked` 사본).
- `WorkStationSelectPresenter` — i번째 버튼 = 그 산업의 i번째 단계. 잠김 툴팁은 특성 레벨(`RequiredTraitLevel`)을 적는다.
- `TraitPresenter` — 개척 Lv k가 여는 것을 k번째 단계 레벨로 적는다.
- `SlotStageView` — 대상 그림(`TargetVisual.levels`)은 단계마다 한 장이라 레벨을 단계 번호로 바꿔 넘긴다.
- 상자 `LV.n` 배지 삭제(인벤토리 · 결과 창) — 테마 그림으로 구분된다.
- `EquipSlotSource.CategoryTagOf` — '채굴 Lv10'처럼 분류 + 아이템 레벨.
- 씬 `DesktopWindow_Control.unity` — `Level 6 Button` 추가(5번 복제) · `industryLevelButtons` 6칸.
- 그림 저장소(`Assets/Art`, 별도 git): `_source/icons/unity-ai/chests_theme.py` + raw 2장 · 아이콘 6장 · 레시피 교체 · 굽기 · 목록 다시 쓰기. 옛 상자 15종 그림·원본·`chests_from_grids.py` 삭제.

## 주요 결정 / 근거
- 레벨 버튼은 버튼 순서 → 레벨이 아니라 **순서 → 단계 → 표의 레벨**로 읽는다. 클릭 콜백은 인덱스를 캡처하고 누를 때 지금 산업에서 레벨을 찾는다(산업마다 단계의 레벨이 달라도 맞게).
- 씬 저장 diff가 2,800줄로 커졌다 — 원본 씬에 레이아웃이 정하는 값이 0으로 저장돼 있는데, `eval`로 저장하면 실제 숫자가 박혔다. 원본 씬 문서에 새 오브젝트 18개 + 배열 + 부모 `m_Children`만 옮겨 붙여 +425줄로 줄였다. 같은 일을 하면 같은 방식으로 줄인다.
- 상자 그림: seedream-4-5 3×2 격자 1장(7) — 6번이 평범한 나무 상자로 나와 왕실만 1장 더(7). 합계 14 크레딧 · 잔액 204 → 190.
- 장비 칸 '분류 Lv100'은 한국어는 ◆와 겹치지 않고, 영어 'Common Lv100'은 자동 축소로 들어간다(화면 확인).

## 후속 작업 / 주의사항
- 보스 상자 100075(돌문 수호자 상자)는 아이콘이 없어 '?'로 보인다 — 이번 범위 밖.
- 장착 화면의 '레벨 모자람 잠금 표시'(#146 두 번째 항목)는 아직 안 했다.
- 상자 결과 창을 자원·캐릭터·장비로 묶어 보여 주는 것은 아직 안 했다. 패킷(`GachaRewardInfo.RewardType`)은 이미 종류를 준다. 상자에서 캐릭터가 나오는 데이터는 T-209(진우) 대기.
- 로컬 DB는 서버 재시작 때 0017 마이그레이션이 적용됐다(v16 백업 `game.sqlite3.v16-20261122-131252.bak`).

## 추가 (같은 날) — 감정 결과 [1개 더 감정] · 소모품 창 모양 · 왕실 상자 방향
- `GachaResultPresenter` — 감정 무대가 끝 모습에 머물면 아래에 [1개 더 감정 (비용G)] · [결과 닫기] 줄이 뜬다. 창의 버튼 줄을 복제해 `group` 아래에 둔다(감정 중엔 창이 투명).
- `InventoryGridPresenter.AppraiseAgain` — 마지막으로 연 격자(`s_lastOpener`)에 1개 감정을 다시 맡긴다. 확인은 다시 묻지 않고 보유·골드만 미리 본다.
- 문구 `UI.Gacha.AppraiseAgain` 추가(`Loc_UI.xlsx` → 파이프라인).
- `ItemUsePresenter` — 인벤토리 열과 같은 모양: 종이 바탕 · 머리줄(아이콘 + 제목, 인벤토리 제목줄 복제) · 효과 칸(탭 줄과 같은 테두리 칸) · 대상 목록 테두리 · 고르는 말 왼쪽 정렬. `itemIcon` 필드 추가.
- 씬 저장 diff는 앞의 방식대로 소모품 창 트리 · 레벨 버튼 관련 문서만 남겼다.
- 왕실 상자(100086) 그림을 좌우 반전 — 다른 상자들과 같은 방향.
- #117에 장비 레벨 구간(T-209) 재촉 코멘트.

## 추가 2 (같은 날) — 감정 버튼 두 줄 · 소모품 창 아이콘·▲▼ · 경매 아이콘 · 길드 상점·뽑기 삭제 · 이슈 3개
- `UI.Gacha.AppraiseAgain` → `1개 더 감정<br><size=80%>{0} 골드</size>` · 감정 버튼 줄 높이 72.
- `ItemUsePresenter` — 대상 줄 왼쪽에 `ItemIconView`(캐릭터 초상 · 슬롯은 앉은 캐릭터, 쉬는 슬롯은 끔) · 줄 높이 52 · 개수 ▲▼ 버튼(`StepAmount`, 1~상한).
- `ItemIconView.FitIconInside` — 그림 여백을 칸의 9%에서 **테 두께(9-slice 테두리 ÷ 픽셀 배율 = 8)** 로. 경매장 48 칸에서 그림이 테 안쪽 선을 덮던 원인.
- 길드 열: 상점·뽑기 탭 삭제(`GachaPresenter`·`GachaPoolCardView`·`GachaPoolSummary`·`ShopPresenter`·카드 프리팹) · `GuildTab` = Warehouse 하나(값 0) · 창고 버튼 맨 왼쪽 + 빈 비활성 버튼 2개. `GachaResultPresenter`의 뽑기 의존(`gacha` 필드·다시 뽑기) 제거 — 버튼 줄은 감정 견본으로만.
- 씬 diff 정리 후 `ImportAsset` + `OpenScene`으로 바로 다시 열어 "외부 변경 다시 로드" 창이 뜨지 않게 했다.
- 일감 번호: 스팀 파트너 일감 T-208(중복) → **T-210**. 새 일감 T-211(스팀 클라 연동) · T-212(슬롯 소모품 자동 사용 확대) · T-213(개발 편의 치트).
- 이슈 #152(스팀 환경 — 서버 확인) · #153(나침반·지도 자동 사용 — 기획·서버) · #154(치트 명령 3개 — 서버) · #146 진행 코멘트.

## 추가 3 (2026-11-23) — 감정 건너뛰기 소리 · 수량 꾹 누르기 · 비약 미리보기/결과 알림 · 빈 탭 '-'
- `RelicAppraiseFx` — 공개 소리(띠링·팡파르)를 `PlayRevealSound`로 모으고, 건너뛰면 `ShowEnd`가 대신 울린다. 소리는 `PlayOneShot`이라 연달아 감정하면 겹친다.
- `HoldRepeatButton`(UI/Shared 새 파일) — 누른 순간 1번 → 0.35초 뒤 반복, 간격 0.15초에서 ×0.85씩 줄어 0.02초에서 유지. 소모품 창 ▲▼에 붙였다(필드 형이 `Button` → `HoldRepeatButton`이라 씬 다시 배선).
- `ItemUseLabel.ProjectExp` · `ExpPercent` · `LevelWithPercent` — 비약 먹인 뒤 레벨·% 계산(만렙 넘는 몫은 버림 — 서버와 같다). 사용 창 `Preview Text`에 '이름 LvA (x%) → LvB (y%)', 성공 알림은 '~에게 ~ n개를 사용해 LvB (y%)가 되었습니다'(응답 전에 캐릭터 갱신이 와 있어 지금 값이 결과).
- 문구 4줄(`UI.ItemUse.ExpPreview` · `LevelPercent` · `LevelMax` · `UsedExp`) 추가.
- 길드 빈 탭 버튼 글씨 '-'.
- ⚠️ 씬 정리 파일을 덮어쓴 뒤 다시 열기보다 **리컴파일을 먼저 돌려** "외부에서 수정" 창이 떴다. Win32 `BM_CLICK`으로 [다시 로드]를 눌러 넘겼다 — 순서는 덮어쓰기 → `ImportAsset`+`OpenScene` → 리컴파일.
- T-211에 키 확인 방법 3단계 · 로그인 화면 UI 항목 추가.

## 추가 4 (2026-11-23) — 감정 무대 칸 글자 · 효과음 6종 · 일감 정리
- `SlotView.ScaleLabelsWithSlot` — 켜면 아래 줄 글자 크기를 칸 자신의 확대(localScale)를 빼고 캔버스 배율로만 잰다. 감정 무대는 칸을 2배로 키워서 글자가 12px 그대로 작게 보였다 → 이제 24px.
- `RelicAppraiseFx.DecorateRelic` · `DecorateReward` — 무대 칸을 그린 직후 `GachaResultPresenter.Decorate`가 인벤토리 칸과 같은 아래 줄(분류 배지 · 'N 개')을 입힌다. 전에는 숫자만 '20'으로 떠 인벤토리와 달랐다. 이름 줄은 무대라 그대로 둔다.
- 효과음 6종 합성(`make_sfx.py` · 크레딧 0) — `item_use`(비약·영약) · `equip`(장착·해제) · `slot_unlock` · `auction_register` · `auction_cancel` · `auction_buy`. 특성 배우기는 `level_up`을 다시 쓴다. 임포트는 `relic_reveal`과 같게, 목록은 **아트/목록만 다시 쓰기**.
- 소리를 안 넣은 것 — 잠금 · 인벤토리 합치기/쌓기(버튼 소리로 충분) · 우편 수령(결과 창이 `reward_chime`).
- 일감: T-191 · T-178 · T-189 보관(사용자 완료) · T-205 보류(건물 5개 확정 대기) · T-174 표를 지금 데이터로 다시 셈(빈 자리 5칸 · 그림-산업 어긋남 3).
