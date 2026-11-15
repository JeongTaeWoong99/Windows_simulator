---
date: 2026-11-15
title: 응축·큐브 창 가운데 팝업 · 글자 넘침 규칙 전 화면 · 도움말 아이콘 · 로그인 무응답 오알림
tags: [client, ui, art, editor]
---

# 응축·큐브 팝업 · 글자 넘침 · 도움말 아이콘 · 무응답 오알림 (T-201)

## 목적 / 배경
- 사용자(태웅): 응축·강화 화면이 좁고 글자가 잘린다 — 가운데 팝업으로. 재료는 팝업이 인벤토리를 막으니 팝업 안에서 고른다.
- 전 화면 점검 → 한 번에 정리(T-201 점검표). 파란 '?'를 양피지 톤으로. 로그인이 성공하는데 "'로그인' 응답이 5초 안에…" 알림이 뜬다.

## 변경
- **응축 창** `!Overlay Canvas` 가운데 팝업(`Overlay` 0.75 · `Window` 640×760 · `BackdropClose`). 같은 종류 캐릭터를 `Material Panel`(ScrollRect + Grid 100) 안 `SlotView`로 늘어놓고 클릭은 격자 좌클릭과 같은 `OnCharacterClicked`.
  진행 막대 바탕 `Slot` → `Border`(판과 같은 색이라 안 보였다) · 더할 몫 `HighlightDark` → `Highlight` · [다음 단계까지 채우기] 자동 축소.
- **큐브 창** 같은 모양(640×540) · 바탕 클릭 = `TryClose`(고르기 전이면 막힌다) · 장비 설명 줄·큐브 버튼 자동 축소.
- **`Josa`**(Data) — `Lang.Format`이 `이(가)`·`은(는)`·`을(를)`·`과(와)`·`(으)로`를 앞 글자 받침으로 고른다. 서식 태그 뒤(`<color>고급</color>(으)로`)도.
- **글자 넘침 규칙** `UI 규칙.md` §3 — 한 줄 + 자동 축소(최대 = 지금 · 최소 0.7, 10 아래 금지) · 줄바꿈은 세로 넉넉한 칸만. 검사기 자동 축소 글자 허용치 3px(끝 획 삐져나옴).
  판매 줄 이름 `preferredWidth` 0 · 개수 오른쪽 정렬. 12곳 검사 0건.
- **도움말 아이콘** `Assets/Sprites/ui/ui_help.png` — `make_icons.py` `ui_help`(양피지 동전 + 진갈 '?', 16px). `HelpIcon` 프리팹 + 프리팹 아닌 씬 아이콘 2개 · 역할 태그 제거 · 글자 '?' 끔.
- **무응답 오알림** `ServerWaitManager.WatchTimeoutAsync` — 5초 지연 뒤 `PostLateUpdate`까지 기다리고 닫혔으면 끝. 
- **알림 창** 판 460×290 · TMP `Use Modern Hangul Line Breaking Rules` 켬(낱자 줄바꿈 → 띄어쓰기 단위).

## 결정 근거
- 무응답 오알림 원인 재현: 응답 처리 job을 `NetworkMessageQueue`에 넣고 메인 스레드를 6초 재움 → 알림이 뜸. UniTask 지연은 Update **맨 앞**(주입된 루프)에서 끝나고 큐 비우기는 `NetworkManager.Update`(스크립트 Update)라, 긴 멈춤 뒤 첫 프레임에서 판정이 먼저 돈다. 수정 뒤 같은 시험 무알림 · 진짜 무응답은 그대로 알림.
  에디터 플레이 시작 직후나 eval 호출 중처럼 메인 스레드가 길게 멈출 때 났다 — 서버 응답 자체는 빨랐다(로그상 세션마다 즉시 Ok).
- 씬 저장마다 꺼진 칸·프리팹 인스턴스의 레이아웃 값(앵커·크기)이 0 ↔ 실제 값으로 오간다 — scratchpad `scenefix.py`(문서 단위) + `modfix.py`(인스턴스 덮어쓰기의 Rect 값)로 되돌려 의도한 변경만 커밋했다. 디스크 씬을 고치면 에디터에 "외부에서 수정됨" 대화상자가 떠 메인 스레드가 멈춘다 → [다시 로드].

## 남은 것
- 영약 자동 사용 ON/OFF — 서버 #123(진우) 대기.
- 점검표 선택 항목: 경매장·아티팩트·거점 진홍 제목 띠 · 팝업 밝은 판 위 옅은 글씨 · 산업 레벨 버튼 작은 글씨 · 큐브 규칙 줄 '일반' 회색.
- `make_icons.py` 변경은 그림 저장소(`Assets/Art`) 쪽 — 이 저장소 커밋에 없다.
- 사용자 실측.

