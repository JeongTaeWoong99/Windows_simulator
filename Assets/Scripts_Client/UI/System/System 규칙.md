# System 폴더 규칙

> 최종 업데이트: 2026-09-25 (툴팁 — T-088 · 수량 팝업의 묻는 말을 부르는 쪽이 넘긴다) · 2026-09-20 (보상 결과 팝업을 상자 개봉과 공유 — T-033) · 대상: `Assets/Scripts_Client/UI/System/`

**최상단 상주 오버레이 캔버스** — 로딩 표시 · 실패 알림 · 연결 끊김 종료, 그리고
**어느 열이 열려 있든 떠야 하는 결과 팝업**을 담는다.
다른 캔버스와 달리 **상주한다**(항상 켜져 있다).

| 파일 | 하는 일 |
|------|---------|
| `SystemCanvasView.cs` | 캔버스 껍데기 |
| `LoadingPresenter/LoadingPresenter.cs` | `ServerWaitManager.BusyChanged`를 구독해 대기 표시·클릭 차단 |
| `GachaResultPresenter/GachaResultPresenter.cs` | `PlayerDataModel.GachaCompleted`·`ItemUseCompleted`를 구독해 얻은 보상을 5열로 표시 (가챠·상자 개봉 공용) |
| `AmountInputPresenter/AmountInputPresenter.cs` | "몇 개?"를 묻고 확인한 수를 돌려준다. **넷 중 유일하게 구독형이 아니다** — 아래 "왜 이것만 `UIManager`를 거치는가" |
| `ConfirmPresenter/ConfirmPresenter.cs` | 예/아니오를 묻고 확인이면 콜백을 부른다. `AmountInputPresenter`와 같은 왕복형 — `UIManager.AskConfirm`이 중개 |
| `FpsTextPresenter/FpsTextPresenter.cs` | 창 구석에 FPS를 띄운다. `DisplayManager.FpsTextPositionChanged` 구독. **오버레이가 아니다** — 차단막·`CanvasGroup` 없이 텍스트만 켜고 끄며, `raycastTarget`을 꺼 클릭스루를 막지 않는다 |
| `TooltipPresenter/TooltipPresenter.cs` · `TooltipRowView.cs` | 커서 밑 `TooltipTrigger`(Shared) 옆에 툴팁을 띄운다. **차단하지 않는 오버레이다** — 아래 "툴팁" 절 |
| `NoticePresenter/NoticePresenter.cs` | `NoticeRaised`·`FatalRaised`를 구독해 알림·종료 안내 |

> **캔버스에 붙지 않는 정적 변환표는 여기 없다 (2026-09-12 · T-049).**
> `ResultMessages`·`RarityPalette`·`IndustryLabel`·`WorkStationProgress`는
> [`Shared 규칙.md`](<../Shared/Shared 규칙.md>)로 내려갔다 — **`#System Canvas`와 무관한데
> 이 폴더에 있어서 위치가 소유권을 거짓으로 주장했다.**

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
넷이 각자 그 `(↓ SUB VIEW)` 오브젝트에
스크립트·`CanvasGroup`·전체화면 `Image`(raycast blocker)를 함께 갖는다.

> 그 아래에 `Panel`이 한 겹 더 있는 셋이 있다 — 근거는 아래 "왜 여기만 `Presenter` 아래에
> `Panel`이 한 겹 더 있는가"에 있다. 없어도 되는 겹이 아니다.

**형제 순서가 곧 위아래다 — 나중에 올수록 위에 그려진다.**

```
!System Canvas (MAIN VIEW)
├─ [0] Fps Text Presenter    (↓ SUB VIEW)   ← 오버레이가 아니라 맨 밑
├─ [1] Loading Presenter     (↓ SUB VIEW)
├─ [2] Gacha Result Presenter (↓ SUB VIEW)
├─ [3] Amount Input Presenter (↓ SUB VIEW)
├─ [4] Confirm Presenter      (↓ SUB VIEW)
├─ [5] Tooltip Presenter      (↓ SUB VIEW)   ← 팝업 위 · 알림 아래
└─ [6] Notice Presenter      (↓ SUB VIEW)   ← 항상 마지막
```

**`Notice Presenter`는 언제나 맨 아래(마지막)다.** 알림은 실패·종료를 알리는 마지막 출구라
무엇에도 가려지면 안 된다. 오버레이를 새로 넣을 때는 그 앞에 끼운다.

### 차단막 색 — 넷이 같은 값을 쓴다

