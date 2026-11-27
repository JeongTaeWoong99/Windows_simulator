# Artifact 폴더 규칙

> 최종 업데이트: 2026-11-27 (**도감 맨 위 특수 줄** — 상자 줄(상자 단계 순) · 레이드 줄 · 그 아래 등급 묶음 · Special 등급 + 얻는 곳으로 표에서 가른다 · #161 B안) · 2026-11-24 (**도감 상세 '얻는 곳'** — 유물 감정·상자·레이드 상자·채취별 확률을 표에서 계산(`ArtifactSources`) · 효과 칸을 실행 중에 복제해 아래에 · 상세 높이는 최소 300에서 내용만큼 · T-150) · 2026-11-21 (**도감에서도 장착** — 오른쪽 상세가 인벤토리 아티팩트 창과 같은 모양 + [도감에 장착] · [닫기]·[도감 보기]는 뺌 · 요청은 공용 `ArtifactEquipRequester` · 도감 스크롤바 없앰(휠로 굴린다) · 격자 가로 가운데 — 한 줄 칸 수로 남는 폭을 좌우 여백에 나눔) · 2026-11-15 (도감 칸에 이름을 쓰지 않는다 — 인벤토리 칸처럼 그림만 · 이름은 툴팁·오른쪽 상세 · 상세 이름은 `RarityPalette.OnLight` · 오른쪽 상세 윗선 = 스크롤 영역 윗선) · 2026-11-02 (차단막을 누르면 닫힌다 — `BackdropClose`) · 2026-11-01 (거점 화면과도 상호 닫기 · T-182) · 2026-10-29 (켜고 끄기 제거 — 장착하면 항상 켜짐 · 상세 패널은 보기만 · #90 · 장비 탭 칸이 서버 칸 번호 · 잠금 — #87) · 2026-10-25 ([효과 끄기] 뺌 — 꺼 둔 번호만 [효과 켜기] · 칸 'NEW' 글자 대신 장비와 같은 반짝임 · 판매 목록 '마석 0' 임시 — 이슈 #89 · #90) · 2026-10-25 (보유·판매 칸을 빼고 인벤토리 장비 탭으로 — 도감 + 상세 + 장착 반짝임) · 2026-10-25 (새로 만듦 — T-151 · T-182 · 이슈 #81) · 대상: `Assets/Scripts_Client/UI/Artifact/`

**`#Artifact Canvas` — 아티팩트 화면(도감). 경매장과 같은 자리(3열 가운데 띠 · 화면 전체 차단)에 뜨는 화면이다.**
배치는 목업([`아티팩트도감장착목업.html`](../../../../GameDesign/design/ui/아티팩트도감장착목업.html))을 따르되, 보유·판매는 인벤토리로 옮겼다(아래 "흐름").

| 파일 · 폴더 | 무엇 |
|------|------|
| `ArtifactCanvasView.cs` | 캔버스 — 여닫기 · 판을 3열 가운데 띠에 맞추기(`AuctionCanvasView`와 같은 방식) |
| `ArtifactHeaderPresenter/` | 머리 줄 — 제목 · 보유 골드 · 도움말 · [X] |
| `ArtifactCodexPresenter/` | 왼쪽 **도감** — 필터(전체/장착) · '장착 n / 전체' · 맨 위 특수 줄(상자 · 레이드) + 등급 묶음(`ArtifactCodexGroupView` — '획득 n / 전체' + 칸 격자). 방금 장착한 칸으로 굴러가 반짝인다 |
| `ArtifactDetailPresenter/` | 오른쪽 **상세** — 인벤토리 아티팩트 창과 같은 모양(이름·상태·효과·안내) + **[도감에 장착]**. 성공하면 그 칸이 반짝인다. 켜고 끄기는 없다(장착하면 항상 켜짐 · #90) |
| `ArtifactEquipRequester.cs` | 장착 요청 하나 — 연타 막기 · 대기 표시 · 결과. 이 상세와 인벤토리 아티팩트 창이 함께 쓴다 |
| `ArtifactText.cs` | 문구 — 효과 한 줄 · 상태 · 툴팁. 이 캔버스와 인벤토리의 아티팩트 칸·창이 함께 쓴다 |

상태(고른 번호 · 반짝일 번호)는 `Managers/ArtifactModel`이 들고, 보유·장착은 `PlayerDataModel`이 주인이다.

---

## 흐름 (2026-10-25 사용자 결정 · 서버 기획 동의)

1. 유물을 감정하면 아티팩트가 **인벤토리 장비 탭**에 들어온다 — 장비와 한 격자(서버 칸 번호 · T-188)에 선다. 새로 들어온 표시는 장비와 같은 반짝임이다 — 칸에 'NEW' 글자를 붙이지 않는다(도감에 없는 번호라는 경고는 판매 목록이 한다)
2. 장비 탭의 아티팩트 칸 — **우클릭(판매 담기) · Ctrl+좌클릭(잠금) · Ctrl+우클릭(경매 등록) · 끌기는 장비와 같다.** 좌클릭만 **아티팩트 창**(`Inventory/ArtifactEquipPresenter`)을 연다
3. 아티팩트 창에서 [도감에 장착] → 이 화면이 열리고 그 칸이 반짝인다. 이미 장착한 번호는 장착할 수 없다(번호마다 1개). 장착하면 효과가 계속 붙는다 — 끄는 길이 없다(#90)
4. 남는 것 — 싼 것은 즉시 판매, 비싼 것은 경매

> 조작 상세는 [`Inventory 규칙.md`](<../Inventory/Inventory 규칙.md>)의 "장비 탭의 아티팩트 칸".

---

## 자리 — `!Overlay Canvas`의 둘째 자식

```
!Overlay Canvas
├─ [0] #Auction Canvas (MAIN VIEW)
├─ [1] #Artifact Canvas (MAIN VIEW)   Canvas(중첩) · 차단막 · ArtifactCanvasView — 평소 꺼짐 · SetActive (UIManager.ShowArtifact)
│  └─ Body Panel                      VLG
│     ├─ Background
│     ├─ Header Presenter (↓ SUB VIEW)
│     └─ Content Panel                HLG
│        ├─ Codex Presenter (↓ SUB VIEW)      Filter Row · Codex Scroll View(Group Template) — 스크롤바 없음 · 좌우 여백은 코드가 맞춘다(가운데)
│        └─ Right Panel                       폭 640 · VLG
│           └─ Detail Presenter (↓ SUB VIEW)  높이 300 · Empty Text · Equip Panel(인벤토리 아티팩트 창 내용 복제 — 닫기·도감 보기 뺌)
└─ [2] Coin Fly Presenter …
```

- 판별·여닫기 근거는 경매장과 같다 — [`Auction 규칙.md`](<../Auction/Auction 규칙.md>)의 "자리".
- 안쪽 [X] 말고 **판 밖 차단막**을 눌러도 닫힌다(`BackdropClose`).
- **경매장과 아티팩트는 하나만 열린다** — `ShowAuction`·`ShowArtifact`가 서로를 닫는다. 같은 띠를 덮는 화면 둘이 겹치지 않게 한다.
- ⚠️ 굵은 한글 글자는 노랑 강조 서체로 그려진다 — 상세의 안내·효과 글자는 보통 굵기로 둔다.

---

## 도감 상세 — 얻는 곳 (2026-11-24 · T-150)

- `ArtifactSources.Build(tid)`가 서버와 같은 규칙으로 계산한다. 표가 바뀌면 따라간다.
  - 유물 감정 · 상자: 여는 아이템의 `OpenGachaId` 풀에서 **1회 추첨** — 확률 = 이 행 가중치 / 풀 가중치 합(아이템·캐릭터·장비·골드·아티팩트 표의 같은 GachaId를 합친다 · 서버 `GachaPoolCatalog`)
  - 레이드 상자: `RaidBossTable.ArtifactChancePermille` · 채취: `GatherArtifactTable.ChancePerMillion`(판정당)
  - 여는 아이템이 없는 풀(골드 뽑기 등)은 얻을 길이 없어 뺀다.
- 본 적 없는 번호도 얻는 곳은 보여 준다 — 무엇을 노릴지 고르는 데 쓴다.
- 칸은 씬에 없다 — `CreateSourceBox`가 효과 칸을 복제한다(씬 저장 시 레이아웃 값 수천 줄 변경을 피함). 상세 칸 높이 고정(300)도 같은 곳에서 풀어 최소 300 · 내용만큼 늘어난다.

## 도감 특수 줄 — 상자 · 레이드 (2026-11-27 · #161 · 목업 [`아티팩트도감특수줄목업.html`](../../../../GameDesign/design/ui/아티팩트도감특수줄목업.html) B안)

- 맨 위에 **얻는 곳별 줄**을 세우고 그 아래는 지금처럼 등급 묶음이다. 줄 머리 = `UI.Artifact.GroupChest`(상자) · `GroupRaid`(레이드) + '획득 n / 전체'.
- 판정은 표에서 한다(`ArtifactCodexPresenter.SourceOf`) — **등급이 Special**이고
  - `RaidBossTable.RewardArtifactTID`면 레이드 줄(보스 순)
  - 테마 상자(`ItemType.Box`)의 `OpenGachaId` 풀에 있으면 상자 줄(여는 상자 TID 순 = 상자 단계 순)
  - 어느 쪽도 아니거나 Special이 아니면 등급 묶음으로 간다 — 보스·상자가 늘면 그 줄만 길어진다.
- 새 상자 아티팩트 아이콘 34~37 — Unity AI 격자 1장(`_source/icons/unity-ai/artifacts_ai.py` · `artifact_grid_c`).

## 조작

| 어디 | 키 | 하는 일 |
|------|----|---------|
| 도감 칸 | 좌클릭 | 상세에 띄운다 |

- **장착은 두 곳에서 한다** — 인벤토리 아티팩트 창과 이 화면의 오른쪽 상세(2026-11-21). 장착은 되돌릴 수 없고, 장착하면 효과는 늘 켜져 있다(끄기 제거 — #90). 확인 창은 두지 않는다(번호마다 1개뿐이고 남는 것은 팔면 된다).
- **반짝임** — 장착 성공이 `ArtifactModel.Highlight`를 부르면 도감이 필터를 '전체'로 돌리고 그 칸으로 굴러가 튕김 + `SlotGainShine` 두 번. 도감이 막 켜지는 중이면 칸이 생긴 뒤(`Start`·`OnEnable`) 꺼내 간다.
- 메인 버튼에 점 알림을 붙이지 않는다. 효과 합계도 적지 않는다.

> 📌 경매장 · 아티팩트 · 거점은 같은 자리를 덮어 **셋 중 하나만 열린다**(`UIManager.ShowAuction` · `ShowArtifact` · `ShowOutpost` — 2026-11-01 · T-182).