## 이어서 — 사용자 피드백 반영 (`86cf5367`)

- '?' 아이콘: 양피지 동전이 어색하다는 피드백 → 바탕 없는 금빛 물음표(`make_icons.py ui_help` · 그림 저장소 쪽 스크립트는 미커밋).
- 응축 창: 위쪽이 비어 보인다 → 머리 줄 48→88(상반신을 `Portrait Frame`(Border) 액자에 넣음 · 이름 30 · ★ 26), 머리 줄 `childForceExpandHeight` 끔(닫기 48 유지), 비교 상자 `Border` 바탕, 단계 막대 28, 창 760→680.
- 큐브 창: BEFORE/AFTER 상자가 바깥 패널과 같은 `Slot`색이라 구분이 없었다 → 두 상자 `Border` + `ui_input` 슬라이스. 가운데 Spacer(flex 1)를 끄고 비교 줄이 남은 세로를 채움(최소 132). 머리 아이콘 64(안쪽 그림은 늘이기 앵커라 sizeDelta -8). 창 540→500.
- 씬 저장 잡음 245곳은 두 팝업 하위 fileID만 남기고 HEAD로 되돌렸다(scenefix). 검사기 0건.

## 이어서 — 피드백 2차 (`86cf5367`)

- '?' 아이콘: 금빛 고리 + 어두운 원 + 22px 금 '?'. 가챠 카드 도움말에 파란 틴트(0.29,0.525,0.784)가 남아 '?'가 초록으로 보였다 → 흰색.
- 팝업 딤 0.75 → 0.985(11곳 · 경매·아티팩트·거점 포함). 프로젝트가 Linear 색공간이라 알파 0.75로는 배경이 ~53% 밝기로 남았다 — 실측 203→25.
- **`CelebrationFx`**(UI/Shared) — `On(rect).Play(color, power, speed)`. 빛살·고리·번짐·별 조각 모두 FxSprites PNG(코드로 그리지 않음). 응축 성공 0.35 · ★ 상승 1 + ★ 줄 터짐 · 큐브 등급 상승 0.35+(등급−고급)·0.17.
- 강화 자동 패널: 묶음 칩 글자 "경험치×2"/"전체"(2026-10-10 '/' 칩 결정을 뒤집음 — 규칙 줄은 '/' 그대로). 칩 21개 자동 축소(최소 10). 규칙 줄 = 어두운 Border 우물.
- 슬롯 설정 효율 줄: `EfficiencyRowView.SetTooltip` + 줄마다 툴팁(`UI.WorkStation.Tip.*` 5행 — Loc_UI KR/EN 추가 · 파이프라인 실행).
- 위젯 위 여백 5(위 5 · 아래 3 불일치 정리).

## 이어서 — 시안 (2026-11-15 · 코드 변경 없음)

- 바탕화면 `UI_샘플_등급테두리_칸배치.html` — 등급 테두리 A(지금)~G 7종 × 6등급, 캐릭터 칸 배치 지금/시안 1/시안 2. 실제 그림(rarity_bg · 상반신 · 산업 아이콘 · 자물쇠)으로 PIL 렌더(크레딧 0). 생성 스크립트는 scratchpad `mock/make_mock.py`.
- 어두운 속 스타일(B~G)에서 검은 기사처럼 어두운 그림은 묻힌다 — 고를 때 판단 거리.
- 열 오른쪽 1~2px 틈(633.33 폭 · 0.5 ppu 9-slice 반올림)은 pixelPerfect/정수 폭 시험을 사용자 결정 뒤에.

## 이어서 — 시안 2 적용 (`4219a30f`)

