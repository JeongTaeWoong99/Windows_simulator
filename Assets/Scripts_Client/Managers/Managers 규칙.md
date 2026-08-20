# Managers 규칙

> 최종 업데이트: 2026-08-20 (`ServerWaitManager` 추가 — 서버 왕복 대기 창구) · 대상: `Assets/Scripts_Client/Managers/`

**`MonoService<T>`를 상속해 서비스 로케이터에 등록되는 것들.** 그게 이 폴더의 정의다.
`Services.Get<T>()`로 어디서나 꺼내 쓰는 전역 상태·기능이 여기 있다.

---

## 1. 무엇이 여기 오는가

```
Services.Get<T>() 로 꺼내 쓸 전역 상태·기능인가?
├─ 예 → Managers/   (MonoService<T> 상속 필수)
└─ 아니오
    ├─ 화면을 그린다        → UI/
    ├─ 정적 유틸이다        → 해당 기능 폴더 (예: Data/GameDataLoader — static class)
    └─ 범용 인프라다        → Common/  (⚠️ 승인 필요 — Common 규칙.md)
```

**`MonoService<T>`를 상속하지 않는 클래스는 여기 두지 않는다.** 등록되지 않는데 폴더 이름만
"Managers"면, 꺼내 쓰려다 `Services.Get`이 실패하는 혼란이 생긴다.

---

## 2. 현재 매니저 5개

| 클래스 | 역할 | 경계 — 하지 않는 것 |
|--------|------|--------------------|
| **`PlayerDataModel`** | 서버가 밀어준 **내 계정 상태** 캐시(인벤토리·슬롯·캐릭터·재화). 수신 진입점을 구독해 가공된 변경 이벤트를 발행 | **송신하지 않는다.** 요청은 그 요청을 일으킨 Presenter가 직접 보낸다 |
| **`UIManager`** | 화면 골격의 단일 출입구. 캔버스 여닫기 + `#Main Canvas` 안의 화면 전환 | 위젯을 직접 쥐지 않는다. 각 패널의 위젯은 그 패널의 Presenter 몫 |
| **`WindowManager`** | 데스크톱 창 제어(투명·항상 위·클릭 스루·크기·위치) | Win32 **선언**은 갖지 않는다 → `DesktopWindow/Win32Native` |
| **`PingManager`** | 연결 생존 확인(5초 Ping / 15초 무응답 감지) | **소켓을 끊지 않는다.** 알리고 앱을 내릴 뿐 — 세션 정리는 서버 폴더의 것 |
| **`ServerWaitManager`** | **서버 왕복 한 건의 대기 창구.** 로딩 표시 on/off · 5초 무응답 감시 · 실패/치명 알림 발행 | **패킷을 받지 않는다.** 요청을 보낸 Presenter가 `Begin` 후 `Succeed`/`Fail`로 보고한다. `EResultCode`도 모른다 — 문구 변환은 Presenter가 `ResultMessages`로 마쳐 넘긴다 |

### `ServerWaitManager` — 왕복 한 건을 지켜보는 곳

`PingManager`와 **축이 다르다.** 저쪽은 "연결이 살아 있나"(주기적 하트비트),
여기는 **"방금 보낸 요청 한 건이 돌아왔나"** 다.

```
Presenter          요청 전송 직후  Begin("로그인", onClosed) → ServerWaitHandle
   │                                   │ 0.15초 지나도 안 끝나면 로딩 표시
   ├─ 성공 응답 ──→ handle.Succeed()   │ 로딩만 조용히 내린다
   ├─ 실패 응답 ──→ handle.Fail(문구)  │ 로딩을 내리고 알림을 띄운다
   └─ 아무 보고도 없다 ────────────────┘ 5초 뒤 스스로 닫고 무응답 알림
```

- **타임아웃은 전 요청 공통 5초 단일 기준값이다.** 지점마다 다르게 두지 않는다.
- 한 번 닫힌 핸들은 이후 호출을 무시한다 — 타임아웃이 먼저 닫은 뒤 늦게 온 응답이 조용히 흘러간다.
- `onClosed`는 성공·실패·타임아웃 **어느 쪽이든** 불린다. 호출부의 버튼 잠금은 여기서 푼다
  (무응답으로 버튼이 영구히 잠기는 경로가 사라진다).
