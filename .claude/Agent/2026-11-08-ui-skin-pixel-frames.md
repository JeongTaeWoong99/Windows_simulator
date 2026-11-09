---
date: 2026-11-08
title: UI 틀 3종을 Claude가 코드로 그려 입히고, UI 톤을 중세 판타지(나무 갈색·양피지)로 바꾼다
tags: [client, art, ui]
---

# UI 픽셀 틀 (`fx/ui/` · `UiSkin`)

## 목적 / 배경
- 사용자 요청: Claude로 UI를 만드는 스킬을 찾아보고, 게임 컨셉에 맞게 몇 가지 만들어 UI에 적용한다.
- 찾은 스킬 중 크레딧 없이 UI(9-slice)를 만드는 전용 스킬은 없었다(SpriteCook은 외부 크레딧). pixelfx와 같은 방식(코드로 그리고 → 보고 → 고친다)으로 직접 그렸다.
- 기준은 `Art 규칙.md` "UI — 어두운 단색 구조 + 판타지 장식": 바탕색은 그대로, 판타지는 테두리(9-slice)로만.

## 변경 내용
- 그림 저장소: `_source/ui/claude-ui/make_ui.py` → `fx/ui/ui_panel`(20px · 경계 7) · `ui_button`(16 · 5) · `ui_input`(16 · 5). 회색조라 Image 색이 곱해진다.
- `ArtImportPostprocessor`: `ui_` 접두는 점 필터 + 경계(`UiBorders`).
- `FxSprites.UiPanel·UiButton·UiInput`.
- `UiSkin`(신규): 씬이 열릴 때 Sliced Image의 기본 스프라이트 `Background`·`UISprite`·`InputFieldBackground`를 우리 틀로 바꾼다. 픽셀 1 = 캔버스 2(`pixelsPerUnitMultiplier 0.5`).
- `GachaPoolCardView.Awake`: 프리팹으로 만들어지는 상점·뽑기 카드에 `UiSkin.Apply(this)`.
- 문서: `Art 규칙.md`(폴더 표 · 원본 종류 `ui/`), `UI 규칙.md`(스프라이트 절).

## 주요 결정 / 근거
- **씬·프리팹을 고치지 않고 실행 중에 바꾼다.** 메인 저장소 파일이 그림 저장소 GUID를 가리키면 안 된다(`Art 규칙.md`). 그림이 없으면 예전 모양 그대로다.
- 틀은 회색조(밝은 곳 1.0) — 패널·버튼·탭의 색(남색·파랑·주황)과 코드가 바꾸는 색이 그대로 산다. 곱하기라 바탕보다 밝은 선은 못 그어서, 빗면은 그늘·홈(어두운 선)으로 낸다.
- 첫 시안은 너무 옅어(외곽선 0.42 · 바탕 0.93) 판타지가 안 읽혔다 → 외곽선 0.25~0.28 · 안쪽 홈 · 깎은 모서리 · 아래 2줄 그림자로 올렸다.

## 확인
- 플레이 → 로그인 → 화면 캡처로 제목·탭·버튼·드롭다운·입력칸·상점 카드가 바뀐 것을 봤다. 새 오류 없음.

## 후속 작업 / 주의사항
- 실행 중 프리팹으로 만드는 줄·칸(`AuctionRowView`·`MailRowView`·`SellCartRowView`·`CharacterStateRowView`·`TraitNodeView`·`WorkStationSlotView`·`WidgetMiniSlotView` 등)은 아직 기본 모양이다 — 필요하면 그 뷰의 `Awake`에서 `UiSkin.Apply(this)`.
- 귀퉁이 마름모 징은 어두운 화면에서 거의 안 보인다. 장식을 더 키우려면 `make_ui.py`를 고쳐 다시 굽는다.