- 사용자가 시안 2를 골랐다 + "계단 그라데이션이 더 위까지" → `make_slots.py rarity_bg`를 불투명 짙은 속(0.10) + 계단빛 5단(속의 아래 15/24)으로. 9-slice 경계 12 → 4 — 경계 12이던 때는 가운데 8줄만 늘어 큰 칸에서 계단 비율이 바뀌었다. `ArtImportPostprocessor.UiBorders`도 4.
- 곱셈 한 장이라 속이 아주 옅게 등급색을 띤다(초록 칸은 초록빛 검정). 시안의 중성 어둠과 같게 하려면 바탕을 두 장(어두운 판 + 색 입힌 테)으로 나눠야 하는데, 부르는 쪽 6곳이 `background.color`를 직접 쓰고 있어 한 장으로 두었다.
- `SlotView`: `bestAptitudeChip`(산업 그림 + 숫자) · `SetBestAptitude` · 이름을 끈 칸은 그림 inset 6. 경험치 게이지를 LV 알약 바닥 가로 막대로 옮겼다(세로 막대는 꽉 찬 그림을 가린다).
- 툴팁: `TooltipContent.Grade(rarity)`(등급 줄 7곳을 이걸로) → `TooltipPresenter.PaintRim`이 판 가장자리 4px 등급색 테.
- 상태줄·위젯: 캔버스에 `Ui Frame`·`Ui Paper`가 없어(몸통 `Body Panel`이 없는 띠 열이라 `UiSkin` 규칙에 안 걸렸다) 옆 열과 밝기 차가 어긋나 보였다 → `UiSkin.ApplyAll`에 규칙 추가 + 씬에 구움. 여백 좌우 10 · 위아래 8, 위젯 버튼 (-10, 8)·(-54, 8).
- 씬 잡음은 scenefix로 되돌렸다. ⚠️ `scenefix.py --report`는 파일을 쓰지 않는다 — 예전 출력 파일을 그대로 복사해 씬이 HEAD로 돌아갔다가, 에디터 메모리의 씬을 다시 저장해 되살렸다.
- 효과음: `Art/_source/audio/claude-sfx/make_sfx.py`(numpy 합성 · 크레딧 0) — 지금 소리 셋의 후보 8개 + 새 사건 8개. 바탕화면 `효과음_시안/효과음_시안.html`로 들어 보게 했다. 사각파는 최고값만 맞추면 지금 소리보다 서너 배 커서 평균 세기(RMS 0.09)로 맞춘다. 게임에는 아직 넣지 않았다.
- 검사기 0건(장비 탭).

## 이어서 — 효과음 설치 · 분류 필터 · 아티팩트 팝업 (`72bc2fd6` · `72bc2fd6`)

- 효과음: `make_sfx.py`에 `coin_c` + RMS 0.09 맞춤. `Art/audio`에 ui 4 · sfx 10(coin_gain_1~3). `Sounds.Pick(key, n)` — 있는 벌만 돌린다. 검사기(`ArtValidator.CheckSounds`)는 프로퍼티 값을 보므로 Pick도 통과한다.
- 분류 필터: `InventoryItemGroup` enum을 지우고 `InventoryFilter.Group`을 분류 ID 문자열로. 공급자가 `FilterGroups`·`GroupLabel`·`MatchesGroup`을 답한다. 자원 = `ResourceSlotSource.GroupOrder`([제외]와 같은 표 — 기준이 둘이면 "유물은 어디?"가 된다), 장비 = 장비·아티팩트. 맞는 칸이 앞, 나머지는 흐리게(기존 찾기 방식).
- 아티팩트 창: 판매 목록 자리 → `!Overlay Canvas` 가운데 팝업(응축 창의 딤을 CopySerialized · 원래 바탕은 새 `Window` 640×280으로). `LayoutElement`·`ContentFloorLayoutElement`는 뗐다. 판매 목록이 물러나는 동작은 응축·큐브와 같게 유지.
- 도감 창: `Codex Presenter` VLG 간격 0 → 8 · `Right Panel` 위 여백 = Filter Row 높이 + 8 = 48.
- 어두운 글씨: 팔레트 휘도 비교 스캔은 버튼 그림 위 틴트를 몰라 오탐이 많다 — 실제 캡처로 확인했다.
- 원인 조사(코드 변경 없음): 캐릭터 2장 = 10-02 보유분 + 치트(`CheatGiveAllCharacters`는 종류마다 1장). 경매 더미 = `Kind=Item`만 만든다(`AuctionDummy.Build`) → 분류=아티팩트로는 0건. 경매 DB 1050건 state 1 확인.
- 씬 잡음 246건 scenefix로 되돌림(유지 49 · 새 5).

## 이어서 — 효과음 2차 · 글씨 대비 전수 (`f0e44ada` · `f0e44ada`)

