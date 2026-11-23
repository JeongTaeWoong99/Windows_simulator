# Steam 폴더 규칙

> 최종 업데이트: 2026-11-24 (로그인 화면 스팀 모드 [접속하기]·[종료] · 빌드에 `steam_appid.txt` 복사) · 2026-11-23 (폴더 신설 — 스팀 로그인 티켓 · 에디터 시험 메뉴 · T-211 시험 환경 — 480 + 개인 키로 로컬 서버 `steam:<id>` 계정 생성 실측) · 대상: `Assets/Scripts_Client/Steam/`

**스팀(Steamworks.NET)과 이야기하는 코드.** 화면을 그리지 않고, 서비스로 등록하지 않는다.

## 지금 있는 것

| 파일 | 하는 일 |
|------|---------|
| `SteamAuth.cs` | `SteamAPI.InitEx` · `GetAuthTicketForWebApi("wsgame")` → 티켓 hex. 로그인 화면(`LoginPresenter.LoginWithSteam`)이 `C_LoginRequest.SteamTicket`에 싣는다 |
| `Editor/SteamAppIdBuildCopy.cs` | Windows 빌드가 끝나면 루트 `steam_appid.txt`를 exe 옆에 복사한다(직접 실행용 — 스팀에 올리는 빌드에는 빼야 한다 · T-210 뒤) |
| `Editor/SteamLoginTestMenu.cs` | 에디터 메뉴 `Window/DesktopWindowControl/스팀 로그인 시험` — 플레이 중 로그인 화면에서 스팀으로 들어간다(에디터 전용 — 빌드에 안 들어간다) |

## 알아둘 것

- **패키지** — `com.rlabrecque.steamworks.net`(UPM git · 태그 고정). 네이티브 DLL이 패키지 안에 있다.
- **AppID** — 지금은 Valve 시험 앱 `480`. 에디터는 프로젝트 루트 `steam_appid.txt`를 읽는다. 우리 AppID가 나오면(T-210) 이 파일만 바꾼다.
- **identity `"wsgame"`** 은 서버 설정 `Game:Steam:Identity`와 같아야 한다(#144).
- **콜백은 `SteamAPI.RunCallbacks`로 온다** — 상주 매니저가 없어 티켓을 기다리는 동안만 `SteamAuth`가 돌린다. 업적·오버레이 등 상주 콜백이 생기면 그때 `Managers/`에 서비스를 둔다.
- **에디터 시험** — 플레이 중 로그인 화면에서 메뉴 `Window/DesktopWindowControl/스팀 로그인 시험`. 스팀 클라이언트가 켜져 있고 로컬 서버에 Web API 키(사용자 환경 변수 `Game__Steam__WebApiKey`)가 있어야 한다.
- **로그인 화면 두 모드**(`LoginPresenter`) — 빌드는 스팀 모드: Id 칸을 숨기고 [접속하기]·[종료]. 에디터는 Id 모드, 치트 창 도구 줄 [스팀 화면]을 켜면 다음 플레이부터 스팀 모드(EditorPrefs — 씬에 안 남는다). 자동 접속은 하지 않는다(2026-11-24 사용자).