- 둘이 만나는 곳은 하나뿐이다 — `PingManager`가 연결 끊김을 판정하면
  알림을 띄울 창구로 `RaiseFatal`을 빌려 쓴다.

> 화면 쪽은 `UI/System/`의 `LoadingPresenter`(`BusyChanged` 구독)와
> `NoticePresenter`(`NoticeRaised`·`FatalRaised` 구독)가 맡는다 →
> [`UI 규칙.md`](<../UI/UI 규칙.md>) §7.

### 짝을 이루는 두 데이터 창구

혼동하기 쉬운 지점이다. **누구에게나 같은 값**과 **나에게만 해당하는 값**이 갈라져 있다.

| | `Data/GameDataLoader` | `Managers/PlayerDataModel` |
|---|---|---|
| 출처 | 엑셀 → `.bytes` (고정 테이블) | 서버 패킷 |
| 내용 | 아이템명·캐릭터명·수치 | 인벤토리·슬롯·보유 캐릭터·재화 |
| 성격 | 누구에게나 같다 | 내 계정에만 해당한다 |

⚠️ **이름 조회에서 자주 틀린다.** `GameDataLoader.GetCharacterName`은 **TID**(캐릭터 종류)를 받고,
`PlayerDataModel.GetCharacterName`은 **개체 번호**(내가 가진 그 한 장)를 받는다.
바꿔 넣으면 `?#2` 같은 값이 나온다. 자세한 건 [`Data 규칙.md`](<../Data/Data 규칙.md>).

### 하트비트(`PingManager`)가 필요한 이유

**TCP는 끊김을 즉시 알려주지 않는다.** 소켓이 FIN/RST 없이 사라지는 종료
— 유니티 플레이 중지 · PC 절전 · 랜선 분리 — 에서는 **양쪽 다 상대가 죽은 걸 모른다.**

| | 증상 |
|---|---|
| 서버 | 접속 중으로 보고 **채취를 계속 굴린다** (좀비 유저) |
| 클라 | 화면은 멀쩡한데 **아무것도 반영되지 않는다** |

그래서 5초마다 Ping을 보내고 15초 무응답이면 `[연결]` 오류를 남긴 뒤,
`ServerWaitManager.RaiseFatal`로 **치명 알림을 띄운다 — 사용자가 확인을 누르면 앱이 종료된다**
(에디터에서는 플레이 모드가 멈춘다). 끊긴 채로 화면만 멀쩡한 상태를 남기지 않기 위해서다.
한 번이라도 Pong을 받았는지로 **"도중 끊김"과 "최초 접속 실패"** 를 갈라 문구를 고른다.

**그래도 소켓을 끊지는 않는다** — 세션은 서버 담당 코드(`MikaClient`)의 것이고,
좀비 세션을 실제로 정리하는 것도 서버 몫이다. 이건 그 절반이다.

핑은 **로그인 전, 연결된 순간부터** 보낸다. 서버는 유휴 세션을 로그인 여부와 무관하게 끊으므로,
로그인 전에 아무것도 안 보내면 연결만 되어 있어도 유휴로 판정돼 끊긴다
(서버 Ping 핸들러는 로그인 없이도 Pong을 돌려준다). 연결 즉시 핑을 시작해 소켓을 살려 둔다.

### ⚠️ Ping은 매니저가 송신하는 유일한 예외다

**요청 패킷은 그 요청을 일으킨 Presenter가 직접 보낸다** — 버튼과 패킷이 한자리에 있어야
"이 화면이 무엇을 보내는가"가 코드에서 보이기 때문이다(`PlayerDataModel`은 수신 전담).

Ping만 예외인 이유는 **사용자 조작이 아니라 연결 수명 관리**라서다.
누를 버튼도, 보여 줄 화면도 없다.

> 🔑 이 예외를 **"매니저도 송신한다"로 일반화하지 않는다.** 화면에서 비롯된 요청이면
> 그 화면의 Presenter가 보낸다.

---

## 3. ⚠️ 서비스 조회는 반드시 `Start`

**가장 자주 밟는 함정이다.** `Services.Get<T>()`를 `Awake`·`OnEnable`에서 부르면 안 된다.