- 효과음: 사용자 "뾱뾱 · 아케이드 · 하이톤" → `make_sfx.py`를 하프(Karplus-Strong)·종·양피지 바스락·나무·북으로 다시 짰다. 잡음은 두 번 거른다(`soft`) — 한 번만 거르면 중심 주파수가 3~7kHz로 '쉬' 소리가 남았다. 지금 중심 130~1100Hz. 버튼·보상은 그림 저장소 HEAD의 예전 파일로 되돌렸다. 시안은 바탕화면 `효과음_시안2/`.
- 큐브: 자동은 `CubeRollAuto`(0.08초 · 크기 0.35). 뽑기: `GachaCharge`(영웅 0.45 · 전설↑ 0.7초 — `RewardRevealSettings` 값) · `GachaBurst`(영웅 0.9 · 전설 1.4 · 신화 2.2초).
- 대비 검사: scratchpad `cscan.sh` — 캡처 + 글씨 자리(textBounds → 화면 좌표)로 바탕 중앙값·글씨색을 재 WCAG 비를 낸다. 작은 글씨는 안티에일리어싱 때문에 실제보다 낮게 나온다(진갈 정상 글씨가 2.5 근처) — 2.0 아래만 본다. 덮인 뒤 화면 글씨는 경로 필터로 거른다.
- 원인: 경매 표 줄은 `Slot` 35% 반투명이라 실제로는 밝은 줄인데 글씨가 `TextMain`(밝은 색)이었다. 등급색 9D 회색·1EFF00 초록은 양피지 위에서 안 읽힌다 → `RarityPalette.OnLight`. 응축 경고 빨강(팔레트 negative)은 갈색 위에서 묻혔다 → `OfFor`가 밝은 짝으로.
- 진갈 글씨 0.85 → 1: 씬 80 · `GachaPoolCardView` 3. 0.62 → 0.85(6). 라벨 리치 텍스트 72 → 82%.
- 원인 조사(코드 변경 없음): 경매 더미는 자원만 → 이슈 #124(진우)로 전 종류 요청.
- 그림 저장소(`Assets/Art`)는 커밋하지 않았다 — 소리 파일 · `make_sfx.py` 등.

## 이어서 — 팝업 소리 삭제 · 경매 확인 팝업 표 · 판매 등록 정리 · 상단 칸 바탕 끔 · 도감 띠링

- 팝업 열기·닫기 소리: 사용자 "버튼 소리와 같이 나서 이상하다" → `PanelTransition`·응축·큐브·아티팩트 창의 `PopupOpen/Close` 호출과 `Sounds` 항목, `Art/audio/ui/popup_*.wav`, `make_sfx.py` 항목을 지웠다. 여닫는 버튼의 `UiClick`만 남는다. 차단막으로 닫을 때는 소리가 없다.
- 도감 새 등록: `Sounds.CodexRegister`(`codex_register.wav` · 미→시 종 두 음 + 하프 · 1.1초) — `ArtifactCodexPresenter`가 칸을 튕길 때 한 번.
- 확인 팝업: `ConfirmPresenter`에 `Detail Box`(Border 0.92 · 글씨 19 TextMain) + `AskConfirm(…, detail)`. 판 560 폭 · `ContentSizeFitter`로 높이를 내용에 맞춤 · 문구 자동 축소 끔(22). 기존 호출(해금 등)은 상자가 숨고 문구만.
- 표 한 줄 `UIRichText.Row` = 라벨 + `<line-height=0>` 줄바꿈 + `<align=right>`값 — 1em이면 120% 키운 총액이 위 줄과 겹쳐 1.6em.
- 경매 구매: 이름(등급색 OnLight) ×개수만 크게, 상자에 단가 · 총액 / 시세 두 줄 / 보유 골드 · 구매 후. 금액은 노랑(`GoldOnDark`). `AuctionPriceText`에 `onDark`·`twoLines`.
- 판매 등록: 금액 표를 `Summary Box`(어두운 상자)로 · 확인 팝업도 같은 표(`BuildMoneyTable`). 수량/단가 줄을 라벨 70 · 입력 묶음 256 · 오른쪽 버튼 140(글씨 18 자동 축소)으로 세로 맞춤 · 도움말 15(입력 아래로 78 들여쓰기). 고르는 목록 [선택]은 양피지 버튼 위 밝은 글씨라 안 읽혔다 → `AuctionRowView`가 고르는 목록이면 글씨를 TextDark로. 줄의 '경매 단가 n G 이상'은 진갈 칸 위 HighlightDark → Highlight.
- 상단: `Nick Panel`·`Gold Panel`·위젯 `Top Panel`의 Image `enabled=false` + `Ui Trim` 끔. 지우면 `UiSkin.ApplyAll`이 테를 다시 만든다.
- 커밋 정리: 사용자 "오늘 커밋이 너무 많다 — 8개 이하로" → 오늘 커밋 25개를 주제별 7개로 묶었다(푸시 전이라 기록만 바뀐다). 그림 저장소는 오늘 커밋이 없어 한 개로 넣었다.