## 2차 — 남색 다크 → 중세 판타지 톤 (같은 날)
- 사용자: 색감이 마음에 안 든다. 스크롤 같은 연갈색·어두운 갈색으로, 버튼을 예쁘게. 우리는 다크 판타지가 아니라 마을을 발전시키는 중세 판타지다.
- 팔레트(`UIThemePalette`): 바탕 나무 갈색 · 버튼 양피지 · 고른 탭 금빛 · 핵심 행동 붉은 밀랍 · 글씨 크림. `UI 테마/적용`으로 씬·프리팹에 구웠다.
- 양피지 버튼 안 글씨 214개(씬) + 프리팹을 `TextMain → TextDark`로 다시 태그하고 굵게 했다 — 가는 진한 글씨는 밝은 바탕에서 회갈색으로 흐려 보였다(캡처로 비교).
- 예외: 작업슬롯 칸(`WorkSlotFrame`) 큰 글씨는 굵게 하면 칸을 넘어 되돌렸다. 상점·뽑기 카드 가격 버튼은 금·빨간 가격이 양피지에서 안 읽혀 태그를 떼고 어두운 나무색을 직접 넣었다.
- 틀 그림 2차: 바탕을 0.86~0.9로 낮춰 위 빛줄·띠를 밝게(곱하기라 1.0이 최대) · 외곽선 0.2.
- 문서: `Art 규칙.md` 아트 방향(다크 판타지 아님 · UI 색 표), `Shared 규칙.md` 테마 톤.
- ⚠️ 작업 중 `git checkout -- WorkStationSlotView.prefab`으로 그 프리팹의 테마 칠을 실수로 되돌렸다 → 적용 메뉴로 다시 칠했다(다시 태그할 글씨는 없었다).
- 확인: 플레이 → 로그인 → 메인 3열 캡처. 경매장·아티팩트·거점·팝업 화면은 아직 눈으로 안 봤다.

## 3차 — 글씨 흰색 통일 · 놋쇠 장식 덧그림 (같은 날)
- 사용자: 양피지 버튼의 진한 글씨가 안 보인다 → 흰색으로 통일(골드·특수 의미색 제외). UI가 너무 심플하다 → 과하지 않은 판타지 버튼.
- 2차에서 `TextDark`로 바꾼 글씨를 전부 `TextMain`으로 되돌렸다(씬 193 · 프리팹 14). 원래부터 `TextDark`였던 씬 태그 2개(`&816976046` · `&894502282`)와 `HelpIcon`·`SlotView`는 그대로. 굵게는 남겼다.
- 흰 글씨가 읽히게 버튼을 가죽 갈색 `#9C7444`로 어둡게, 선택 `#C98D26`, 핵심 `#B5502F`, 잠김 `#5E5043`. 글씨 `#FAF7F2` · 보조 `#E0D9CC`.
- **틀을 두 겹으로** — 바탕은 흰 판(색 곱하기), 장식 `ui_*_trim`은 색이 든 반투명 덧그림(외곽선 · 빛줄 · 그늘 · 놋쇠 귀퉁이). 곱하기로는 바탕보다 밝은 빛·금속색을 못 그려서다. `UiSkin`이 첫 자식 `Ui Trim`(ignoreLayout · raycast 끔)으로 깐다.
- 자식 순서로 칸을 찾는 코드(`WidgetPositionLayout` 열 · `WorkStationListPresenter`·`InventoryGridPresenter`의 `slotParent`)의 컨테이너는 Image가 없어 장식이 붙지 않음을 확인했다.
- 경매장 표 머리의 정렬 버튼(투명 바탕)에 장식 상자가 생겨, 바탕 알파 0.1 이하면 장식을 깔지 않게 했다.
- 확인: 메인 3열 · 설정 · 경매장 캡처. 새 오류 없음(상점 카드 제목 넘침 경고는 전부터 있던 것 · 스스로 해소).

## 4차 — 화려한 판 (같은 날)
- 사용자: "과하게 해 봐, 너무 밋밋하다".
- `make_ui.py`에 `STYLE` 스위치 — `ornate`(지금): 판 32px(나무 띠 4줄 · 금 안쪽 줄 · 그늘 · 두 겹 외곽선 · 놋쇠 귀퉁이 판 + 징), 버튼 24px(위 절반 윤기 · 아래 두께 · 금 안쪽 줄 · 귀퉁이 판), 입력 20px(안으로 파인 그늘 · 금 줄). `plain`은 3차 그대로 남김.
- 9-slice 경계 판 10 · 버튼 8 · 입력 6 — `ArtImportPostprocessor.UiBorders`도 맞춤.
- 확인: 메인 3열 플레이 캡처. 작은 필터 버튼(높이 ~24)은 귀퉁이 판이 빽빽하다 — 사용자 판단 대기.