전체화면 blocker `Image`는 `sprite = null`(각진 네모) · `raycastTarget` 켬이 공통이고,
**색만 축이 갈린다.**

| 오버레이 | 색 | 왜 |
|---|---|---|
| `GachaResult` · `AmountInput` · `Confirm` · `Notice` | **검정 a 0.35** | 떠 있는 창에 눈을 모은다. 같은 알림·확인류라 농도가 다르면 생김새가 갈린다 |
| `Loading` | **흰색 a 0.851** | 이건 "무언가 떴다"가 아니라 **"지금은 아무것도 만질 수 없다"** 를 말한다. 뒤를 거의 덮는 것이 목적이라 다른 축이다 |

> ⚠️ **알파가 0이어도 `raycastTarget`이 켜져 있으면 계속 막는다.** 안 어둡던 시절(a 0.000)에도
> 차단은 정상으로 돌고 있었다 — **어두워졌는지로 차단 여부를 판단하면 안 된다.**

## ⚠️ 판별 축은 둘이다 — 순서를 지켜 묻는다

새 화면·팝업의 자리를 정할 때 **두 가지를 따로 묻는다. 하나로 묶으면 틀린다.**

| | 무엇을 정하나 | 질문 |
|---|---|---|
| **① 차단 범위** | **어느 캔버스에 사는가** | 이게 떠 있는 동안 **화면 전체**를 막아야 하나? |
| **② 생명주기** | `SetActive`인가 `CanvasGroup`인가 | 나를 **다시 켜 줄 주체가 밖에** 있는가? |

**①을 먼저 묻는다.** ①의 답이 "화면 전체"면 자리가 `!System Canvas`로 정해지고,
그 순간 ②의 답은 **`CanvasGroup`으로 강제된다** — 상주 캔버스에서 자기를 끈 채 시작하면
`Start`가 돌지 않아 배선이 끊기기 때문이다. 즉 ②는 ①에 종속될 수 있고, 그 반대는 없다.

### ① 차단 범위 — 열 안의 차단막은 다른 열에 닿지 않는다

| 캔버스 | Sorting Order |
|---|---|
| `!System Canvas` | **2** |
| `!Login Canvas` | 1 |
| `#Widget` · `#Storage` · `#Market` · `#Main` · `#State` | **0 — 전부 형제** |

열 캔버스는 전부 order 0인 **형제**다. 그 안에 전체화면 차단막을 깔아도 **자기 열만 막고**
상태바·메인·다른 열은 그대로 눌린다. 화면 전체를 막을 수 있는 것은 order 2인 이 캔버스뿐이다.

> 🔴 **여기서 한 번 틀렸다.** 수량 입력 팝업(당시 `SellAmountPresenter`)을 ②만 보고
> "격자가 켜 주니 `SetActive`" → `#Storage Canvas` 자식으로 뒀다. ②의 판단 자체는 맞았지만
> ①을 묻지 않았다. **증상은 "확인을 누르기 전인데 다른 열 버튼이 눌린다"로만 보여**
> 알파·`raycastTarget`·`blocksRaycasts`를 먼저 의심하게 되는데, 원인은 전부 그쪽이 아니라
> **캔버스 order**였다. 2026-09-05에 `!System Canvas`로 옮겼다.

### ② 생명주기 — 나를 다시 켜 줄 손이 밖에 있는가

| 종류 | 여닫는 법 | 무엇이 | 왜 |
|---|---|---|---|
| **밖에서 켜 주는 화면** | `SetActive` | 캔버스 · 메인 화면 셋 · 좌우 열 · 로그인 — **나머지 전부** | `UIManager`나 형제 Presenter가 켜 준다. 꺼져도 켜 줄 손이 살아 있고, `OnEnable` 재구독 + `Refresh`([`UI 규칙.md`](<../UI 규칙.md>)의 "공통 작성 규약")가 꺼진 동안 놓친 것을 따라잡는다 |
| **상주 오버레이** | `CanvasGroup` | 이 캔버스의 넷 전부 | 오브젝트를 끄면 `Start`가 돌지 않거나(배선이 끊긴다) 다시 켤 이벤트를 못 받아 **영구 잠김**이 된다(꺼진 오브젝트엔 콜백이 안 온다) |

⚠️ **`SetActive`로 못 여닫는다고 해서 "자기 이벤트로 뜨는 것"인 건 아니다.**
`AmountInputPresenter`는 밖(`UIManager.AskAmount`)에서 열어 주는데도 `CanvasGroup`을 쓴다 —
①이 자리를 정했고 자리가 여닫는 법을 정했을 뿐이다.

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

