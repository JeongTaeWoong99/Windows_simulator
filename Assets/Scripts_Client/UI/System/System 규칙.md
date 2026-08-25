# System 폴더 규칙

> 최종 업데이트: 2026-08-26 (`WorkStationProgress` 추가 · 정적 변환표를 별도 표로 분리) · 대상: `Assets/Scripts_Client/UI/System/`

**최상단 상주 오버레이 캔버스** — 로딩 표시 · 실패 알림 · 연결 끊김 종료, 그리고
**어느 열이 열려 있든 떠야 하는 결과 팝업**을 담는다.
다른 캔버스와 달리 **상주한다**(항상 켜져 있다).

| 파일 | 하는 일 |
|------|---------|
| `SystemCanvasView.cs` | 캔버스 껍데기 |
| `LoadingPresenter/LoadingPresenter.cs` | `ServerWaitManager.BusyChanged`를 구독해 대기 표시·클릭 차단 |
| `GachaResultPresenter/GachaResultPresenter.cs` | `PlayerDataModel.GachaCompleted`를 구독해 뽑힌 보상을 5열로 표시 |
| `NoticePresenter/NoticePresenter.cs` | `NoticeRaised`·`FatalRaised`를 구독해 알림·종료 안내 |

**캔버스에 붙지 않는 정적 변환표도 여기 산다** — 어느 캔버스에서나 쓰이고
어디에도 속하지 않는 것들이라, 한 화면 폴더에 두면 다른 화면이 그걸 참조하게 된다.

| 파일 | 하는 일 |
|------|---------|
| `ResultMessages.cs` | 결과 코드 → 사용자 문구 |
| `RarityPalette.cs` | 등급 → 표시 색 |
| `WorkStationProgress.cs` | 슬롯 스냅샷 → 진행도·남은 초. **큰 창의 슬롯 목록과 상주 위젯이 함께 쓴다** — 복사하면 서버 판정식이 두 벌이 된다 |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 캔버스·레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

---

## 계층과 Sorting Order

`!System Canvas`는 **가장 큰 Sorting Order**를 갖는다 — 로딩·알림은 무엇 위에든 떠야 하고,
다른 화면이 그 위를 덮으면 안 된다.

| 캔버스 | Sorting Order |
|--------|---------------|
| `!System Canvas` | **2** |
| `!Login Canvas` | 1 |
| 나머지 `#...Canvas` | 0 |

> 값은 작지만 **상대 순서만 맞으면 된다**. 문서가 한동안 `100·200·300`이라 적어 두었는데
> 씬의 실제 값과 달랐다 — 2026-08-25에 실제 값으로 맞췄다.

`Override Sorting` + 자기 `GraphicRaycaster`는 여기도 그대로 적용된다.

계층은 다른 캔버스와 같은 2단이다 — `(MAIN VIEW)` → `(↓ SUB VIEW)` → 내용.
`Loading Presenter` · `GachaResult Presenter` · `Notice Presenter`가 각자 그 `(↓ SUB VIEW)`
오브젝트에 스크립트·`CanvasGroup`·전체화면 `Image`(raycast blocker)를 함께 갖는다.

> 그 아래에 `Panel`이 한 겹 더 있는 둘이 있다 — 근거는 아래 "왜 여기만 `Presenter` 아래에
> `Panel`이 한 겹 더 있는가"에 있다. 없어도 되는 겹이 아니다.

**형제 순서가 곧 위아래다 — 나중에 올수록 위에 그려진다.**

```
!System Canvas (MAIN VIEW)
├─ [0] Loading Presenter   (↓ SUB VIEW)
├─ [1] GachaResult Presenter (↓ SUB VIEW)
└─ [2] Notice Presenter    (↓ SUB VIEW)   ← 항상 마지막
```

**`Notice Presenter`는 언제나 맨 아래(마지막)다.** 알림은 실패·종료를 알리는 마지막 출구라
무엇에도 가려지면 안 된다. 오버레이를 새로 넣을 때는 그 앞에 끼운다.

## ⚠️ 무엇으로 여닫는가 — `SetActive`인가 `CanvasGroup`인가

**가르는 기준은 "보이냐"가 아니라 "나를 다시 켜 줄 주체가 밖에 있는가"다.**

