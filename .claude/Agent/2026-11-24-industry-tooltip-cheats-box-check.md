---
date: 2026-11-24
title: 산업 설명 툴팁 · 서버 치트 3종 연결 · 테마 상자 분포 확인
tags: [client, ui, editor]
---

# 산업 설명 툴팁 · 서버 치트 3종 연결 · 테마 상자 분포 확인

## 목적 / 배경
- #135 — PR #145로 `IndustryTable.Tooltip`이 들어와 작업슬롯 산업 버튼에 설명을 띄운다(T-150).
- #154 — 서버 PR #159의 `SimulateBoxOpen`·`SetCharacterLevel`·`ClearItems`를 치트 창에 붙여 T-213 5종을 끝낸다.
- #117 — PR #156의 테마 상자 장비 레벨·등급을 미리 세기로 확인한다.

## 변경 내용
- `WorkStationSelectPresenter.BindIndustryButtons` — 버튼마다 `TooltipTrigger`(없으면 실행 중 `AddComponent`) · `BuildIndustryTooltip` = 산업 이름 / `Industry.<산업>.Tooltip`
- 치트 — `BoxSimulateCheatSection`(새) · `ClearItemsCheatSection`(새) · `CharacterExpCheatSection`에 레벨 줄 · `CheatSender.Responded` 이벤트(응답 `Message`를 칸이 읽는다) · 로그 한 줄 120자 자르기 → `cheat-console 규칙.md`

## 주요 결정 / 근거
- 툴팁 트리거를 프리팹에 넣지 않고 실행 중에 붙였다 — 씬을 에디터로 저장하면 레이아웃 값이 1,800줄씩 바뀌는 문제(11-24 앞 작업)를 피하려고. `EquipPickRowView`의 `CanvasGroup`과 같은 방식.
- 번역 키는 표 행을 찾지 않고 `"Industry." + EIndustryType + ".Tooltip"`으로 만든다 — 생성 코드의 `TooltipLocKey`와 같은 규칙이고 enum 이름이 같다.
- 인벤토리 비우기는 되돌릴 수 없어 확인 창을 둔다.

## 실측
- 로컬 서버(병합 코드로 재시작) · qa_claude — 툴팁 내용 5종 · 트리거 5개 · 미리 세기(개척자 2,000회 56종) · 레벨 일괄 42명 Lv30 · 자원 비우기 2칸(장착 장비 남음).
- 테마 상자 6종 × 1만 회 → #117: 레벨 70/30은 맞다. 높은 상자일수록 영웅 이상이 줄고(개척자 102 → 왕실 4) · 성주 골드 > 왕실 · 캐릭터는 안 나온다 → 진우 확인으로 넘김.

## 후속 작업 / 주의사항
- 가짜 `pointerEnter`로는 툴팁이 뜨지 않았다 — 실제 마우스로 사용자 확인 필요(#135 · #154는 확인 뒤 클라가 닫는다).
- T-215(서버 치트)가 PR로 들어왔는데 INDEX에 없어 한 줄 넣었다.

## 업데이트 (2026-11-24 밤) — 산업 문구 다시 쓰기 · 치트 버튼 · 도감 얻는 곳
- 사용자 지적: 산업 툴팁이 두루뭉술하다. 원인은 문구가 **아직 없는 기능**(농사 새참 · 채굴 설비 · 벌목 건축 개량)을 설명했기 때문이다. 5산업 수치·분포는 지금 전부 같고 차이는 자원과 쓰는 곳뿐이다.
  → `IndustryTable.Tooltip`(KR) · `Loc_Industry`(EN)를 "어디서 무엇을 얻는다"로 고쳐 쓰고, 툴팁에 **업그레이드 재료**(건물 비용표에서 계산)와 **확률 보상**(단계별 상자 / 미감정 유물) 줄을 더했다. `Loc_UI` 4행 추가.
- 치트: `CheatGui.ActionButton`이 글자 폭만큼 넓어진다(잘림 해결 — '전 종류 1장씩'). 인벤토리 비우기는 빨간 `DangerButton`을 고르는 줄 아래 오른쪽에 두고, 확인 창은 `delayCall`로 OnGUI 밖에서 띄운다.
- 도감 상세 '얻는 곳'(T-150 후속) — `ArtifactSources` · 효과 칸 실행 중 복제 · 상세 칸 높이 고정 해제 → `Artifact 규칙.md`.
- ⚠️ 한 번 플레이에 들어갔을 때 화면에 다른 계정(로컬 서버 기록 없음)이 보여 바로 껐다. 원인은 확인하지 못했다.
- ⚠️ `InternalEditorUtility.ReadScreenPixel`로 에디터 창을 찍으면 Unity가 뒤에 있을 때 다른 프로그램 화면이 찍힌다 — 찍은 파일은 지웠다. 에디터 창 확인은 사람에게 맡긴다.

## 업데이트 (2026-11-25 새벽) — 폴리싱 4건
- 발굴 툴팁이 버튼을 덮음 — 툴팁이 좌우 어디에도 안 들어가면 `Place`가 창 안으로 밀어 대상을 덮었다. 양쪽 다 안 되면 대상 아래(모자라면 위)로. 산업 설명은 문장마다 한 줄(`SplitSentences`)로 나눠 폭도 줄였다 — 실측: 발굴 버튼 x 488~595 · 툴팁 x 601~.
- 레벨 버튼 글자 — 씬 `Industry Level Panel` 74 → 92 · 라벨 최대 15 → 18 · 줄 간격 -16 → -8 · `GetIndustryLevelButtonText`(Lv 줄 75% · 이름을 한글 3자 / 영어 9자 단위로 줄바꿈). 실측 최소 글자: 한국어 16.6(왕실 보석 광산만 12.1) · 영어 9 → 14.4~16.6.
  영어 이름이 3~4낱말·10자 넘는 낱말이라 폭에 안 들어갔다 → `Loc_IndustryLevel` EN 22개를 두 낱말로 줄였다(Old Battleground → Battle Site 등 · KR 원문은 그대로).
- 도감 '얻는 곳'에서 장인의 나무 자만 6곳인 이유 — 표시가 아니라 데이터: T-208 테마 상자 전환 때 나무 상자 아티팩트 행만 6개 상자로 복사되고 은 상자 아티팩트(No.25 은빛 수확패)는 얻는 곳이 사라졌다 → #117에 진우 확인 요청.
- 로그인 상자 페이드 인 — `LoginPresenter.Awake`에서 숨기고 모드 결정 → `FadeInPanel`. 시작 직후 캡처가 느려 페이드 자체는 눈으로 못 봤고, 스팀 모드에서 Id 칸 없이 뜨는 것만 확인.
- 앞서 본 `두닥닥두두` 계정은 스팀 모드로 플레이가 시작돼 [접속하기]로 들어간 사용자 스팀 계정이었다(원격 아님).