| 축 | 무엇 | 켤 때 | **끌 때** |
|---|---|---|---|
| **차단** `blocksRaycasts` | 뒤 UI 클릭을 막는다 | 대기가 시작되는 **즉시** | **즉시** |
| **표시** `alpha`·`interactable` | 로딩 몸통이 보인다 | **약 0.15초 뒤** | **즉시** |

> ⚠️ **끄는 쪽에는 지연을 넣지 않는다.** 실패 경로에서 `Resolve`가 `NoticeRaised`를 **먼저**,
> `BusyChanged(false)`를 나중에 던지는데 **둘이 같은 동기 호출**이라 사이에 프레임이 그려지지 않는다.
> 여기에 지연을 넣으면 로딩의 흰 차단막(a 0.851)과 알림의 검은 차단막(a 0.35)이 **겹쳐 보이는
> 프레임**이 생겨, 실패할 때마다 화면이 밝았다 어두워지는 깜빡임이 된다.
> 차단막이 전부 alpha 0이던 시절에는 안 보이던 결함이다.

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

다른 캔버스는 `(↓ SUB VIEW)` 아래에 내용이 바로 온다. 여기 넷 중 셋만 그 사이에 `Panel`이 있다.
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
2. **그래서 차단막이 Presenter로 내려온다.** 넷이 각자 뜨고 지므로 차단도 각자
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

## 왜 보상 결과 팝업이 거래 열이 아니라 여기 있는가

`GachaResultPresenter`는 `PlayerDataModel`의 결과 이벤트를 **스스로 구독해서 뜬다** —
위 표의 "자기 이벤트로 뜨는 상주 오버레이"에 그대로 해당한다.

거래 열(`#Market Canvas`)의 자식으로 두면 **요청을 보낸 직후 열을 닫는 순간 결과가 통째로
사라진다.** 위 "차단은 즉시" 절이 다루는 사고와 같은 뿌리다 — 다만 그쪽은 0.15초의 틈이
문제였고, 이쪽은 **사용자가 언제든 열을 닫을 수 있다**는 점이 문제다. 차단으로 막을 수 없다.

최상단 상주 오버레이로 두면 어느 열이 열려 있든, 심지어 다 닫혀 있어도 결과가 뜬다.

> 칸은 인벤토리와 같은 `SlotView` 프리팹을 쓴다 — 같아야 할 생김새를 두 벌로 두면
> 한쪽만 고쳐진다. 자세한 건 [`Storage 규칙.md`](<../Storage/Storage 규칙.md>).

### 가챠와 상자 개봉이 같은 팝업을 쓴다 (2026-09-20 · T-033)

서버가 개봉 보상을 가챠와 **같은 모양**(`GachaRewardInfo`)으로 내려주기 때문이다 —
연출을 두 벌 만들면 한쪽만 고쳐진다. 늘어놓는 방식만 갈린다:

| | 칸 하나가 뜻하는 것 | 왜 |
|---|---|---|
| 가챠 | 뽑힌 **한 건** | 뽑힌 순서대로 하나씩 공개하는 연출이 들어올 자리다(T-031) |
| 상자 개봉 | 한 **종류**의 합계 | 한 번에 99개까지 깐다 — 낱개면 칸이 수백 개가 되어 화면이 잠긴다 |

보상 종류는 넷이다(`EGachaRewardType`) — 아이템 · 캐릭터 · 장비 · **골드**(상자 전용, `Count`가 금액).
⚠️ 분기하지 않고 `ItemId`만 읽으면 그 보상이 **빈 칸**으로 그려지는데, 필드가 추가만 된 형태라
컴파일도 경고도 통과한다 — 캐릭터 축이 들어왔을 때 실제로 난 사고다.

## 왜 이것만 `UIManager`를 거치는가 — 넷 중 하나는 왕복이다

**여기 사는 오버레이는 원칙적으로 밖에서 참조당하지 않는다.** 매니저가 이벤트를 쏘고
Presenter가 스스로 구독해 뜬다 — 그래서 `UIManager`는 이 캔버스를 **들고 있지 않고**,
`SystemCanvasView.Show()`는 지금 호출처가 0이다(상주라 여닫을 일이 없다).

