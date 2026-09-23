# Main 폴더 규칙

> 최종 업데이트: 2026-09-23 (목록 칸에도 산업 레벨을 적는다 — T-080) · 대상: `Assets/Scripts_Client/UI/Main/`

**`#Main Canvas` — 한 자리를 여러 화면이 갈아 끼우는 유일한 캔버스.**
`UI/`에서 규칙이 가장 많은 곳이라, 화면을 하나 더 붙이려면 여기를 읽는다.

| 폴더 | 무엇 |
|------|------|
| `MainCanvasView.cs` | 캔버스 껍데기 + **`SetTitle(string)`** (아래 "캔버스 머리의 제목") |
| `WorkStationListPresenter/` | 작업슬롯 목록 (+ 종속 View `WorkStationSlotView`) |
| `WorkStationSelectPresenter/` | 작업슬롯 선택 (+ 종속 View `CharacterStateRowView` — 목록 줄과 세팅 카드가 함께 쓴다 · `EfficiencyRowView` — 효율 계산 한 줄) |
| `SettingPresenter/` | 창 설정 |
| `MenuPresenter/` | 하단 메뉴 — **항상 켜져 있다** |

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

---

## 한 캔버스 안에서 화면을 갈아 끼운다

같은 자리를 여러 화면이 나눠 쓰면 **캔버스를 여러 개 두지 않고 Presenter를 여러 개 둔다.**

```
#Main Canvas (MAIN VIEW)   LayoutElement(pref 900 · flexH 0) · Canvas · GraphicRaycaster · VerticalLayoutGroup
├─ Title                                        pref 50  · flexH 0   ← 항상
├─ WorkStation List Presenter   (↓ SUB VIEW)    pref  0  · flexH 1   ┐
├─ WorkStation Select Presenter (↓ SUB VIEW)    pref  0  · flexH 1   │ 하나만 켜진다
├─ Setting Presenter            (↓ SUB VIEW)    pref  0  · flexH 1   ┘
└─ Menu Presenter               (↓ SUB VIEW)    pref 100 · flexH 0   ← 항상
```

- **갈아 끼워지는 화면은 전부 같은 레이아웃 값(`preferredHeight 0` · `flexibleHeight 1`)을 준다.**
  그래야 어느 것이 켜지든 같은 자리에 같은 크기로 들어간다.
  겹쳐 놓을 필요가 없다 — **꺼진 오브젝트는 레이아웃 계산에서 아예 빠진다.**
- **배경 Image는 "전부 꺼질 수 있는가"로 정한다.** 여기처럼 `Title`·`Menu`가 늘 켜져 있으면
  캔버스에 배경을 둬도 된다(빈 판이 보일 일이 없다). 자식이 전부 꺼질 수 있는 캔버스라면
  **배경을 자식으로 내린다** — 안 그러면 다 껐는데 빈 판만 남는다.
- 화면마다 캔버스를 두면 **화면을 하나 붙일 때마다 `Canvas`·`GraphicRaycaster`·`LayoutElement` 높이를
  따로 맞춰야 하고, 하나만 어긋나도 크기가 틀어진다**([`Layout 규칙.md`](<../Layout/Layout 규칙.md>)).

### 하단 메뉴 줄이 특별 이벤트 자리다

`Menu Presenter` 줄이 기획 2.4의 **특별 이벤트 자리**다(2026-09-16 · 기획 6장 Q12 해소). 별도 층을 두지 않는다.
보스 · 대형 작업물처럼 채취 루프 밖의 콘텐츠가 오면 **이 줄에 버튼이 는다.**

- 줄이 `pref 100 · flexH 0`이고 갈아 끼워지는 화면이 `flexH 1`이라 **버튼이 늘어도 슬롯 영역은 밀리지 않는다** —
  줄 안의 `HorizontalLayoutGroup`(폭 확장)이 버튼 폭만 나눈다.
- 아직 없는 콘텐츠의 **빈 칸·잠긴 버튼을 미리 두지 않는다**. 버튼에 **배지·타이머·"오늘까지"를 붙이지 않는다**(P1).

## 전환 층은 하나다 — 화면은 자기를 끄지 않는다

> 2026-08-10 변경. 예전엔 **같은 일을 두 층이 따로 했다.**
> ```
> UIManager                  작업슬롯 ↔ 설정        ← 바깥에서
>   └ WorkStationPresenter   목록 ↔ 선택            ← 안에서
> ```
> 폴더도 하이어라키도 이 중첩을 그대로 베껴 `WorkStation Panel` 한 겹이
> **아무 위젯도 안 가진 채** 끼어 있었다. 세 화면을 형제로 눕히고 `WorkStationPresenter`를 지웠다.

**`MainScreen` 하나로 셋을 다룬다. 켜고 끄는 곳은 `UIManager.ShowMainScreen` 한 곳뿐이다.**

