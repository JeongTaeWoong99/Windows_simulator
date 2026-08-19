# Editor 폴더 규칙

> 최종 업데이트: 2026-08-20 · 대상: `Assets/Scripts_Client/Editor/`

**이 프로젝트 전용 에디터 툴을 두는 곳.** 폴더 이름이 `Editor`라서 유니티가
자동으로 `Assembly-CSharp-Editor`로 컴파일하고 **런타임 빌드에서 제외**한다.

기능(UI·Log 등)에 종속된 에디터 확장은 그 폴더 안 `Editor/`에 둔다
(`Common/Editor`, `UI/Layout/Editor`처럼). 여기는 **특정 기능에 묶이지 않는
프로젝트 전체 작업용 툴**만 둔다.

## 폴더 구성 — 기능별 하위 그룹

툴이 늘면서 기능별 하위 폴더로 묶는다(폴더명은 kebab-case). **Editor 폴더의 하위 폴더도 전부
`Assembly-CSharp-Editor`로 컴파일되므로** 빌드·네임스페이스에 영향이 없다. 네임스페이스는 위치와
무관하게 `DesktopWindowControl.EditorTools`로 통일한다. 하위 폴더별 개별 `규칙.md`는 두지 않고
이 문서가 그룹별로 설명한다.

```
Editor/
  shared/           여러 기능이 공유하는 헬퍼·인프라
  scene-copy/       오리지널 씬 복사 기능
  server-console/   에디터에서 서버 실행/종료 + 로그 보기
  memory-meter/     에디터 메모리 사용량을 상단 툴바에 실시간 표시
```

---

## 지금 있는 것

### `shared/` — 공용

| 파일 | 하는 일 |
|------|---------|
| `EditorGit.cs` | 에디터 툴 공용 git 실행 헬퍼(`Run`·`LatestCommitOf`). 씬 복사기·최신성 검사기가 함께 쓴다 |
| `ProjectPreferences.cs` | 환경 설정 목록 **맨 위**에 오는 이 프로젝트 전용 설정 그룹의 뿌리(경로 접두사 `RootPath` + 그룹 페이지) |
| `EditorIcons.cs` | 유니티 내장 아이콘을 이름으로 찾아 캐시한다(`Get`). 툴바 버튼 3개가 함께 쓰며, 이름 상수도 여기 모은다 |

### `scene-copy/` — 오리지널 씬 복사

| 파일 | 하는 일 |
|------|---------|
| `OriginalSceneCopier.cs` | `Scenes/Original`의 씬을 `Scenes/Test Copy`로 최신본 복사 + README에 이력 기록 + `.copy-state`에 씬별 최신 커밋 해시 기록 (동작만, UI 없음) |
| `OriginalSceneCopyToolbarButton.cs` | 위 동작을 상단 메인 툴바 오른쪽(클라우드 아이콘 옆)에 '오리지널 씬 복사' 버튼으로 얹는다 (공식 `[MainToolbarElement]` API) |
| `SceneCopyFreshnessChecker.cs` | 유니티로 포커스가 돌아올 때 복사본이 오리지널 최신 커밋보다 낡았는지 검사해 `[지금 복사]` 팝업을 띄운다 |
| `SceneCopySettings.cs` | 자동 알림 On/Off 개인 설정(`EditorPrefs`) + 위 그룹 아래 '오리지널 씬 복사' 토글 UI |

### `server-console/` — 서버 실행 콘솔

| 파일 | 하는 일 |
|------|---------|
| `ServerRunner.cs` | WSGameServer를 백그라운드로 켜고/끄는 프로세스 제어기(UI 없음). PID는 `SessionState`, 로그는 `Temp/WSGameServer.log`로 리다이렉트 |
| `ServerConsoleWindow.cs` | 실행/종료 토글 + 로그 파일을 tail 해 터미널처럼 보여주는 `EditorWindow` |
| `ServerConsoleToolbarButton.cs` | 위 창을 여는 상단 메인 툴바 '서버 콘솔' 버튼 (`[MainToolbarElement]`) |

### `memory-meter/` — 메모리 사용량 표시

| 파일 | 하는 일 |
|------|---------|
| `EditorMemoryMeter.cs` | 지금 쓰는 메모리를 읽고(`Snapshot`) 정리(`Cleanup`)한다 (동작만, UI 없음) |
| `EditorMemoryToolbarButton.cs` | 위 수치를 상단 메인 툴바 오른쪽에 1초마다 갱신해 표시한다 (`[MainToolbarElement]`) |

