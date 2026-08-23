# shared 폴더 규칙

> 최종 업데이트: 2026-08-23 · 대상: `Assets/Scripts_Client/Editor/shared/`

**여러 에디터 툴이 함께 쓰는 헬퍼·인프라를 두는 곳.** 한 기능에만 쓰이는 것은
그 기능 폴더(`scene-copy/`·`server-console/`·`memory-meter/`)에 둔다.
폴더 전체에 걸리는 규칙은 [`Editor 규칙.md`](<../Editor 규칙.md>)에 있다.

## 지금 있는 것

| 파일 | 하는 일 |
|------|---------|
| `EditorGit.cs` | 에디터 툴 공용 git 실행 헬퍼(`Run`·`LatestCommitOf`). 씬 복사기·최신성 검사기가 함께 쓴다 |
| `ProjectPreferences.cs` | 환경 설정 목록 **맨 위**에 오는 이 프로젝트 전용 설정 그룹의 뿌리(경로 접두사 `RootPath` + 그룹 페이지) |
| `EditorIcons.cs` | 유니티 내장 아이콘을 이름으로 찾아 캐시한다(`Get`). 툴바 버튼 3개가 함께 쓰며, 이름 상수도 여기 모은다 |

---

## 환경 설정에 새 항목 추가하기

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

## ⚠️ 아이콘 이름에 공백을 넣지 않는다

**`EditorGUIUtility.FindTexture`는 공백이 든 아이콘 이름을 못 찾는다**(`SceneAsset Icon` → null).
`EditorIcons`의 이름 상수는 점으로 이어진 이름(`Profiler.Memory` 꼴)만 쓴다.