```csharp
// 목록 — 자기를 끄지 않는다. 번호를 넣고 자리를 넘길 뿐이다.
selectPresenter.Open(slotIndex);
ui.ShowMainScreen(MainScreen.WorkStationSelect);   // 이 호출이 목록을 끈다

// 선택 — 뒤로가기도 마찬가지다. 스스로 끄면 목록이 켜지기 전 빈 칸이 남는다.
ui.ShowMainScreen(MainScreen.WorkStationList);
```

**참조 방향은 하나뿐이고, 방향에 이유가 있다.** 목록이 선택 화면을 참조한다 —
반대로 선택 화면이 목록의 이벤트를 구독하면 **평소 꺼져 있어서 신호를 못 받는다.**
`OnDisable`에서 구독을 끊는 규약과 정면으로 부딪힌다.

> **살아 있는 쪽이 넘긴다.** 이게 "이벤트를 쏘고 위층이 받는다"를 대신하는 규칙이다.
> 위층이 없어졌으므로 받을 사람도 없다.

> 꺼져 있는 화면을 열 때는 **인자를 먼저 넣고 켠다**(`Open(slotIndex)` 안에서 `SetActive(true)`).
> 꺼진 오브젝트는 `Start()`가 아직 안 돌았을 수 있어, 켠 직후 값을 넣으면 초기화가 덮어쓴다.

## 작업슬롯 칸은 세 상태다 — 잠김 · 열린 빈 칸 · 배치됨

| 상태 | 판정 | 프레임 라벨 | 클릭 |
|---|---|---|---|
| 잠김 | `WorkSlotTable`의 `UnlockTID`를 `PlayerDataModel.IsUnlocked`가 false | 해금 조건(골드 · 계정 레벨 · 안 열린 선행 칸) | 해금 흐름 |
| 열린 빈 칸 | 열림 + 배치 없음 | "비어있음." | 선택 화면 |
| 배치됨 | 열림 + 배치 있음 | 슬롯 뷰가 덮는다 | 선택 화면 |

- **해금 흐름은 서버 판정 순서를 따른다** — 선행 미충족 알림 → 골드 부족 알림 → 확인 팝업(`UIManager.AskConfirm`).
  클라 판정은 안내일 뿐이고 서버가 다시 검사한다.
- 조건 문구는 **클라가 테이블에서 만든다** — 서버는 열린 목록만 준다([해금 기획](<../../../../GameDesign/design/unlock/README.md>) 1장).
- 열린 목록은 로그인 때 `S_UnlockListResponse`로 오고(슬롯 스냅샷보다 앞), 확인을 누르면 `C_UnlockRequest`를 보낸다.
  성공하면 `UnlocksChanged`가 라벨을, 뒤따르는 `S_WorkStationSlotSyncResponse`가 새 칸을 그린다.