## 5차 — 중간 판 (같은 날)
- 사용자: 4차는 너무 과하다 → 3차와 4차의 중간. 모서리 장식 때문에 버튼이 작아 보이고 여백이 생기는 게 싫다.
- `STYLE="medium"` 추가·채택: 모서리 깎기 1칸(투명 귀퉁이 최소) · 귀퉁이 놋쇠 판 없음. 패널 24px(나무 띠 2줄 · 금 줄 · 안쪽 그늘), 버튼 16px(위 윤기 3줄 · 아래 두께 2줄), 입력 16px(파인 그늘 · 금 그늘 외곽선). 경계 7·4·4.
- 사고: 사용자가 플레이 중일 때 확인 없이 재컴파일해 플레이 세션이 깨졌다(NullReference). 플레이를 다시 켜 확인 후 껐다. 재컴파일 직전에 isPlaying을 먼저 봐야 한다.
- 확인: 메인 3열 플레이 캡처.

## 6~7차 — 쇠 액자 → Taskbar Heroes 결 (같은 날)
- 사용자: 중간 판도 밋밋하다 → Taskbar Heroes 느낌으로. 바탕화면 스크린샷 5장을 레퍼런스로 주고, 프로젝트에 복사해 아트 만들 때 참고하게 하라고 함.
- 레퍼런스: `Assets/Art/_source/reference/taskbar-hero/taskbar-hero-1~5.jpg`(그림 저장소). `Art 규칙.md`에 "레퍼런스" 절 + `_source/reference/` 종류 + AI 머리말 참조 추가.
- `make_ui.py` `STYLE="taskbar"`: 창 액자 32px(경계 12 · 돌 띠에 그리스 무늬 8px 한 마디 · 귀퉁이 쇠판 + 구리 마름모) · 버튼 16px(구리 테 2줄 · 홈 · 윤기, 경계 5) · 입력 16px(파인 돌 테, 4) · 제목 띠 `ui_header` 16px(구리 테, 6). `iron`(쇠 테)도 남김.
- 팔레트: 돌회색 바탕 `#2A2827`·`#363331`·칸 `#1C1B1B` · 버튼 `#5A2A1A` · 선택 `#C8641E` · 핵심 `#99231F` · 강조 `#F0A030` · 새 역할 `Header`(4) `#8B1B19`.
- 열 제목 `Title` 3개 · 오버레이 창 `Header Presenter` 3개를 `Header`로, 그 글씨를 `Highlight`(금)로. 열 제목 글씨 가운데 정렬.
- `UiSkin`: `Background`를 역할로 가름(Header→제목 띠 · 그 밖→파인 칸) · 창 액자 `Ui Frame`(마지막 자식 · Tiled)을 `Body Panel`(MAIN VIEW)과 큰 팝업 바탕에 두르고 레이아웃 여백 +24. 첫 시도는 액자를 첫 자식(밑)에 깔아 내용에 가려 안 보였다.
- 여백이 늘어 인벤토리 도구줄 1이 35px 넘침 → 빈칸 `-(Layout)` 최소 20→0 · 등급 일괄 드롭다운 150→120 (씬).
- 사고: 5차 때 사용자 플레이 중 재컴파일로 세션이 깨짐(위). 이후 매번 isPlaying 확인.
- 확인: 메인 3열 · 설정 · 뽑기 · 경매장 · 거점 · 아티팩트 플레이 캡처. 넘침 경고는 상점 카드(전부터 · 스스로 해소)뿐.

## 8차 — 버튼 아이콘 · 스프라이트 아틀라스 · 캡처 공유 (같은 날)
- 아이콘 8종(크레딧 0 · Claude 코드): `Art/_source/ui/claude-ui/make_icons.py` → 메인 `Assets/Sprites/ui/`의 `hud_mail`·`hud_setting`·`hud_exit`·`menu_inventory`(자루)·`menu_guild`(방패)·`menu_auction`(망치)·`menu_artifact`(수정)·`menu_outpost`(집). 16px · 자동 1px 외곽선 · 색 포함. 종료는 처음 빨강이 적갈색 버튼에 묻혀 밝은 쇠색으로.
- 씬: 상태바 버튼 3개는 `Icon` 32px + 글씨 끔, 메뉴 버튼 5개는 위 `Icon` 32px + 글씨를 아래 34px로. 처음 40px로 했다가 2.5배라 픽셀이 고르지 않아 32로.
- 스프라이트 아틀라스 V2 4개(폴더 단위 · 점 필터 · 무압축 · 회전/꽉 채우기 끔 · 여백 4): `Art/atlases/atlas_icons`(292장 → 1024×512) · `atlas_fx_hit`(43 → 512²) · `atlas_ui`(8 → 128×64) · 메인 `Assets/Sprites/atlas_sprites_ui`(19 → 128²). 플레이에서 `packed=True` · Tiled 액자도 그대로 그려짐을 확인. 캐릭터·대상(이미 시트)·배경(큼)·부드러운 fx(선형 필터)는 안 묶음.
- 캡처는 바탕화면 `UI 작업 캡처 2026-11-08/`에 복사(사용자가 팀 공유 후 지운다).

