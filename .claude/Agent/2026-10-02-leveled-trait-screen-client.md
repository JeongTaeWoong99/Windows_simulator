---
date: 2026-10-02
title: 클라 특성 화면을 레벨형(산업 × 종류 표)으로 · 산업 레벨 잠금 · 산출량 줄 (T-116 · #50)
tags: [client, ui, data]
---

# 클라 특성 화면을 레벨형으로 (T-116 · 이슈 #50)

## 목적 / 배경
- 서버가 특성을 노드 사슬 → 레벨형으로 바꿨다(T-108 · `83f8616`). 클라는 옛 해금 목록으로 읽어 특성이 전부 안 배운 것으로, 상위 산업 레벨이 잠금 없이 보였다.
- 화면은 제안 3안(표 · 종류 탭 · 산업 탭) 중 **A안 산업 × 종류 표**로 사용자가 정했다.

## 변경 내용
- `ServerPacketHandler` — `S_UserTraitListResponse` 핸들러 · 이벤트.
- `PlayerDataModel` — `_traitLevels` · `GetTraitLevel` · `IsIndustryLevelOpen` · `GetTraitEffectSum`(서버 `User.SumTraitEffect` 사본) · `TraitsChanged`. 올리기 응답의 `Level`을 실패여도 반영.
- `GameDataLoader` — `TryGetUserTraitLevel` · `TryGetIndustryUnlockTrait` 추가, `GetIndustryLevelUnlockTid` · `TryGetIndustryLevelByUnlockTid` 삭제.
- `TraitPresenter` 재작성(3열 표 · 구역 탭 삭제 · [레벨 올리기]) · `TraitNodeView`(상태 MaxLevel/Available/Locked · `BindBlank` · 잇는 선 삭제).
- `WorkStationSelectPresenter` — 레벨 잠금 3곳 · 속도 가산 · 효율 계산에 **산출량 줄**(5줄) · 잠김 툴팁 문구.
- 씬: `Trait Tab Panel` 삭제 · 격자 3열(191×44 · 8×6) · 버튼 문구 · 본문 자동 크기 14~22. 프리팹: `Link Image` 삭제 · 글자 14/11.
- `ResultMessages` — `TraitMaxLevel`.

## 주요 결정 / 근거
- 표의 칸은 (산업, `EffectType`)로 찾는다 — TID 대역을 베끼지 않는다. 없는 칸은 빼지 않고 `BindBlank`(바탕째 끔) — 빼면 열이 어긋나고 회색이면 "잠김"으로 읽힌다.
- 포인트 부족은 색으로 가르지 않는다 — 모든 칸이 회색이 된다.
- 산출량 설명 문구는 사용자 요청 — "좋은 게 나올 확률이 오르나?" 오해 방지. 같은 자원이 더 · 희귀도 그대로 · 상자·경험치 안 늚.
- ⚠️ `→` 글리프가 `neodgm_pro SDF`(Static)에 없다 — "지금 효과" / "다음 레벨" 두 줄로 썼다. 새 문구를 넣을 땐 `HasCharacter`로 확인할 것.

## 후속 작업 / 주의사항
- ⚠️ **에디터가 pull 전 씬을 들고 있다가 저장하면 들어온 씬 변경이 되돌아간다.** 이번에 `79054b2`의 하단 메뉴 "거래"가 "거래소"로 돌아가 있어 되돌렸다. 씬 커밋 전 `m_text` diff를 본다.
- 치트 창 특성 묶음은 T-115(서버 특성 레벨 치트 · #51) 대기.
- 실측 후 #50에 처리 코멘트.

## 2차 — A안 목업대로 다시 짓기 (같은 날)
- **원인:** 1차는 `GridLayoutGroup` 3열 칸만 깔아 목업의 **산업 이름 열 · 고정 머리 줄 · 눈금 · 항목/값 정보 줄 · 레벨 사다리**가 전부 빠졌다. 사용자 "A안 HTML 목업이랑 완전 다른데?"
- **구조:** 줄 프리팹 `TraitRowView`(이름 열 64 + `TraitNodeView` ×3, HLG) × 6을 Content(VLG)에 풀로 생성. 머리 줄 `Trait Header Row`는 스크롤 밖 고정(여백 우 26 = 6 + 스크롤바 20).
- **칸:** 종류 이름 · `Lv x/y` · 눈금 10개(최대 레벨만큼 켜고 지금 레벨만큼 채움).
- **정보 영역:** VLG로 이름(+Lv) · 종류 설명 · `EfficiencyRowView` 정보 줄(효과 `지금 ▶ 다음` · 다음 레벨 조건 초록/빨강 · 비용) · `TraitLadderView` 사다리 · 사다리 안내. 버튼 `레벨 올리기 (n점)`.
- 정보 영역 357.5 · 버튼 50은 판매 목록 정렬 규칙이라 유지했다(한 번 300/42로 바꿨다가 되돌림).
- 새 문구 글리프 전부 `neodgm_pro SDF`에 있음 확인(`▶` · `—` 포함).
- RunCommand는 네임스페이스로 감싸져 `Image`가 모호하다 → `using Img = UnityEngine.UI.Image;`.

## 3차 — 정보 영역 개선 (사용자 피드백 · 같은 날)
- 피드백: 효과가 `…`로 잘림 · 비용 표시가 작고 애매 · 스크롤바 불필요. HTML 개선안(`특성화면_개선제안.html`, 저장소 루트 · 커밋 안 함)으로 확인받고 반영.
- 새 View `TraitDetailStatsView` — 지금 ▶ 다음 비교 상자 + 조건 · 비용 칩. `EfficiencyRowView` 재사용을 걷어냈다.
- 사다리: 칸 폭 44 고정 · 왼쪽 정렬, 레벨 숫자 크게 · 계정 Lv 작게, 다음 칸이 막히면 빨강(`Bind(..., nextReachable)`).
  **사다리가 커졌던 원인:** 가로 배치 그룹이 `childForceExpandHeight`로 스스로 flexible로 보고돼 남는 높이를 먹었다 → `flexibleHeight = 0`.
- 표: `ScrollRect` · 스크롤바 제거, `Scroll View Panel` → `Trait Table Panel`.
- [레벨 올리기]: 못 올리면 회색(`PaintConfirm`, interactable은 유지).
- RunCommand로 만든 오브젝트는 `UIThemeColor.Setup`만으로 색이 안 들어간다 — `Apply(Resources.Load<UIThemePalette>(...))`로 구웠다.
- ⚠️ 작업 중 사용자가 플레이 중이었는데 재컴파일해 그 세션이 NRE로 깨졌다 — 재컴파일 전 `editor_status`의 playMode를 본다.

## 4차 — 단순화 · 플레이 실측 (같은 날)
- 피드백: 레벨 사다리·제목 불필요(조건 칩으로 충분) · 큰 '최대 레벨' 상자 보기 싫음 · 표 줄 높이·글씨 키우기.
- `TraitLadderView` 삭제(스크립트·씬 오브젝트). 최대 레벨은 이름 줄 `Lv 5 / 5 · 최대`(노랑) + `현재 효과` 상자 하나.
- 줄 58(60은 표가 8.5px 넘쳐 잘림 — 특성 탭에서 표 영역 383.5) · 칸 이름 18 · Lv 13 · 산업 이름 17 · 머리 줄 15 · 포인트 16.
  정보 영역: 이름 24 · 설명 16 · 비교 상자 68(값 24) · 칩 42(값 19).
- 실측: 플레이 모드에서 로그인 팝업을 잠깐 끄고 `ServerPacketHandler.Handle_S_UserTraitListResponse/AccountLevelResponse`에 가짜 스냅샷을 넣어
  잠김·최대 상태를 `ScreenCapture.CaptureScreenshot`으로 확인(UI 오버레이는 카메라 캡처에 안 찍힌다). 에디터가 포커스를 잃으면 캡처 파일이 안 생긴다 → `editor_focus`.
- 공통 줄 칸 폭 어긋남: 꺼진 바탕 Image가 배치 계산에서 빠져 기본 폭(스프라이트 10)이 사라짐 → 칸 LayoutElement preferredWidth 0 고정. 실측 173/173/183 → 176×3.

## 5차 — 조건 줄 · 포인트 문구 정리 (2026-10-03)
- 피드백: 비교 상자(▶ 28)와 조건 칩(간격 8)의 세로선 어긋남 → 성격이 다른 둘을 맞추지 말고 모양을 나누기로. 목업 5안(`특성정보영역_목업.html`, 커밋 안 함) 중 ③ 채택.
- 조건·비용 칩 삭제 → 버튼 바로 위 구분선 + 글자 한 줄 `조건 계정 LvN · 보유 포인트 n`(Spacer로 버튼 쪽으로 밈). `TraitDetailStatsView`는 비교 상자만.
- 표 위 `Point Text` 삭제(보유 포인트가 조건 줄과 중복). 고른 것 없음·최대 레벨에도 `보유 포인트 n`은 보인다. 생긴 높이로 줄을 다시 60.
- 플레이 캡처로 빈 상태 · 충족(공통 산출량) · 미달(채굴 개척 계정 Lv50) 확인.

## 마무리 (2026-10-03)
- 사용자 실측 완료 → T-116 Closed · archive. 치트 창 특성 묶음은 **T-117**(태웅 · ⏸ T-115/#51)로 분리.
- #50에 처리 코멘트 + 서버 확인으로 넘김. 목업 HTML 2개는 저장소 루트에 남기되 커밋하지 않음.