| 오버레이 | 무엇을 구독하나 |
|---|---|
| `LoadingPresenter` | `ServerWaitManager.BusyChanged` |
| `NoticePresenter` | `ServerWaitManager.NoticeRaised` · `FatalRaised` |
| `GachaResultPresenter` | `PlayerDataModel.GachaCompleted` · `ItemUseCompleted` |
| `AmountInputPresenter` | **없다 — 구독형이 아니다** |
| `ConfirmPresenter` | **없다 — 같은 왕복형이다** (`UIManager.AskConfirm`) |

셋은 단방향이다. "이런 일이 생겼다"를 듣고 뜨면 끝이라 부르는 쪽이 답을 기다리지 않는다.
**수량 팝업만 답을 돌려줘야 한다** — `Open(itemId, max, onConfirm)`의 `onConfirm`이 그것이라
이벤트만으로는 성립하지 않는다.

그렇다고 부르는 쪽(창고 격자)이 직접 들면 **캔버스를 넘어 남의 패널을 붙드는** 모양이 되고,
같은 팝업을 쓰는 화면이 늘 때마다 그 화면 수만큼 배선이 늘어난다.
→ 참조를 `UIManager` 한 곳에 모으고 **`AskAmount(itemId, max, question, onConfirm)`** 로 중개한다.
  묻는 말(`question`)은 부르는 쪽이 넘긴다 — 판매 담기는 "몇 개를 팔까?", 상자 개봉은 "몇 개를 열까?"다.
  팝업에 문구를 박아 두면 두 번째 용도가 생기는 순간 틀린 말을 한다(2026-09-25 · 상자 개봉이 "팔까?"로 묻고 있었다).

> ⚠️ **`UIManager`가 든 것은 캔버스가 아니라 팝업 하나다.** 여기 오버레이를 새로 넣을 때
> 기본값은 여전히 **구독형(참조 없음)** 이고, 중개는 **답을 돌려줘야 할 때만** 더한다.

> ⚠️ **팝업이 뜬 채로 부른 화면이 닫히는 경로를 막아야 한다.** 상주라 `OnDisable`이 오지 않아
> 스스로 정리할 손이 없다 — `UIManager.CloseAllExceptWidget()`이 `Close()`를 부른다.
> 빠뜨리면 증상이 "가끔 아무것도 없는 바탕에 팝업만 떠 있다"로만 보인다.

## 툴팁 — 막지 않는 오버레이 (2026-09-25 · T-088)

부가·세부 정보(산업 레벨 스펙)와 아이콘 버튼의 이름은 **올리면 뜨는 툴팁**으로 보인다.
대상에 `TooltipTrigger`(`UI/Shared/`)를 붙이면 끝이고, 그리는 것은 여기 하나다.

| 갈림길 | 결정 | 왜 |
|---|---|---|
| 자리 | 이 캔버스 · `Notice` 바로 앞 | 어느 열 위에서든 떠야 하고, 결과 팝업 위의 칸에도 뜰 수 있어야 한다. 알림은 여전히 그 위다 |
| 여닫기 | `CanvasGroup.alpha`만 | 상주 오버레이라 ②가 강제한다. **`blocksRaycasts`는 늘 끈다** |
| 호버 감지 | `WindowManager.UIHitsUnderCursor`의 **맨 위 하나** | 포커스가 없으면 Unity 입력이 멈춰 `IPointerEnter`가 안 온다. 클릭스루 판정이 쏜 레이캐스트를 같이 읽는다(프레임당 한 번) |
| 자리 계산 | 대상의 오른쪽 옆 · 넘치면 왼쪽 · 세로는 창 안으로 | 커서를 따라가면 읽는 동안 흔들린다 |
| 지연 | 0.3초 · 숨김 즉시 · 끈 직후 0.15초 안에 다른 대상이면 바로 | 버튼 사이 간격을 지나는 한두 프레임에 대상이 비어도 끊겨 보이지 않게 |

> ⚠️ **툴팁 안의 그림은 전부 `raycastTarget`을 끈다.** 켜 두면 커서 밑에 깔린 툴팁이 맨 위 결과가 되어
> 대상을 잃고 깜빡이고, 클릭스루 판정이 툴팁을 콘텐츠로 봐 **빈 바탕에서 창이 클릭을 먹는다.**
> 줄 프리팹(`TooltipRowView`)도 마찬가지다.

> **맨 위 하나만 보는 것이 곧 차단 규칙이다.** 팝업의 차단막이 떠 있으면 맨 위가 차단막이라
> 가려진 버튼에는 뜨지 않는다 — 따로 막을 코드가 없다.

