# Editor 폴더 규칙

> 최종 업데이트: 2026-09-17 (`cheat-console/` 추가) · 대상: `Assets/Scripts_Client/Editor/`

**이 프로젝트 전용 에디터 툴을 두는 곳.** 폴더 이름이 `Editor`라서 유니티가
자동으로 `Assembly-CSharp-Editor`로 컴파일하고 **런타임 빌드에서 제외**한다.

기능(UI·Log 등)에 종속된 에디터 확장은 그 폴더 안 `Editor/`에 둔다
(`Common/center-header/Editor` 처럼). 여기는 **특정 기능에 묶이지 않는
프로젝트 전체 작업용 툴**만 둔다.

⚠️ **어느 유니티 프로젝트에 옮겨도 쓸모가 있는 툴이면 여기가 아니라 `Common/`이다.**
2026-08-26에 `shared/`(git·아이콘·환경 설정)와 `memory-meter/`가 그 이유로 내려갔다.
경계 판정은 [`Common 규칙.md`](<../Common/Common 규칙.md>) 1절의 관문을 쓴다.

## 폴더 구성 — 기능별 하위 그룹

툴이 늘면서 기능별 하위 폴더로 묶는다(폴더명은 kebab-case). **Editor 폴더의 하위 폴더도 전부
`Assembly-CSharp-Editor`로 컴파일되므로** 빌드·네임스페이스에 영향이 없다. 네임스페이스는 위치와
무관하게 `DesktopWindowControl.EditorTools`로 통일한다.

**툴마다 자기 폴더에 규칙 문서를 둔다.** 이 문서는 폴더 전체에 걸리는 것만 담는다 —
특정 툴의 동작·함정은 그 폴더 문서에서 본다.

| 하위 폴더 | 무엇을 | 문서 |
|-----------|--------|------|
| `scene-copy/` | 오리지널 씬을 `Test Copy`로 복사 + 낡음 알림 (동작만 — 버튼은 치트 창 도구 줄) | [`scene-copy 규칙.md`](<scene-copy/scene-copy 규칙.md>) |
| `server-console/` | 에디터에서 서버 실행/종료 + 로그 보기 (여는 버튼은 치트 창 도구 줄) | [`server-console 규칙.md`](<server-console/server-console 규칙.md>) |
| `cheat-console/` | 서버 치트를 한 창에 모아 보내기 + 안전장치 팝업. **프로젝트 전용 버튼(씬 복사·서버 콘솔)도 이 창 도구 줄에 모은다** | [`cheat-console 규칙.md`](<cheat-console/cheat-console 규칙.md>) |

**메인 툴바에 올리는 프로젝트 전용 요소는 '치트' 버튼 하나다.** 새 툴이 버튼을 원하면 툴바가 아니라 치트 창 도구 줄에 붙인다.

**공용 헬퍼는 `Common/editor-shared/`에 있다** — git 실행(`EditorGit`) · 내장 아이콘 캐시(`EditorIcons`) ·
환경 설정 뿌리(`ProjectPreferences`). 툴바 버튼·아이콘·환경 설정 항목을 붙이는 절차는
[`editor-shared 규칙.md`](<../Common/editor-shared/editor-shared 규칙.md>)에 있다.

여기 툴들은 자기 네임스페이스 안에서 그 글로벌 타입들을 그대로 부른다 — 별도 `using`이 필요 없다.

---

## 이 폴더에만 걸리는 것

- **`EditorPrefs` 키 접두사는 `DWC.`를 유지한다.** `ProjectPreferences.PrefsPrefix`가
  `Application.productName` 기반으로 바뀌었지만, **이미 쓰던 키를 바꾸면 사람들이 잡아 둔
  설정(씬 복사 자동 알림 토글 등)이 초기화된다.** 새로 만드는 키만 `PrefsPrefix`를 쓴다.
- 툴바 요소 경로의 첫 조각은 툴킷 브랜드 **`Arca/`**로 통일한다 —
  `[MainToolbarElement]`는 어트리뷰트라 컴파일 타임 상수만 받아 `productName`을 쓸 수 없다.
- ⚠️ **툴바 요소를 새로 추가하면 기존 사용자 화면에는 숨겨진 채로 들어온다**(2026-09-17 실측 — 추가한 토글·치트 버튼이
  렌더링되지 않았다). 툴바 빈 곳 **우클릭**(또는 `⋮`) → 목록에서 이름(`치트`)을 체크해야 보인다. 코드로 켜는 공개 API는 없다.
  못 찾으면 메뉴 `Window > DesktopWindowControl > 치트`로도 연다.