## 9차 — 에디터 미리보기 · 메뉴 버튼 아이콘만 · 툴팁 (같은 날)
- 사용자: 씬(편집 모드)에서도 틀이 보이면 좋겠다 · 메뉴 버튼 5개도 글씨 빼고 올리면 툴팁.
- `UiSkinPreview`(Editor): `UiSkin.Recorder`로 바꾼 것(스프라이트·만든 장식·여백)을 적고 저장·플레이·컴파일·씬 닫기 직전에 되돌림, 직후 다시 입힘. 만든 장식은 `HideAndDontSave`. 저장된 씬에 `ui_*`·`Ui Trim` 0건 확인. `ClearSceneDirtiness`는 공개 API가 아니라 리플렉션.
- 미리보기는 `delayCall`로 입혀서 Unity 창이 뒤에 있으면 다음 프레임까지 늦다(직접 호출로는 장식 376 · 액자 11 확인).
- `TooltipTrigger.label`(숨긴 글씨) 추가 — 메뉴 5 · 상태바 3 버튼에 연결, 메뉴 아이콘 48px(3배) 가운데 · 글씨 끔.
- 사고: 사용자 플레이 중 재컴파일을 또 걸었다 — 이번엔 컴파일 오류로 실패해 리로드가 없어 세션은 무사. 고친 파일은 플레이가 끝난 뒤 넣음.
- 바운스: 코드에 누름·호버 바운스가 없다(DOTween 스케일은 강화·합치기 같은 결과 연출뿐). 드롭다운 차이는 TMP_Dropdown(열 때 0.15초 페이드) vs 제외 `CheckDropdownView`(페이드 없음)뿐 — 사용자에게 어떤 움직임인지 확인 요청.

## 10차 — 눌림 반응 전수 · 드롭다운 목록 크기 · 골드 아이콘 (2026-11-09)
- 9차 바운스 보고는 틀렸다 — 누름 반응 `ButtonPressFx`(Event System에 하나)가 이미 있었고 **`Button`만** 잡아 Unity 드롭다운·체크가 빠졌다. 제외 드롭다운만 튄 이유(머리가 `Button`). → `Button · TMP_Dropdown · Toggle`로 넓힘.
- 드롭다운 전수(씬 TMP 21 + 제외 1): 제외 목록이 머리보다 10px 넓음 · 줄 바탕이 목록 틀을 덮어 테두리가 안 보임 · 체크 표시가 칸 색(`Slot`)이라 안 보임 · 템플릿 높이 150이라 8줄부터 스크롤.
  - `CheckDropdownView.FitList`: 가로 늘이기 + 높이에 틀 여백 포함, 열 때마다 다시 잼. `ListPadding` 4→5(딱 맞으면 스크롤바가 뜸).
  - `UiSkin.InsetList`: 실행 중에 `Viewport`를 4px 들임. TMP는 좌우만 — 위아래까지 들였더니 TMP가 목록 높이를 내용 높이로 줄여 마지막 줄이 잘렸다. 제외 목록은 `AutoHideAndExpandViewport`가 보이는 칸 크기를 덮어써 여백이 사라져 `AutoHide`로.
  - 씬(YAML 직접): 체크 22개 `Slot`→`Highlight` · 템플릿 21개 높이 150→260.