## 이어서 — 아이콘 삐져나옴 · 시세 탭 · 판매 편의 · 성장 개체 판매 확인 · 슬롯 확장 이슈

- 아이콘: `ItemIconView`의 `Icon Image`가 칸에서 4px 안쪽 고정이라 92px 칸에서 등급 테(약 7%) 위로 그림이 덮였다(하트·뽑기권처럼 끝까지 그린 그림). → `Awake`에서 앵커 9% 안쪽 비율 + 칸에 `RectMask2D`. 경매 줄·판매 등록·시세·아티팩트 팝업·도감 상세가 한 번에 맞는다. 프리팹·씬 값은 실행 때 덮인다.
- 정사각형: 판매 등록 줄 일부가 89~91×92 — 옆 글씨가 길면 `HorizontalLayoutGroup`이 최소 폭(-1)까지 깎았다. `SquareLayoutElement.minWidth`도 한 변으로(Common — 마스터 반영은 `/unity-skill-sync`로 확인 필요).
- 판매 등록: 종류 드롭다운 150 + 글씨 자동 축소('아티팩트'가 '아…'로 잘렸다). 고르는 줄은 아무 데나 눌러도 고른다(`AuctionRowView.OnPointerClick` — 구매·취소 줄은 버튼만).
- 시세 탭: 요약 칸·단가별 수량은 어두운 칸 위인데 금액이 진한 금(HighlightDark)이었다 → 밝은 금. 라벨 17 · 값 30(자동 축소) · 축/날짜 14 · 단가별 줄 28 · 머리 버튼 230/150 + 자동 축소(“이 아이템 매물 보기”가 칸을 넘쳤다) · 이름 OnLight.
- 성장 개체 판매 확인: 진홍 버튼 글자(밝은 금 `Highlight`)를 그대로 밝은 팝업에 써서 골드가 안 보였다 → 경고만 문구로, 버튼 글자는 어두운 상세 상자로.
- BOM: 오늘 파이썬 편집(`utf-8-sig`로 쓰기)이 BOM 없던 .cs 27개에 BOM을 붙였다 — 전부 되돌렸다. 앞으로 BOM 없는 파일은 `utf-8`로 쓴다.
- 이슈 #125 [기획·서버 확인] 작업슬롯 8 → 10칸 건의(진우). 클라는 칸 수를 씬의 칸 개수로 그린다(`slotParent.childCount`) — 확정되면 칸 2개를 씬에 더한다.

## 이어서 — 머지 정리 · 시세 그래프 툴팁 · 검색 후보 클릭

- 머지: 원격 `b4e66b9c`는 로컬 `9acfa134`의 이전판(푸시된 1차)이라 로컬이 상위집합이었다 → 충돌 4건을 로컬 쪽으로, 원격 위에 한 커밋으로 올렸다(머지 커밋 없음).
- 14일 그래프: 막대가 `Accent` 35%라 밝은 판 위에서 회색처럼 흐렸다 → 80% · 선은 `HighlightDark`(같은 하늘색이면 막대 위에 묻힌다). 툴팁은 막대가 아니라 칸 전체 높이 투명 그림(`Awake`가 막대 부모 맨 뒤에 만든다) — 낮은 막대·빈 날도 뜬다. 문구는 기존 키(`PeriodToday`·`UnitPrice`·`PriceRange`·`TradeCount`·`NoTrades`)만 써서 번역 테이블은 안 건드렸다.
- 검색 후보 클릭이 자주 안 먹힘 — 원인: 후보 줄 버튼엔 `IDragHandler`가 없어 드래그 대상이 **부모 입력칸**으로 잡혔다(`GetEventHandler<IDragHandler>` 실측). 누른 채 10px(`pixelDragThreshold`) 움직이면 입력칸이 드래그를 시작하고, 누른 대상 ≠ 드래그 대상이라 `InputSystemUIInputModule`이 클릭을 취소한다. → `SuggestView`가 드래그를 받고(`OnEndDrag`에서 놓은 줄을 고른다). 폭은 `sizeDelta.x` 120 → 0(3곳), 이름 색 `RarityPalette.OnLight`.
- 씬 잡음: `scenefix.py` — ID는 `GlobalObjectId.targetObjectId`로 뽑는다(`TryGetGUIDAndLocalFileIdentifier`는 씬 오브젝트에 0을 줘 전부 되돌렸다).
- 툴킷 마스터: task-writer(`art/`·`later/` · 완료 때 공돌리기) · task-reader(INDEX 표 셋) · github-issue-writer(공돌리기) — `a9b27a6`, 푸시는 사용자.