Unity는 씬을 열 때 오브젝트마다 `Awake → OnEnable`을 **이어서** 부른다. 모든 `Awake`가 먼저
끝나는 게 아니므로, `OnEnable` 시점엔 다른 서비스가 아직 등록 전일 수 있다.
"모든 `Awake`가 끝났음"이 보장되는 첫 시점은 `Start`다.

```
Start     → Get + 최초 구독
OnEnable  → 캐시가 있을 때만 재구독 (껐다 켜는 경로)
OnDisable → 구독 해제
```

어기면 `Services.Get`이 `KeyNotFoundException`을 던지고, 캐시 필드가 null로 남아
**한참 뒤 사용 지점에서 `NullReferenceException`으로 다시 터진다** — 원인과 증상이 멀어진다.

> 구독을 `OnEnable`/`OnDisable`에 두고 싶으면 **조회만** `Start`로 분리한다.
> `Start`와 `OnEnable`이 모두 구독을 시도하므로 중복 구독 플래그(`_isSubscribed`)로 막는다.

### 유일한 예외 — `WindowManager`는 `Awake`에서 초기화한다

`SettingPresenter.Start()`가 창 설정값을 읽어 토글·드롭다운을 맞추는데, **Unity는 `Start` 순서를
보장하지 않는다.** `WindowManager`의 로드를 `Start`에 두면 패널이 먼저 도는 경우
아직 읽지 않은 값을 가져간다.

**이 예외가 안전한 이유는 다른 서비스를 건드리지 않기 때문이다** — `LoadSettings()`는
`PlayerPrefs`에서 값을 읽는 순수 로드라 등록 순서 문제와 무관하다.

> 🔑 판단 기준: **`Awake`에서 해도 되는 것은 "자기 값만 채우는 일"** 뿐이다.
> 다른 매니저를 조회하는 순간 `Start`로 내려야 한다.

---

## 4. `MonoService<T>`의 `T`에 무엇을 넣나

`T`는 **"이 객체를 무엇으로 찾을 것인가"** 를 정하는 키다. `Services`가 `typeof(T)`를 키로 쓰므로
**여기 적은 타입으로만** 꺼낼 수 있다.

| 쓰임 | 선언 | 조회 |
|------|------|------|
| **자기 자신으로 등록** — 교체할 일 없는 매니저 | `class XxxManager : MonoService<XxxManager>` | `Services.Get<XxxManager>()` |
| **역할 인터페이스로 등록** — 구현을 갈아끼울 것 | `class Person : MonoService<IWalk>` | `Services.Get<IWalk>()` |

2번이 이 클래스를 단순 싱글톤과 구분 짓는 지점이다. `Person`을 `Robot`으로 교체할 때
인터페이스로 등록했으면 씬에서 오브젝트만 바꾸면 되고 호출부는 그대로다.

⚠️ **둘은 같은 게 아니다.** `MonoService<Person>`으로 등록하면 `Person`이 `IWalk`를 구현했더라도
`Get<IWalk>()`는 키가 없어 실패한다.

**현재 이 폴더의 4개는 전부 1번(자기 자신)** 이다 — 교체 대상이 아니기 때문이다.

---

## 5. `WindowManager`의 창 정책

Win32 호출 자체는 [`DesktopWindow 규칙.md`](<../DesktopWindow/DesktopWindow 규칙.md>)에 있다.
여기는 **이 게임이 그 API로 무엇을 정했는가**다.

### 크기는 절대 픽셀 프리셋이다 — 모니터 비례가 아니다

기준 **960×540(16:9)** 에 배율을 곱한 고정 픽셀이다.

| 프리셋 | 크기 |
|---|---|
| `X1` | 960 × 540 |
| `X1_25` | 1200 × 675 |
| `X1_5` | 1440 × 810 |
| `X2` | 1920 × 1080 |

**왜 절대 픽셀인가** — 어느 기기에서든 UI 픽셀 크기가 똑같아야 **디자인 검증과 버그 재현**이 된다.
모니터 비례로 두면 같은 배율이라도 실제 픽셀 수가 달라져 "내 화면에선 되는데"가 생긴다.

