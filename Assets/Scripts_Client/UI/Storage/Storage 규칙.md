# Storage 폴더 규칙

> 최종 업데이트: 2026-08-23 (`UI 규칙.md`에서 분리) · 대상: `Assets/Scripts_Client/UI/Storage/`

**`#Storage Canvas` — 탭으로 내용을 갈아 끼우는 창고 화면.**

| 폴더 | 무엇 |
|------|------|
| `StorageCanvasView.cs` | 캔버스 껍데기 |
| `StorageTabPresenter/` | 탭 4개를 쥐고 무엇을 켤지 정한다 |
| `InventoryPresenter/` | 자원 목록 (+ 종속 View `InventorySlotView`) |
| `StorageInformationPresenter/` | 창고 정보 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 스크롤·레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

---

## 탭 전환도 층은 하나다

`#Main Canvas`와 같은 원리다 — 자리를 나눠 쓰는 화면이 여럿이면 **캔버스를 여러 개 두지 않고
Presenter를 여러 개 두고, 켜고 끄는 곳을 한 군데로 모은다.** 여기서는 `StorageTabPresenter`가
그 한 군데다. 탭 enum 하나를 seam으로 두고, 각 Presenter는 자기를 끄지 않는다.

## 아직 화면이 없는 버튼도 Presenter가 잡아 둔다

탭 4개 중 자원 하나만 실재하지만, `StorageTabPresenter`가 **네 버튼을 다 잡아 두고**
"아직 없다"를 로그로 남긴다. **안 잡아 두면 눌러도 아무 일이 없어 버튼이 고장 난 것과
구분되지 않는다.**

> ⚠️ `tabButtons` 배열 위의 `[CenterHeader]`에는 `[NonReorderable]`을 같이 단다 —
> **순서가 곧 탭의 의미**라 드래그로 뒤바뀌면 안 되고, reorderable list 경로에서는
> 헤더가 통째로 안 보인다([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약").

## 반복 칸은 종속 View로 뺀다

`InventorySlotView`가 그 예다. 같은 칸이 N개 복제돼 각자 다른 데이터에 묶이면 위젯을
Presenter가 다 들고 있을 수 없다. **매 프레임 도는 계산은 View가 아니라 `InventoryPresenter`가
하고, View에는 결과만 넘긴다** — 상시 실행 앱에서 비용이 칸 수만큼 곱해진다.