- **해금 결과는 내 요청이 아니어도 온다**(치트·퀘스트). 목록 갱신은 항상 하고, 결과 문구는 대기 핸들이 있을 때만 띄운다.
- 선행은 `UnlockTable.Name`으로 적는다 — 콘텐츠 테이블을 거꾸로 찾지 않는다(기획 unlock #13).

## 배치 목록 — 산업 버튼이 곧 정렬 기준이다

선택 화면의 캐릭터 줄은 **고른 산업의 적성 높은 순 → 등급 높은 순 → 개체 번호 순**이다
(`WorkStationSelectPresenter.CompareRows`). 산업 버튼을 누르면 걸러 내기와 정렬이 함께 바뀌므로
**정렬 UI를 따로 두지 않는다** — 목록 순서가 곧 "이 산업에 누구를 넣을까"의 추천이다.

- 개체 번호까지 가서 **동점을 없앤다.** `List.Sort`는 안정 정렬이 아니라 동점이면 다시 그릴 때마다 줄이 바뀐다.
- 다시 그리는 시점은 따로 두지 않았다 — 화면을 열 때(`OnEnable`)와 캐릭터가 늘 때(`CharactersChanged`) 이미 `Refresh`가 돈다.

### 줄 바탕은 등급 색이다

`CharacterStateRowView`의 루트 Image를 **`RarityPalette`의 등급 색**으로 칠한다(`SetRarity`) — 창고 칸과 같은 표다.
목록 줄과 세팅 카드가 같은 프리팹이라 둘 다 칠해진다.

- 등급은 **종류(TID)로 읽는다.** 세팅 카드는 슬롯이 개체 번호만 주므로 `PlayerDataModel.GetCharacterTid`를 거친다.
- `Clear()`가 `RarityPalette.Unknown`으로 되돌린다 — 풀에서 재사용되는 줄이라 안 되돌리면 이전 색이 남는다.
- 🎨 등급 테두리 스프라이트가 오면 **색 대신 스프라이트로 바꾼다.** 자리는 같은 `backgroundImage`다.

## 배치는 축이 둘이다 — 산업 × 산업 레벨 (2026-09-20 · T-068)

`Industry Panel` 바로 아래 `Industry Level Panel`(버튼 5개)이 있다. 같은 산업이라도 레벨이 오르면
판정 1회의 시간·경험치가 3배씩 늘고 **나오는 것이 통째로 바뀐다.**

- **레벨은 슬롯마다 따로다** — 전역이 아니다. 서버가 `t_user_workstation_slot.industry_level`에 저장한다.
  **특성으로 레벨을 열어도 이미 배치된 칸은 그대로다.** 올리려면 그 칸에서 다시 고른다.
- 잠금은 `IsUnlocked(GetIndustryLevelUnlockTid(산업, 레벨))`에서 파생된다.
  **Lv1은 `UnlockTID = 0`이라 늘 열려 있다.** 해금은 창고 **특성 탭 → 산업 레벨**에서 한다.
- 버튼 문구는 `IndustryLevelTable.Name`이라 산업마다 다르다(`Lv2 밭` · `Lv2 저수지`) —
  **코드가 갈아 쓴다.** 그 산업에 없는 레벨은 버튼째 숨긴다.
- **목록 칸은 `산업 Lv{n}`으로 붙여 적는다** — `슬롯 3 · 낚시 Lv2 · 아무개 · 1.24배`
  (`WorkStationSlotView.Bind` · 2026-09-23 · T-080). 산업과 레벨을 `·`로 가르지 않는 건
  **무엇의 레벨인지가 묶여 읽히게** 하기 위해서다. **레벨 이름은 세팅 화면의 버튼에만 쓴다** —
  이름이 최대 6자(`신성한 대지`)인데 `Slot Text`는 TMP 오토사이징이라 한 줄이 길어진 만큼 글자가 작아진다.
  **대기 칸(`슬롯 N · 대기`)에는 적지 않는다** — 산업이 `None`이라 레벨에 의미가 없다.
- **산업을 바꾸면 레벨 줄을 다시 그리고, 잠긴 레벨이 골라져 있으면 Lv1로 되돌린다.**
- 색 규칙·"켜진 불빛은 슬롯에서 파생된다"는 **산업 버튼과 똑같다** — 3단계에서 레벨을 누르면
  산업 교체와 같은 **재배치 요청 1회**이고, 같은 레벨이면 보내지 않고 실패해도 제자리다.
- `UnlocksChanged`를 구독한다 — 창고와 작업슬롯이 **동시에 보이므로**, 특성을 찍은 그 자리에서
  레벨 버튼이 켜져야 한다.

> ⚠️ **해제 요청에는 레벨을 `1`로 실어 보낸다.** 해제는 산업이 `None`으로 가는데 `None`에는
> Lv2 이상이 없어, 고른 레벨을 그대로 보내면 서버가 `IndustryLevelLocked`로 거절한다.
> "고른 값을 그대로 보낸다"가 통하지 않는 유일한 경로다.

## 캔버스 머리의 제목은 `UIManager`가 밀어 넣는다

화면이 바뀌면 `#Main Canvas`의 `Title` 문구도 바뀐다. 문구는 **코드가 아니라
`UI Manager > Main Screens`의 각 줄**에 적혀 있다 — 표시용이라 바뀌어도 로직이 안 바뀌는데
코드에 박으면 문구 하나 고치는 데 컴파일이 필요하고, 화면 목록과 제목 목록이 따로 논다.

**이것이 캔버스 View가 위젯을 쥐는 유일한 예외다.** 어느 화면의 Presenter에 맡겨도
**그 화면이 꺼질 때 함께 죽어서**, 정작 다른 화면으로 넘어간 순간 제목을 못 바꾼다.
그래서 `MainCanvasView`가 `SetTitle(string)`을 갖는다. Model을 구독하지 않고
**위에서 밀어 넣은 값만 그리므로** 역할은 여전히 View다.

## 메인 화면 추가 — 코드는 한 줄이다

`#Main Canvas`의 같은 자리를 나눠 쓰는 화면들은 `MainScreen` enum이 **seam**이다.
여는 쪽(`StatePresenter`의 버튼 · `WorkStationListPresenter`의 칸 클릭)과 패널을 쥔 `UIManager`가
서로의 오브젝트를 모른다.

```
[1] MainScreen 에 값 추가                                       ← 코드는 여기 한 줄뿐
[2] #Main Canvas 아래에 XX Presenter (↓ SUB VIEW) 를 만든다
       LayoutElement: preferredHeight 0 · flexibleHeight 1     ← 다른 화면들과 같은 값
[3] UI Manager > Main Screens 에 (값 · 오브젝트 · 제목) 한 줄
[4] 상태 패널 버튼으로 열 화면이면 State Presenter > Screen Buttons 에 (버튼 · 값) 한 줄
```

동작은 **버튼을 누르면 그 화면만 켜지고, 같은 버튼을 다시 누르면 기본(작업슬롯 목록)으로** 돌아온다.
여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어 버튼이 고장 난 것처럼 보이기 때문이다.

> ⚠️ **`MainScreen`에 값을 중간에 끼워 넣으면 씬의 배선이 조용히 어긋난다.**
> enum은 씬에 **int로** 저장돼서, 값 순서가 밀리면 `Screen Buttons`·`Main Screens`가
> 엉뚱한 화면을 가리킨다. 컴파일도 통과하고 경고도 없다 — **끝에 추가하거나,
> 순서를 바꿨으면 두 배열을 전수 확인한다.**
