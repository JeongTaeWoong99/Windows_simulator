# editor-shared 규칙

> 최종 업데이트: 2026-08-26 (`Editor/shared/` → `Common/editor-shared/` 이관) · 대상: `Common/editor-shared/`

**여러 에디터 툴이 함께 쓰는 헬퍼·인프라.** 어느 유니티 프로젝트에 옮겨도 그대로 쓰인다 —
그래서 프로젝트의 `Editor/`가 아니라 [Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit)
사본인 `Common/` 아래에 있다. Common 전체에 걸리는 규칙은 상위 폴더의 `Common 규칙.md`에 있다(프로젝트에만 있는 문서다).

## 지금 있는 것

| 파일 | 하는 일 |
|------|---------|
| `Editor/EditorGit.cs` | git 실행 헬퍼(`Run`·`LatestCommitOf`). 실패하면 조용히 `null` — 호출측이 git 없이도 진행할 수 있다 |
| `Editor/ProjectPreferences.cs` | 환경 설정 목록 **맨 위**에 오는 이 프로젝트 전용 설정 그룹의 뿌리(`RootPath`·`MenuHint`·`PrefsPrefix`) |
| `Editor/EditorIcons.cs` | 유니티 내장 아이콘을 이름으로 찾아 캐시한다(`Get`). 이름 상수도 여기 모은다 |

---

## 환경 설정에 새 항목 추가하기

프로젝트 전용 설정은 환경 설정 목록 **맨 위** 그룹에 모은다. 그룹 이름은 코드에 박혀 있지 않고
**`Application.productName`**(Player Settings의 Product Name)에서 온다 — 그래서 어느 프로젝트에
복사해도 그 프로젝트 이름으로 뜬다. 새 항목은 이렇게 붙인다.

```csharp
[SettingsProvider]
private static SettingsProvider Create() =>
    new SettingsProvider(ProjectPreferences.RootPath + "/MyThing", SettingsScope.User)
    { label = "내 설정", guiHandler = _ => { /* ... */ } };
```

- 유니티는 **경로 조각으로 정렬하고 `label`로 표시한다**(`SettingsTreeView`).
  `RootPath`의 `__` 접두사는 순전히 정렬용 — 문화권 비교에서 기호가 숫자·문자보다 앞서므로
  유니티 기본 '일반'(경로가 `_General`)보다도 위에 온다. 화면에는 밑줄이 보이지 않는다.
- 값은 `EditorPrefs`에 `ProjectPreferences.PrefsPrefix`를 붙여 담는다.
  `EditorPrefs`는 프로젝트가 아니라 **머신 전역**이라, 접두사가 없으면 다른 프로젝트의
  같은 이름 설정과 값이 섞인다.
  ⚠️ **이미 쓰고 있던 키의 접두사는 바꾸지 않는다** — 바꾸면 사람들이 잡아 둔 설정이 초기화된다.
  이 저장소의 `scene-copy`·`server-console`이 `DWC.`를 계속 쓰는 이유다.
- 환경 설정 창이 열린 채로 스크립트를 컴파일하면 목록이 갱신되지 않는다 — **닫았다 다시 연다.**

## ⚠️ 아이콘 이름에 공백을 넣지 않는다

**`EditorGUIUtility.FindTexture`는 공백이 든 아이콘 이름을 못 찾는다**(`SceneAsset Icon` → null).
`EditorIcons`의 이름 상수는 점으로 이어진 이름(`Profiler.Memory` 꼴)만 쓴다.

`IconContent`가 아니라 `FindTexture`를 쓰는 이유도 있다 — 이름이 틀렸을 때 `IconContent`는
콘솔에 오류를 뱉지만 `FindTexture`는 조용히 `null`을 준다. 아이콘이 없으면 버튼에 텍스트만
나오면 그만이라 조용한 쪽이 맞다.

## 툴바 버튼은 공식 API로만 붙인다

상단 메인 툴바에 얹을 때는 Unity 6.1+ 공식 API **`[MainToolbarElement]`** + `MainToolbarButton`을 쓴다.

⚠️ **리플렉션으로 내부 툴바에 끼우지 않는다.** Unity 6.3부터 '지원되지 않는 요소'로 감지돼
**숨겨진다.** 내부 UI 요소를 리플렉션으로 손대는 것 전반이 같은 부류다
(예: 툴팁 패널 폭 — [`memory-meter 규칙.md`](<../memory-meter/memory-meter 규칙.md>)).

요소 경로의 첫 조각은 툴킷 브랜드 **`Arca/`**로 통일한다. `[MainToolbarElement]`는 어트리뷰트라
**컴파일 타임 상수만** 받으므로 `Application.productName`을 쓸 수 없다 — 그래서 여기만 고정 문자열이다.