- 골드 아이콘 잘림: 자리표시가 `UISprite` Sliced라 `UiSkin`이 버튼 틀·장식을 입혔고, 동전을 넣어도 Sliced라 비율 맞춤이 안 먹었다. 씬 `Gold Image` Simple + `StatePresenter.ApplyGoldIcon`이 Simple·배율 1로.
- 씬을 Unity로 저장하면 레이아웃 구동값 1,230줄이 같이 바뀌어(그 전부터 묵은 값) 되돌리고 YAML을 직접 고친 뒤 씬을 다시 열었다.
- 확인: 플레이에서 열린 드롭다운 전부 너비=머리 · 내용 ≤ 보이는 칸 · 캡처로 틀·체크·골드 확인.

## 11차 — 틀 굽기 · 액자 틈 · 칸 그림 · 버그 (2026-11-10)
- 사용자: 씬에서 실행 전에 안 보여 불편 — 그림 저장소가 없을 때 흰 상자여도 된다. → 에디터 미리보기(`UiSkinPreview`)를 지우고 **굽기**(`UiSkinBaker`, 메뉴 `UI 테마/스킨 굽기`)로. 프리팹 24개 + 씬. `UiSkin.Recorder` → `UiSkin.Skip`(씬 안 프리팹 인스턴스 거르기). `Art 규칙.md` 참조 규칙을 UI 틀·칸 그림만 예외로.
- 굽고 보니: 3px 선택 테두리 선 · 8px 슬라이더 막대 19개에 버튼 틀이 입혀짐 → 되돌리고 `IsThin`(14px 미만 제외). 길드 창고(창 안 구획)에 액자가 겹침 → `IsInsideColumn`이면 액자 안 두름.
- 액자 틈: 띠(24)에 버튼이 딱 붙어 겹쳐 보임 → `FramePadding` 32(틈 8). 구운 창 11개 여백 +8. 확인·수량 입력 팝업이 내용 선호 높이보다 작아 띠를 덮음 → 280. 로그인 창 200×400 → 300×250. 플레이로 메인·인벤·길드·경매·아티팩트·거점·설정을 돌며 겹침 0(띠 31px 기준) 확인.
- 판매 카트 버튼에 아이콘(`sellIcon` 동전 · `disassembleIcon` 마석 아이템 아이콘, 코드가 넣음).
- 칸 그림(`make_slots.py`, 크레딧 0): `rarity_bg`를 경사 판으로 덮어씀 + `rarity_frame` 덧그림(`SlotView.AddRarityFrame`) · 작업슬롯 `ui_slot_empty`/`ui_slot_locked`(Tiled 빗금) + `ui_icon_plus`/`ui_icon_lock` → 씬 8칸에 `Slot Art`·`State Icon` + `WorkSlotFrameView`. 임포트: `rarity_*` 점 필터 · 경계(`UiBorders`).
- 버그: 아티팩트 메뉴 툴팁이 'Button' — 꺼진 글씨라 `LocalizedText`가 안 돌았다 → `TooltipTrigger`가 키로 읽음. 씬에 남은 낡은 번역 글씨 10개를 표 값으로. 로그인 입력칸 자리표시 'Enter text...'는 키가 없어 엑셀 추가 필요(이름 확인 대기).
- 확인용으로 테스트 계정(taewoong)에 치트로 캐릭터 전체 · 골드 500만 · 구슬 6등급 각 3개를 줬다(로컬 DB).

### 12차 (2026-11-10) — 칸 디자인 2차 ("너무 밋밋하다")
- `make_slots.py` 다시 그림(크레딧 0): `rarity_bg` 가운데가 빛나는 등급색 판 + 굵은 외곽선·안쪽 밝은 테 · 틀 장식을 등급 둘로 — 쇠 꺾쇠(일반~희귀) / `rarity_frame_gold` 금 귀퉁이 + 루비(영웅 이상, `SlotView.SetRarityFrame`).
- 작업슬롯: 빈 칸 돌바닥 벽돌 + 구리 꺾쇠·바느질 선 + 돋을새김 영웅 실루엣·구리 '+' 배지 / 잠긴 칸 리벳 쇠 띠 + 귀퉁이 볼트 + 육각 쇠판 자물쇠. 둘 다 Tiled(무늬 주기 = 가운데 칸 12의 약수).
- 플레이 캡처로 확인 — 바탕화면 `UI 작업 캡처 2026-11-10/07·08`.
- 로그인 입력칸 자리표시: `Loc_UI`에 `UI.Login.Placeholder`(아이디 입력 / Enter ID) 추가 → 파이프라인 · 씬 `Placeholder`에 `LocalizedText`.
