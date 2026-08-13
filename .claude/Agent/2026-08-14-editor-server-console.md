---
date: 2026-08-14
title: 에디터 서버 콘솔 — 버튼으로 WSGameServer 토글 실행 + 로그 tail
tags: [client, editor, tooling, server]
---

# 에디터 서버 콘솔

## 목적 / 배경
- 클라 작업 중 서버 확인하려고 매번 Rider 터미널에서 `dotnet run`을 치고 창을 오가던 불편 해소.
- 상단 툴바 '서버 콘솔' 버튼 → 창의 시작/정지 토글로 서버를 백그라운드 실행/종료하고, 로그를 창에서 실시간으로 본다.

## 변경 내용
- `Editor/` 평면 구조를 기능별 하위 폴더로 재편(`.cs`+`.meta` `git mv`로 GUID 보존):
  - `Editor/shared/` — `EditorGit.cs`·`ProjectPreferences.cs`
  - `Editor/scene-copy/` — 씬 복사 4파일
  - `Editor/server-console/` — 신규 3파일
- 신규 `Editor/server-console/ServerRunner.cs` — 실행/종료 제어(UI 없음).
- 신규 `Editor/server-console/ServerConsoleWindow.cs` — `EditorWindow`, 토글 + 로그 파일 tail.
- 신규 `Editor/server-console/ServerConsoleToolbarButton.cs` — `[MainToolbarElement]` 버튼(Right, index 1).
- 문서: `Editor 규칙.md`(하위 그룹·server-console 섹션 추가), `폴더 구조.md`(Editor 행 갱신).

## 주요 결정 / 근거
- **로그를 파일로 OS 리다이렉트**(`cmd /S /C "chcp 65001 && dotnet run ... > Temp/WSGameServer.log 2>&1"`):
  in-process로 stdout을 붙잡으면 **도메인 리로드마다 static·콜백이 소멸해 로그가 끊긴다.** 파일 tail은 리로드와 무관.
  `chcp 65001` + UTF-8 읽기로 한글 로그 깨짐 방지. `/S /C`는 앞뒤 따옴표만 벗겨 경로 따옴표 보존.
- **PID는 `SessionState`**: 도메인 리로드를 넘어 살아남고 Unity 재시작 때 비워짐 → 리로드 후 재부착, 재시작 후엔 orphan 방지.
- **종료는 `taskkill /T`**: `dotnet run`이 실제 서버를 자식(cmd→dotnet→WSGameServer)으로 띄우므로 트리 전체를 내려야 함.
- **하위 폴더도 Editor 어셈블리**: Editor 폴더 하위는 전부 `Assembly-CSharp-Editor`라 그룹화가 빌드·네임스페이스에 무영향.
- 실행 방식은 빌드 exe 대신 `dotnet run`(항상 최신 소스, 빌드 잠금 회피) — 사용자 선택.

## 업데이트 (2026-08-14) — .NET SDK 해석 추가

- **증상**: 에디터에서 실행하면 `NETSDK1045: 현재 .NET SDK는 .NET 10.0 타겟팅을 지원하지 않습니다`로 빌드 실패.
- **원인**: 이 머신은 .NET 10 SDK가 **사용자 로컬 `C:\Users\ASUS\.dotnet\sdk\10.0.202`**에만 있고,
  PATH엔 `C:\Program Files\dotnet`(SDK 9만)와 `~/.dotnet\tools`(툴만)만 등록됨. Rider 터미널은 `~/.dotnet`을
  쓰지만, **Unity가 띄운 cmd는 PATH의 dotnet(SDK 9)** 을 잡아 net10 빌드가 깨졌다. 툴·인코딩은 정상이었다.
