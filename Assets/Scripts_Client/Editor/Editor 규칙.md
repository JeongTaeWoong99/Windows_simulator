# Editor 폴더 규칙

> 최종 업데이트: 2026-08-23 (하위 폴더별 규칙 문서로 분할) · 대상: `Assets/Scripts_Client/Editor/`

**이 프로젝트 전용 에디터 툴을 두는 곳.** 폴더 이름이 `Editor`라서 유니티가
자동으로 `Assembly-CSharp-Editor`로 컴파일하고 **런타임 빌드에서 제외**한다.

기능(UI·Log 등)에 종속된 에디터 확장은 그 폴더 안 `Editor/`에 둔다
(`Common/Editor`, `UI/Layout/Editor`처럼). 여기는 **특정 기능에 묶이지 않는
프로젝트 전체 작업용 툴**만 둔다.

## 폴더 구성 — 기능별 하위 그룹

툴이 늘면서 기능별 하위 폴더로 묶는다(폴더명은 kebab-case). **Editor 폴더의 하위 폴더도 전부
`Assembly-CSharp-Editor`로 컴파일되므로** 빌드·네임스페이스에 영향이 없다. 네임스페이스는 위치와
무관하게 `DesktopWindowControl.EditorTools`로 통일한다.

**툴마다 자기 폴더에 규칙 문서를 둔다.** 이 문서는 폴더 전체에 걸리는 것만 담는다 —
특정 툴의 동작·함정은 그 폴더 문서에서 본다.

| 하위 폴더 | 무엇을 | 문서 |
|-----------|--------|------|
| `shared/` | 여러 기능이 공유하는 헬퍼·인프라 (git 실행 · 환경 설정 뿌리 · 아이콘) | [`shared 규칙.md`](<shared/shared 규칙.md>) |
| `scene-copy/` | 오리지널 씬을 `Test Copy`로 복사 + 낡음 알림 | [`scene-copy 규칙.md`](<scene-copy/scene-copy 규칙.md>) |
| `server-console/` | 에디터에서 서버 실행/종료 + 로그 보기 | [`server-console 규칙.md`](<server-console/server-console 규칙.md>) |
| `memory-meter/` | 에디터 메모리 사용량을 상단 툴바에 실시간 표시 | [`memory-meter 규칙.md`](<memory-meter/memory-meter 규칙.md>) |

---

## 툴바 버튼은 공식 API로만 붙인다

지금 상단 메인 툴바에 버튼을 얹는 툴이 셋(`scene-copy`·`server-console`·`memory-meter`)이다.
셋 다 Unity 6.1+ 공식 API **`[MainToolbarElement]`**로 붙인다.

- ⚠️ **리플렉션으로 내부 툴바에 끼우지 않는다.** Unity 6.3부터 '지원되지 않는 요소'로
  감지돼 **숨겨진다.** 내부 UI 요소를 리플렉션으로 손대는 것 전반이 같은 부류다
  (예: 툴팁 패널 폭 — `memory-meter 규칙.md`).
- 아이콘은 `shared/EditorIcons.cs`를 거쳐 가져온다. 이름 상수도 거기 모은다.

## 개인 설정은 `EditorPrefs`에 둔다

툴의 On/Off 같은 사람별 설정은 `편집 > 환경 설정 > 데스크탑 윈도우 컨트롤` 아래에 모으고
값은 `EditorPrefs`(머신 로컬)에 담는다 — **커밋되지 않으므로 협업자끼리 서로의 설정에
영향을 주지 않는다.** 새 항목을 붙이는 절차는 [`shared 규칙.md`](<shared/shared 규칙.md>)의
"환경 설정에 새 항목 추가하기" 절에 있다.