### 왜 씬을 복사해서 쓰나

원본 씬을 여러 사람이 직접 열어 테스트하면 재직렬화·자동 머지로 참조가 조용히 사라진다
(이슈 #14). **원본은 열지 않고, 복사본(`Test Copy`)에서만 테스트한다.**
복사본은 `.gitignore`에 걸려 커밋되지 않으므로 충돌이 나지 않는다.

- 복사는 `AssetDatabase.CopyAsset`을 쓴다 — 사본에 **새 GUID**가 부여된다.
  `.meta`를 파일로 그대로 복사하면 원본과 GUID가 겹쳐 충돌한다.
- 상단 버튼은 Unity 6.1+ 공식 API `[MainToolbarElement]`로 붙인다.
  리플렉션으로 내부 툴바에 끼우면 **Unity 6.3부터 '지원되지 않는 요소'로 감지돼 숨겨진다.**
  진입점은 이 툴바 버튼 하나뿐이다 — 메뉴 항목·단축키는 두지 않는다.
- 버튼을 누르면 바로 복사하지 않고 **`[지금 복사]/[나중에]` 확인 팝업**을 거친다
  (실수로 눌러도 되돌릴 수 있게). 검사기 팝업의 '지금 복사'는 이미 확인했으므로 `Copy()`를 바로 부른다.

### 복사본이 낡았는지 어떻게 아나

풀만 받고 복사 버튼을 눌렀는지 신경 쓰지 않아도 되게, **복사 시점에 씬마다 그 씬을 마지막으로
건드린 커밋 해시**를 `Test Copy/.copy-state`(폴더가 `.gitignore`라 로컬 전용, `.`로 시작해 Unity가
무시)에 적어 둔다. `SceneCopyFreshnessChecker`가 **유니티로 포커스가 돌아올 때** 오리지널 씬의
현재 최신 해시를 다시 뽑아 비교해, 다르면 `[지금 복사]` 팝업을 띄운다.

- 커밋된 이력만 본다 — 오리지널을 편집하고 **아직 커밋 안 한** 사람에겐 뜨지 않는다(정상).
- git이 없거나 저장소가 아니면 조용히 넘어간다(오탐 없음). 같은 상태로는 세션 중 다시 잔소리하지 않는다.
- **예외 케이스는 검사기 쪽에서 팝업을 띄우지 않고 넘긴다** — 아직 한 번도 복사 안 함(`.copy-state` 없음),
  오리지널 폴더가 사라짐, 검사 중 예외(이건 `Debug.LogWarning`만). 포커스마다 도는 자리라 조용함이 원칙이다.
  (복사 버튼 쪽은 다르다 — 원본 폴더가 없거나 `.unity`가 없으면 그 자리에서 안내 팝업을 띄운다.)

### 자동 알림 켜고 끄기

이 알림이 실제로 필요한 건 **오리지널 씬을 받아 쓰는 쪽(서버 작업)**이다. 씬을 직접 만드는
쪽(클라 작업)에는 자기 커밋에 대한 잔소리가 되므로, **사람별로 끌 수 있게** 해 뒀다.

- `편집 > 환경 설정 > 데스크탑 윈도우 컨트롤 > 오리지널 씬 복사`의 **복사본 최신성 자동 알림** 토글.
  **기본은 켜짐**이라 새로 받은 사람은 지금까지대로 알림을 받는다.
- 낡음 팝업의 세 번째 버튼 **`[다시 알리지 않기]`**로 그 자리에서 끌 수 있다(다시 켜는 위치를 안내한다).
- 꺼지는 건 **자동 검사·팝업뿐**이다. 상단 툴바의 '오리지널 씬 복사' 버튼은 그대로 남아 언제든 수동 복사할 수 있다.
- 설정은 `EditorPrefs`(머신 로컬)에 담겨 **커밋되지 않는다** — 협업자끼리 서로의 설정에 영향을 주지 않는다.

## 서버 콘솔 (`server-console/`)

클라 작업 중 서버를 확인하려고 매번 터미널에서 `dotnet run`을 치던 걸, **상단 '서버 콘솔' 버튼 →
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
- `Temp/`는 `.gitignore`라 로그 파일은 커밋되지 않는다. 서버 실행 중 서버 측 재빌드는 DLL 잠금(MSB3021)을
  유발하므로, 이 툴은 실행/종료만 하고 빌드에는 관여하지 않는다.

## 메모리 사용량 표시 (`memory-meter/`)

유니티에는 **메모리를 상시로 보여 주는 내장 설정이 없다** — Profiler 창이나 Memory Profiler
패키지를 열어야만 보인다. 그래서 상단 툴바에 숫자 하나를 얹어 곁눈질로 확인할 수 있게 했다.
(하단 상태 표시줄에 얹는 공식 API는 없다. 내부 API뿐이라 쓰지 않는다.)

- **버튼에 보이는 값은 프로세스의 워킹셋** — 작업 관리자의 `Unity Editor` 행과 같은 값이라 가장 직관적이다.
  ⚠️ **`System.Diagnostics.Process.WorkingSet64`는 유니티의 Mono에서 현재 프로세스에 대해 0을 준다**(실측).
  그래서 `psapi.dll`의 `GetProcessMemoryInfo`를 P/Invoke로 직접 부른다. Windows 전용이며,
  실패하면 경고 한 번을 남기고 0으로 표시한다.
- **툴팁은 세 갈래로 쪼개 보여 준다** — 네이티브(에셋·씬) · Mono 힙(C# 스크립트) · 그래픽 드라이버.
  각각 `실제 사용량 / 예약(Reserved)`으로 적는다.
  **이 셋의 합은 전체보다 작다** — 에디터 UI·플러그인·DLL 등 프로파일러가 세지 않는 몫이 있어서다.
- **예약(Reserved)은 줄일 수 없다.** 유니티의 GC는 Boehm 기반이라 **비압축(non-compacting)** —
  객체를 회수해도 힙을 압축해 OS에 반납하지 못한다. 정리 버튼이 줄이는 건 '실제 사용량'뿐이고,
  예약을 되돌리려면 **에디터를 재시작**해야 한다. 툴팁에도 이 문장을 적어 뒀다.
- ⚠️ **`EditorGUIUtility.FindTexture`는 공백이 든 아이콘 이름을 못 찾는다**(`SceneAsset Icon` → null).
  아이콘 상수는 점으로 이어진 이름(`Profiler.Memory` 꼴)만 쓴다.
- **갱신은 `EditorApplication.update`에서 1초에 한 번**만 한다. 값을 바꾼 뒤
  `MainToolbar.Refresh(path)`로 툴바에 알리는 게 공식 갱신 경로다.
  도메인 리로드마다 팩토리 메서드가 다시 불리므로 구독은 `-=` 후 `+=`로 건다.
- **클릭하면** `EditorUtility.UnloadUnusedAssetsImmediate()` + `GC.Collect()`로 정리하고
  `[메모리] 전 → 후 (차이)`를 콘솔에 남긴다. 확인 팝업은 없다(되돌릴 게 없는 안전한 동작).
  에디터에서 `Resources.UnloadUnusedAssets()`는 비동기라 결과를 바로 못 재므로 즉시판을 쓴다.

### 환경 설정에 새 항목 추가하기

이 프로젝트 전용 설정은 환경 설정 목록 **맨 위**의 '데스크탑 윈도우 컨트롤' 그룹에 모은다
(`ProjectPreferences.cs`). 새 항목은 이렇게 붙인다.

```csharp
[SettingsProvider]
private static SettingsProvider Create() =>
    new SettingsProvider(ProjectPreferences.RootPath + "/MyThing", SettingsScope.User)
    { label = "내 설정", guiHandler = _ => { /* ... */ } };
```

- 유니티는 **경로 조각으로 정렬하고 `label`로 표시한다**(`SettingsTreeView`).
  `RootPath`의 `__` 접두사는 순전히 정렬용 — 문화권 비교에서 기호가 숫자·문자보다 앞서므로
  유니티 기본 '일반'(경로가 `_General`)보다도 위에 온다. 화면에는 밑줄이 보이지 않는다.
- 값은 `EditorPrefs`에 `DWC.` 프리픽스를 붙여 담는다(`EditorPrefs`는 프로젝트가 아니라 머신 전역이다).
- 환경 설정 창이 열린 채로 스크립트를 컴파일하면 목록이 갱신되지 않는다 — **닫았다 다시 연다.**