**16:9는 어떤 경우에도 유지한다** — 배율이 모니터보다 크면 작업 영역 안으로 줄이는데,
이때도 **가로·세로를 같은 비율로** 줄인다(`ClampToWorkArea`). 한 축만 줄이면 UI가 찌그러진다.

### ⚠️ 창 크기는 언제나 "클라이언트 영역" 기준이다

`SetWindowPos`가 받는 건 **외곽(테두리 포함) 크기**다. 원하는 렌더 해상도를 그대로 넘기면
타이틀바·테두리 두께만큼 **렌더 영역이 줄어 16:9가 깨진다** — 그러면 `Match = Height`인 캔버스의
기준 폭이 1920이 아니게 되어 열 폭이 전부 어긋난다.

`ClientSizeToOuterSize`가 스타일(`GWL_STYLE`/`GWL_EXSTYLE`)과 창의 DPI를 읽어
`AdjustWindowRectExForDpi`로 프레임 두께를 더한다.

**스타일을 바꾼 뒤에는 크기를 반드시 재적용한다.** `SetTitleBar`의 `SWP_NOSIZE`는 외곽을 유지한 채
프레임만 벗기므로, 그 순간 클라이언트 영역이 커진다.

### ⚠️ 크기·위치는 한 기준으로 원자 적용한다 (`ApplySizeAndPosition` — A-1)

크기와 위치를 **따로** 적용하지 않는다. 예전엔 리사이즈(`SWP_NOMOVE`)와 이동(`SWP_NOSIZE`)을
나눠 불렀는데, 그 사이 과도 상태에서 두 가지가 어긋났다:

- **모니터 판정 오염** — 리사이즈가 좌상단을 고정한 채 창을 키우면, 그 커진 사각형으로
  `MonitorFromWindow`가 다른 모니터를 잡아 클램프·앵커가 서로 다른 작업 영역 기준으로 계산됐다.
- **외곽 재추정 불일치** — 위치 계산이 외곽 크기를 다시 추정해, 리사이즈의 실측 보정(dx/dy)과
  어긋나 오른쪽·아래 앵커가 프레임 두께만큼 넘쳤다.

그래서 `ApplySizeAndPosition`은 ① 작업 영역을 **한 번만** 고정하고(클램프·앵커가 같은 `wa`를 씀),
② `ClientSizeToOuterSize`로 외곽을 만들어 앵커 좌표를 계산한 뒤 **단일 `SetWindowPos`로 이동+크기 동시** 적용,
③ `GetClientRect`로 실측해 어긋나면 외곽을 그 차이만큼 보정하되 **앵커 좌표도 보정된 외곽으로 재계산**한다
(한 번만). 크기·위치가 항상 같은 외곽 값을 공유하므로 스냅이 어긋나지 않는다.

### ⚠️ 작업 영역은 "주 모니터"가 아니라 "창이 놓인 모니터"다

`SPI_GETWORKAREA`는 **주 모니터 값만** 돌려준다. 듀얼 모니터에서 창을 보조 모니터로 옮기면
위치·클램프 계산이 전부 어긋난다. `MonitorFromWindow(MONITOR_DEFAULTTONEAREST)` +
`GetMonitorInfo().rcWork`를 쓰고, 그게 실패할 때만 `SPI_GETWORKAREA`로 폴백한다.

앵커 좌표 계산에는 **외곽 크기**를 쓴다 — `SetWindowPos`가 옮기는 게 외곽 사각형이라,
클라이언트 크기로 계산하면 타이틀바가 켜졌을 때 오른쪽·아래 앵커가 프레임 두께만큼 넘친다.

### 타이틀바·투명·동적 클릭스루는 고정값이다 (토글을 걷어냈다)

설정 UI에서 이 세 토글을 제거하고 오브젝트를 비활성화했다. 그래서 `LoadSettings`는 이 셋을
**저장값에서 읽지 않고 인스펙터 `setStart*`를 그대로 고정**한다(에디터·빌드 공통) — 저장값을 읽으면
옛 실행에서 남은 상태가 되살아나는데 되돌릴 UI가 없기 때문이다. 기본값은 타이틀바 off · 투명 on · 동적 클릭스루 on.
`Set*` 기능 코드는 남겨 둔다(재활성화 여지).

