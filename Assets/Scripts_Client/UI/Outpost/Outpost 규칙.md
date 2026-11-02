# Outpost 폴더 규칙

> 최종 업데이트: 2026-11-02 (차단막을 누르면 닫힌다 — `BackdropClose`) · 2026-11-01 (거점 화면 첫 구현 · T-182 · #75)

**`#Outpost Canvas` — 거점 화면. 경매장·아티팩트와 같은 자리(3열 가운데 띠 · 화면 전체 차단)에 뜨는 지도 화면이다.**
하단 메뉴 [거점]으로 열고 안쪽 [X]·판 밖 차단막으로 닫는다(Esc 없음 · 경매장과 같다). 건물 창이 떠 있으면 창 뒤 바탕·판 밖 어느 쪽을 눌러도 건물 창만 닫힌다. 경매장 · 아티팩트 · 거점은 셋 중 하나만 열린다(`UIManager`).
배치는 목업([`거점목업.html`](../../../../GameDesign/design/ui/거점목업.html))을 따른다. 규칙·수치는 [`building/README.md`](../../../../GameDesign/design/building/README.md) · [`trait/README.md`](../../../../GameDesign/design/trait/README.md).

| 파일 · 폴더 | 무엇 |
|------|------|
| `OutpostCanvasView.cs` | 캔버스 — 여닫기 · 판을 3열 가운데 띠에 맞추기(`AuctionCanvasView`와 같은 방식) |
| `OutpostHeaderPresenter/` | 머리 줄 — 제목 · 도움말 · 계정 레벨 · 보유 골드 · [X] |
| `OutpostMapPresenter/` | 지도 — 건물 5채(`OutpostBuildingView`) · 건물 메뉴 · 바닥 누르면 메뉴 닫기 |
| `OutpostBuildingPresenter/` | 건물 창 머리 줄 — 이름 · 레벨 · 분류 · 탭 · [X]. **창 전체(`window`)를 여닫는다** |
| `OutpostUpgradePresenter/` | 업그레이드 탭 — 레벨 ▶ 다음 레벨 · 비용 줄(`OutpostCostRowView`) · 얻는 것 · 이유 · 버튼 |
| `TraitPresenter/` | 특성 탭 — 트리(노드 + 선) · 오른쪽 정보 카드. 옛 인벤토리 특성 탭을 옮겼다 |

데이터는 `PlayerDataModel`(`GetBuildingLevel` · `GetBuildingPoints` · `CheckBuildingUpgrade` · `TryGetUnmetPrerequisite`)과 `GameDataLoader`(`Buildings` · `TryGetBuildingLevel` · `HasBuildingNodes`)다.

---

## 건물 창은 형제로 나눈다

`Building Window > Window Panel` 아래에 `Building Presenter` · `Upgrade Presenter` · `Trait Presenter`가 **형제**로 있다(Presenter 안에 Presenter를 두지 않는다).
그래서 창을 여닫는 일은 자기 오브젝트가 아니라 **`OutpostBuildingPresenter.window`** 를 켜고 끈다. 지금 탭이 아닌 페이지는 꺼져 부모 세로 배치에서 빠진다.

## 지도 위 자리

- 건물 자리는 **앵커 = 지도 %** 다(표에 자리 칼럼이 없다). 어느 건물인지도 씬이 정한다(`OutpostBuildingView.buildingTid`).
- 바닥(`Ground Button`)은 `AspectRatioFitter` Envelope이고 건물·메뉴가 그 **자식**이다 — 화면 비가 바뀌어도 건물이 그림 위 같은 자리에 남는다. 넘친 부분은 `Map Presenter`의 `RectMask2D`가 자른다.
- 그림은 임시다(`Assets/Art/outpost/`). 정식은 같은 이름으로 덮는다.

## 표시 둘 — ▲ · ●

- ▲ = 지금 올릴 수 있다(`CheckBuildingUpgrade` — 서버 `TryUpgradeBuilding`과 같은 순서. 안내일 뿐 서버가 다시 검사한다).
- ● = 남은 포인트. **트리 노드가 없는 건물은 켜지 않는다** — 눌러도 쓸 곳이 없는 재촉이 된다.

## 트리 없는 건물

1차 트리는 작업장뿐이다(T-183). 나머지 4채는 업그레이드는 열어 두고, [특성] 탭을 감추고 "특성은 준비 중 — 포인트는 쌓입니다"를 적는다. 메뉴의 [특성]을 눌러도 업그레이드 탭으로 간다.

## 트리

- 노드 자리는 `UserTraitTable.Column` · `Row`, 선은 `RequiredTraitTIDs`다 — 노드가 늘어도 코드는 그대로다.
- ⚠️ **선은 영역의 실제 크기로 잰다.** 방금 켜진 창은 레이아웃이 돌지 않아 크기가 0이다 — `Show`가 **부모부터** 강제 재배치한 뒤 그린다(자기만 재면 부모 세로 배치가 정하는 크기를 못 받아 선이 왼쪽 아래에 뭉친다 · 2026-11-01 실측).
- 올리기는 2단계다 — 노드를 눌러 고르고, [레벨 올리기] → 확인창.