- **수정**: `ServerRunner.ResolveDotnetExe()` 추가 — `~/.dotnet\dotnet.exe`가 있고 그 `sdk/`에 메이저 ≥ `RequiredSdkMajor`(=10)
  SDK가 있으면 그 exe를 쓰고, 아니면 PATH의 `dotnet`에 맡긴다. 명령의 `dotnet`을 이 해석 결과로 교체(따옴표로 감쌈).
  full-path dotnet.exe는 자기 위치 기준으로 SDK를 찾으므로 SDK 10이 선택된다.
- ⚠️ `RequiredSdkMajor`는 서버 `WSGameServer.csproj`의 TFM(net10.0)에 묶인다 — TFM 올리면 이 상수도 올린다.
- ⚠️ 협업자 머신에서 .NET 10이 Program Files에만 있으면 `~/.dotnet`이 없으니 PATH의 dotnet을 그대로 쓴다(정상).

## 업데이트 (2026-08-14) — 서버가 기동 직후 종료되던 문제 (UI 뒤집힘 + 접속 거부)

- **증상**: '서버 시작' 후 잠깐 초록/실행 중이다가 곧 회색/정지로 뒤집힘. 로그엔 "10050 포트에서 대기 중"이
  찍히는데도 게임 접속은 `SocketException: 연결 거부`. → 두 증상이 같은 원인.
- **원인**: `CreateNoWindow=true` + `UseShellExecute=false`면 서버 프로세스에 **콘솔(stdin)이 없다.**
  서버 `Program.cs:18`은 기동 후 `Console.ReadLine()`으로 대기하는데, stdin이 EOF면 즉시 null을 받아
  **Main이 끝나 프로세스가 곧바로 종료**된다. 그래서 서버가 잠깐 떴다 꺼지고(→ 포트 닫힘 → 접속 거부),
  추적하던 cmd PID도 죽어 UI가 정지로 뒤집혔다.
- **수정**: `UseShellExecute=true` + `WindowStyle=Hidden`으로 **숨겨진 콘솔**을 준다. ReadLine이 정상적으로
  블록해 서버가 계속 살아 있고, 이 프로세스는 에디터와 독립이라 도메인 리로드도 견딘다.
  (`CreateNoWindow`는 ShellExecute에서 무시되므로 `WindowStyle`로 숨긴다.)
- ⚠️ 대안(pipe로 stdin 열어두기)은 우리가 쥔 핸들이 리로드 때 닫히면 서버가 EOF로 죽어 **리로드 생존과 상충**해 기각.

## 업데이트 (2026-08-14) — 로그인 전 자동 끊김 + orphan 서버 가드

에디터 서버로 옮긴 뒤 "연결은 되는데 로그인 전 5초 만에 끊긴다"는 증상 조사. **에디터 툴 탓이 아니다** —
툴은 터미널의 `dotnet run`과 같은 소스를 그대로 빌드한다.

- **원인 (클라)**: `PingManager`가 `OnLoginCompleted`에서만 `_isRunning=true`라 **로그인 전엔 1바이트도 안 보낸다.**
  서버 `MikaServer.SweepIdle`는 로그인 여부와 무관하게 `now - LastReceivedAt ≥ SessionIdleTimeout`인 세션을 끊는다.
  → 연결 후 무핑 5초 = 끊김.
- **원인 (서버, 디버그 값)**: `Global.SessionIdleTimeout`이 커밋 `d2c52c6`(08-10)에서 15초→**5초**로 내려감.
  같은 커밋의 `GatherSpeedMultiplier=6.0`(채취 6배)으로 채취 주기가 30→5초가 되자 "판정은 채취 주기보다 짧게"
  규칙에 맞춰 따라 내린 것. **둘 다 "배포 전 되돌린다"고 주석에 적힌 확인용 값.** 터미널에서 문제없던 시절은
  이 값들이 들어가기 전(15초)이었다.
- **서버 쪽 모순(서버 담당 영역 — 손대지 않고 공유만)**: `Global.cs:32` 주석은 아직 "5초마다 핑 — 세 번 연속
  놓쳐야 끊긴다"(15초 시절)인데 현재 판정 5초 = 핑 주기 5초라 여유 0. 핑 한 번만 늦어도 끊긴다. 주석 갱신 누락.
