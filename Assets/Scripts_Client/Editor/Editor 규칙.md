# Editor 폴더 규칙

> 최종 업데이트: 2026-08-13 · 대상: `Assets/Scripts_Client/Editor/`

**이 프로젝트 전용 에디터 툴을 두는 곳.** 폴더 이름이 `Editor`라서 유니티가
자동으로 `Assembly-CSharp-Editor`로 컴파일하고 **런타임 빌드에서 제외**한다.

기능(UI·Log 등)에 종속된 에디터 확장은 그 폴더 안 `Editor/`에 둔다
(`Common/Editor`, `UI/Layout/Editor`처럼). 여기는 **특정 기능에 묶이지 않는
프로젝트 전체 작업용 툴**만 둔다.

---

## 지금 있는 것

| 파일 | 하는 일 |
|------|---------|
| `OriginalSceneCopier.cs` | `Scenes/Original`의 씬을 `Scenes/Test Copy`로 최신본 복사 + README에 이력 기록. 메뉴 `Tools/오리지널 씬 복사`(단축키 `Ctrl+Shift+C`) |
| `OriginalSceneCopyToolbarButton.cs` | 위 동작을 상단 메인 툴바 오른쪽(클라우드 아이콘 옆)에 '오리지널 씬 복사' 버튼으로 얹는다 (리플렉션) |

### 왜 씬을 복사해서 쓰나

원본 씬을 여러 사람이 직접 열어 테스트하면 재직렬화·자동 머지로 참조가 조용히 사라진다
(이슈 #14). **원본은 열지 않고, 복사본(`Test Copy`)에서만 테스트한다.**
복사본은 `.gitignore`에 걸려 커밋되지 않으므로 충돌이 나지 않는다.

- 복사는 `AssetDatabase.CopyAsset`을 쓴다 — 사본에 **새 GUID**가 부여된다.
  `.meta`를 파일로 그대로 복사하면 원본과 GUID가 겹쳐 충돌한다.
- 상단 버튼은 유니티 내부 `UnityEditor.Toolbar`에 리플렉션으로 붙는다.
  **유니티 버전이 오르면 버튼이 안 붙을 수 있다** — 그때도 메뉴·단축키는 동작한다.
