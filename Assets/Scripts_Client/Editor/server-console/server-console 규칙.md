# server-console 폴더 규칙

> 최종 업데이트: 2026-09-17 (여는 버튼을 메인 툴바에서 치트 창 도구 줄로 이동) · 대상: `Assets/Scripts_Client/Editor/server-console/`

**에디터에서 서버를 켜고 끄고 로그를 보는 툴.** 폴더 전체에 걸리는 규칙(네임스페이스·툴바 버튼
공식 API)은 [`Editor 규칙.md`](<../Editor 규칙.md>)에 있다.

## 지금 있는 것

| 파일 | 하는 일 |
|------|---------|
| `ServerRunner.cs` | WSGameServer를 백그라운드로 켜고/끄는 프로세스 제어기(UI 없음). PID는 `SessionState`, 로그는 `Temp/WSGameServer.log`로 리다이렉트 |
| `ServerConsoleWindow.cs` | 실행/종료 토글 + 로그 파일을 tail 해 터미널처럼 보여주는 `EditorWindow`(보이는 줄만 그리는 가상 스크롤·줄 단위 복사) |

---

## 왜 이렇게 만들었나

클라 작업 중 서버를 확인하려고 매번 터미널에서 `dotnet run`을 치던 걸, **치트 창 도구 줄의 '서버 콘솔' 버튼 →
창의 시작/정지 토글**로 대체한다. 로그는 창 안에서 터미널처럼 실시간으로 본다.

- **실행 방식**: `ServerRunner`가 `cmd /S /C "chcp 65001 && dotnet run --project ...\WSGameServer.csproj > Temp\WSGameServer.log 2>&1"`
  를 백그라운드로 띄운다. 빌드 단계 없이 **항상 최신 소스**로 돈다.
- **로그를 파일로 리다이렉트하는 이유**: 서버 로그(`Console` 출력)를 cmd의 `>`로 `Temp/WSGameServer.log`에
  직접 적게 하고, 창은 그 파일을 tail 한다. ★ 이렇게 해야 **에디터가 스크립트를 재컴파일(도메인 리로드)해
  static·콜백이 소멸해도** 서버가 안 끊기고 로그도 계속 쌓인다. in-process로 stdout을 붙잡으면 리로드마다
  로그가 끊긴다. (`chcp 65001` + UTF-8 읽기로 한글 로그가 깨지지 않게 맞춘다.)
- **재부착**: 실행 중 프로세스의 PID를 `SessionState`에 둔다 — **도메인 리로드를 넘어 살아남고 Unity 재시작
  때 비워진다.** 창은 이 PID로 실행 여부를 판정하므로, 리로드 후에도 토글 상태·로그 tail이 이어진다.
- **종료**: `dotnet run`은 실제 서버를 자식으로 띄우므로 `taskkill /PID <pid> /T /F`로 **트리 전체**
  (cmd→dotnet→WSGameServer)를 내린다. Unity 종료 시(`EditorApplication.quitting`) 실행 중이면 정리한다.
- **로그 표시 — 보이는 줄만 그린다(가상 스크롤)**: 로그 전체를 한 문자열로 `SelectableLabel`·`CalcHeight`에
  넘기면, 한 줄만 늘어도 새 문자열이라 IMGUI 텍스트 캐시가 빗나가 **전체를 다시 레이아웃**한다.
  실측(neodgm 16px) 결과 493줄에서 377ms, 5000줄에서 6초가 걸렸다. 서버가 약 2초마다 로그를 찍어 그 주기로 EditorLoop 스파이크가 났다.
  그래서 줄바꿈 없는 고정 줄 높이로 스크롤 높이를 `줄 수 × 줄 높이`로 잡고, **화면에 걸친 줄만** 그린다.
  비용은 로그 총량과 무관하다(새 줄 3개 ≈ 3ms, 한 화면을 처음 넘길 때 ≈ 40ms 1회).
  - 가로 폭은 **그린 줄만** 재서 넓힌다. 전체 줄을 미리 `CalcSize`하면 5000줄에 약 5초가 걸린다.
  - 여러 줄 드래그 텍스트 선택은 없다. 대신 **줄 단위 선택**(클릭·Shift+클릭·드래그)과 Ctrl+C·Ctrl+A·우클릭 복사를 둔다.
  - 창을 열 때(도메인 리로드 포함) 로그 파일은 **끝 2MB만** 읽는다. 메모리에는 최근 10000줄만 둔다.
- `Temp/`는 `.gitignore`라 로그 파일은 커밋되지 않는다. 서버 실행 중 서버 측 재빌드는 DLL 잠금(MSB3021)을
  유발하므로, 이 툴은 실행/종료만 하고 빌드에는 관여하지 않는다.