- **수정 (클라, 내 담당)**: `PingManager`를 **연결 시점부터** 핑 보내도록 변경 —
  로그인 게이팅(`OnLoginCompleted`)·`PlayerDataModel` 의존 제거, `BeginHeartbeat()`를 `Start`/`OnEnable`에서 호출.
  서버 `Handle_C_PingRequest`는 로그인 없이도 Pong을 돌려주고, 연결 전 `Send`는 `Session?.SendPacket`이라 안전한 no-op.
  어떤 서버 타임아웃 값에도 견고해진다. `Managers 규칙.md` 하트비트 절에 "연결 시점부터" 한 줄 추가.

- **에디터 서버 비정상 종료(orphan)**: Unity 크래시·강제 종료 시 `EditorApplication.quitting`이 안 불려
  숨겨진 서버가 orphan으로 10050을 점유 → 재시작 시 `SessionState` PID 소멸로 추적 불가 → 다음 Start가 바인딩 실패.
  **가드 추가**: `ServerRunner.Start()`가 `IsServerPortInUse()`(`GetActiveTcpListeners`)로 포트 점유를 검사,
  점유 중이면 다이얼로그로 확인 후 `KillByPort`(netstat -ano 파싱 → PID → `taskkill /T`)로 정리하고 시작.

## 업데이트 (2026-08-14) — 서버 콘솔 로그 정렬 깨짐 (한글 가변폭 폴백)

- **증상**: 터미널에선 "시각·레벨·스레드" 컬럼이 세로로 맞는데, 서버 콘솔 창(`ServerConsoleWindow`)에선 밀려 보인다.
- **원인**: 창이 `Font.CreateDynamicFontFromOSFont("Consolas", 12)`로 렌더. **Consolas엔 한글 글리프가 없어**
  한글(분류·내용)이 Unity 기본 **가변폭 폴백 폰트**로 그려진다. ServerLog의 공백 패딩 정렬(`[{Thread,-12}]`)은
  고정폭에서만 성립하므로 무너진다. 서버 포맷·`ClientLogger`·문자열 가공은 무관(순수 폰트 문제).
- **수정(클라 전용)**: `ServerConsoleWindow`가 프로젝트에 포함된 **네오둥근모**(`Assets/Resources/Fonts/neodgm_pro.ttf`,
  `forceTextureCase:-2` Dynamic·`includeFontData:1` — ASCII·한글 모두 고정폭)를 `AssetDatabase.LoadAssetAtPath<Font>`로
  불러 쓰도록 변경. 못 찾으면 Consolas로 폴백. 서버 포맷 정의는 그대로 둔다.

## 후속 작업 / 주의사항
- ⚠️ **서버 담당자와 협의 필요**: `GatherSpeedMultiplier`(6.0)·`SessionIdleTimeout`(5초)은 확인용 값이라
  배포 전 원복(1.0/15초) 대상이고, `Global.cs:32` 주석은 현재 5초와 어긋난다. 클라 측 수정으로 증상은 사라지지만
  서버 판정 5초 = 핑 주기 5초의 여유 0 문제는 남아 있다.
- ⚠️ **미검증**: Unity 에디터에서 실제 컴파일·구동을 아직 못 돌렸다. 새 폴더 3개와 스크립트 3개의 `.meta`는
  Unity를 갱신해야 생성된다(생성 후 원본과 함께 커밋). GUID 충돌 시 이동한 4파일의 참조부터 확인.
- ⚠️ Encoding: 서버 로그가 여전히 깨지면 `chcp 65001` 경로 대신 파일을 `Encoding.Default`로 읽는 쪽 검토.
- 서버 실행 중엔 서버 측 재빌드가 DLL 잠금(MSB3021) 유발 — 이 툴은 실행/종료만, 빌드 관여 안 함.
- 계획 원본: `~/.claude/plans/lively-splashing-cascade.md`.