| 종류 | 여닫는 법 | 무엇이 | 왜 |
|---|---|---|---|
| **밖에서 켜 주는 화면** | `SetActive` | 캔버스 · 메인 화면 셋 · 좌우 열 · 로그인 — **나머지 전부** | `UIManager`나 형제 Presenter가 켜 준다. 꺼져도 켜 줄 손이 살아 있고, `OnEnable` 재구독 + `Refresh`([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약")가 꺼진 동안 놓친 것을 따라잡는다 |
| **자기 이벤트로 뜨는 상주 오버레이** | `CanvasGroup` | `LoadingPresenter` · `GachaResultPresenter` · `NoticePresenter` | 스스로 이벤트를 받아 뜨는 게 **유일한 입구**다. 자기를 끄면 다시 켤 이벤트를 못 받아 **영구 잠김**이 된다(꺼진 오브젝트엔 콜백이 안 온다) |

오버레이는 오브젝트를 **항상 활성**으로 두고 `alpha`(0/1)·`blocksRaycasts`로 여닫는다.
`blocksRaycasts`가 대기·알림 중 뒤 UI 클릭을 막는다(전체화면 blocker Image는 alpha 0이어도
`raycastTarget`이 켜져 있으면 계속 막는다).

**`interactable`은 뒤 UI를 막지 않는다.** 그건 이 `CanvasGroup` **안쪽**의 `Selectable`만 잠근다.
오버레이 몸통에 버튼이 없으면 하는 일이 없다 — 막는 것은 `blocksRaycasts`와 blocker Image다.

### 전부 `CanvasGroup`으로 통일하면 안 된다

`CanvasGroup`은 **레이아웃에서 빼 주지 않는다.** alpha가 0이어도 자리는 그대로 차지한다.

- `#Main Canvas`의 세 화면(목록·선택·설정)이 동시에 활성이 되어 `VerticalLayoutGroup` 아래
  **한 자리를 셋이 나눠 갖는다.**
- `CloseAllExceptWidget`이 캔버스까지 끄는 이유가 무력화된다 — 그 900px가 안 사라져
  **위젯이 창 가장자리에서 밀린다**([`Layout 규칙.md`](<../Layout/Layout 규칙.md>)).
- 안 보이는 화면이 계속 돈다. `WorkStationListPresenter.Update`는 지금 SUB VIEW라 꺼질 때
  함께 멈추는데, 그 보장이 사라진다. 상시 실행 앱에서 비용은 곧 생존 조건이다.

### 로딩은 두 축을 나눠 쓴다 — 차단은 즉시, 표시만 지연

| 축 | 무엇 | 언제 |
|---|---|---|
| **차단** `blocksRaycasts` | 뒤 UI 클릭을 막는다 | 대기가 시작되는 **즉시** |
| **표시** `alpha`·`interactable` | 로딩 몸통이 보인다 | **약 0.15초 뒤** |

표시를 미루는 것은 깜빡임 제거다 — 그 안에 응답이 오면 아예 안 떠서 빠른 왕복이 조용하다.

**차단까지 미루면 안 된다.** 그 0.15초 동안 뒤 UI가 열려 있어, 요청을 보낸 화면을 사용자가
닫아 버릴 수 있다. 그러면 `OnDisable`이 구독을 끊어 응답을 놓치고, **실제로는 성공한 요청에
5초 뒤 무응답 알림이 뜬다**(가챠 요청 중 하단 [거래]로 열 닫기, 작업슬롯 대기 중 뒤로가기).

차단에 틈이 없는 근거는 두 가지다 —
- 각 Presenter가 `Send()` 직후 `_wait.Begin()`을 부르고, `Begin`이 `BusyChanged(true)`를
  **동기로** 던진다(`ServerWaitManager.Begin`). 사이에 프레임 경계가 없다.
- 실패 시 `Resolve`가 `NoticeRaised`를 **먼저**, `BusyChanged(false)`를 나중에 던진다.
  Notice가 차단을 넘겨받은 뒤 Loading이 내려간다.

**전역 차단이 있어도 각 Presenter의 개별 버튼 잠금은 남긴다** (`_isWaiting`·`SetButtons`·
`ApplyWaitingLock`). 역할이 다르다 — 차단은 "대기 중 전체"를 막고, 개별 잠금은 "이 버튼은 지금
못 누른다"를 **보이게** 한다.

## 왜 여기만 `Presenter` 아래에 `Panel`이 한 겹 더 있는가

다른 캔버스는 `(↓ SUB VIEW)` 아래에 내용이 바로 온다. 여기 셋 중 둘만 그 사이에 `Panel`이 있다.
**멋이 아니라 차단막과 창을 한 사각형에 담을 수 없어서 생긴 겹이다.**

`!Login Canvas`와 나란히 놓고 보면 갈라지는 지점이 보인다.

| | `!Login Canvas` | `!System Canvas` |
|---|---|---|
| 여닫는 법 | 캔버스째 `SetActive` | **상주**(항상 켜짐), 오버레이가 각자 `CanvasGroup` |
| 차단막 Image | **캔버스에 있다**(a 0.43) | **캔버스에 없다** |
| Presenter 크기 | 200×400 — 그 자체가 창 | **1920×1080** — 그 자체가 차단막 |
| 창 상자 | 필요 없다 | `Panel` 자식 |

왜 이렇게 되는지는 세 단계다.

1. **`!System Canvas`는 상주한다.** 여기에 전체화면 Image를 달면 아무것도 안 떠 있는 평상시에도
   화면 전체가 영원히 막힌다. 로그인은 캔버스가 통째로 꺼지므로 차단막을 캔버스에 올려도 된다.
2. **그래서 차단막이 Presenter로 내려온다.** 셋이 각자 자기 이벤트로 뜨고 지므로 차단도 각자
   해야 한다 → 각 Presenter가 자기 몫의 전체화면 Image(`blocksRaycasts`로 여닫는 그것)를 갖고,
   **오브젝트가 화면 전체로 늘어난다.**
3. **늘어난 사각형은 창이 될 수 없다.** 한 `RectTransform`이 "화면 전체로 stretch"와
   "폭 고정 + 내용만큼 세로로 자람(`ContentSizeFitter`)"을 동시에 할 수 없다 — stretch 앵커와
   `ContentSizeFitter`가 서로를 덮어쓴다. 그래서 창을 자식으로 뺀다.

**`LoadingPresenter`에 `Panel`이 없는 것이 반증이다** — 내용이 전체화면 텍스트 하나뿐이라
크기를 잡을 상자가 필요 없었다. 즉 이 겹은 **오버레이라서** 생기는 게 아니라
**크기를 스스로 정하는 창을 담을 때만** 생긴다.

> ⚠️ **지우면 둘 중 하나를 잃는다.** 창이 화면 전체로 늘어나 가챠 결과의 5 × n 성장이 죽거나,
> 차단막이 사라져 팝업 뒤 UI가 클릭된다. 새 오버레이도 창 모양이면 같은 겹을 둔다.

> `#Main Canvas`의 `Panel`은 이것과 다른 물건이다 — 거긴 차단이 아니라 **한 자리를 나눠 쓰는
> 화면들의 정렬 상자**다([`UI 규칙.md`](<../UI 규칙.md>)의 이름 규칙).

## 왜 가챠 결과 팝업이 거래 열이 아니라 여기 있는가

`GachaResultPresenter`는 `PlayerDataModel.GachaCompleted`를 **스스로 구독해서 뜬다** —
위 표의 "자기 이벤트로 뜨는 상주 오버레이"에 그대로 해당한다.

거래 열(`#Market Canvas`)의 자식으로 두면 **요청을 보낸 직후 열을 닫는 순간 결과가 통째로
사라진다.** 위 "차단은 즉시" 절이 다루는 사고와 같은 뿌리다 — 다만 그쪽은 0.15초의 틈이
문제였고, 이쪽은 **사용자가 언제든 열을 닫을 수 있다**는 점이 문제다. 차단으로 막을 수 없다.

최상단 상주 오버레이로 두면 어느 열이 열려 있든, 심지어 다 닫혀 있어도 결과가 뜬다.

> 칸은 인벤토리와 같은 `InventorySlotView` 프리팹을 쓴다 — 같아야 할 생김새를 두 벌로 두면
> 한쪽만 고쳐진다. 자세한 건 [`Storage 규칙.md`](<../Storage/Storage 규칙.md>).