### Topmost·크기·위치의 권위 소스 — 에디터=인스펙터 / 빌드=저장값

이 셋은 **어디서 실행하느냐**로 진실이 갈린다. `LoadSettings`가 `#if UNITY_EDITOR`에선 인스펙터
`setStart*`를, 빌드에선 저장값(`WindowSettings`)을 읽는다 — 위젯 위치(`WidgetPositionLayout.LoadSavedPosition`)도
같은 규칙이라 창·위젯이 에디터 전 구간에서 같은 소스를 따른다. 편집 중 인스펙터를 바꾸면
`WindowManager.OnValidate`가 위젯에 거울질해 미리보기가 즉시 따라온다(창 자체는 빌드에서만 움직인다).
자세한 근거·트레이드오프는 [`Settings 규칙.md`](<../Settings/Settings 규칙.md>) §1.

### 위치는 6칸이고, 드롭다운 하나가 창·위젯을 함께 정한다

`ScreenAnchor`는 6칸(가로 3 × 세로 2, Middle 행 없음)이라 `WidgetPosition`과 인덱스가 1:1이다.
`SettingPresenter`의 위치 드롭다운 하나가 창 앵커(`SetAnchorByIndex`)와 위젯 위치
(`WidgetPositionLayout.SetPosition`)를 **같은 인덱스로** 몰이한다 — 짝이 맞는 모서리 조합만 유효.
레거시 9분할 저장값은 `LoadSettings`의 `MigrateAnchor`가 6칸으로 접는다(Upper 유지, Middle/Lower → Lower, 멱등).
저장값을 읽는 **빌드에서만** 필요해 `MigrateAnchor`는 `#if !UNITY_EDITOR`로 가둔다(에디터는 인스펙터 값을 그대로 쓴다).

### 창 이동은 캔버스 드래그로 OS에 위임한다

보더리스가 기본이라 OS 타이틀바가 없다. 메인 뷰 캔버스에 `WindowDragArea`를 붙여 잡아 끌면
`BeginWindowDrag`가 `ReleaseCapture` + `WM_SYSCOMMAND(SC_MOVE_HTCAPTION)`으로 **OS 이동 루프에 위임**한다
— 스냅·모니터 간 이동을 다시 구현하지 않는다. 상세는 [`DesktopWindow 규칙.md`](<../DesktopWindow/DesktopWindow 규칙.md>) 5-7.

### ⚠️ 캔버스 기준 해상도를 창 크기로 바꾸지 않는다

`CanvasScaler`의 `referenceResolution`은 **고정**이어야 창이 커질 때 `Match = Height`가
UI를 비례 확대한다. 기준 해상도를 창 크기에 맞춰 바꾸면 스케일이 1로 고정돼
**UI가 확대되지 않고 좌상단에 몰린다.**

### ⚠️ `Screen.SetResolution`을 쓰지 않는다

윈도우 모드에서 이걸 부르면 Unity가 **창 스타일·위치·Z순서를 기본값으로 되돌린다** —
타이틀바가 되살아나고 항상 위가 풀리고 창이 중앙으로 리셋돼 **Win32 창 제어를 전부 덮어쓴다.**

백버퍼는 `SetWindowPos` 리사이즈에 맞춰 Unity가 알아서 갱신하므로 부를 이유도 없다.

---

## 6. 관련 문서

| 주제 | 문서 |
|------|------|
| 화면 전환 흐름 · `MainScreen` enum · 캔버스 배치 | [`UI 규칙.md`](<../UI/UI 규칙.md>) |
| 패킷이 어떤 길로 오는가 · ⭐수량 값의 뜻 | [`서버 동작 이해.md`](<../서버 동작 이해.md>) |
| 어떤 기능이 무엇을 주고받는가 (로그인 세트·채취·적성) | [`패킷 레퍼런스.md`](<../패킷 레퍼런스.md>) |
| 창 제어 Win32 선언 | [`DesktopWindow 규칙.md`](<../DesktopWindow/DesktopWindow 규칙.md>) |
| 창 설정 저장 | [`Settings 규칙.md`](<../Settings/Settings 규칙.md>) |
| `Services`·`MonoService` 자체 | [`Common 규칙.md`](<../Common/Common 규칙.md>) |
