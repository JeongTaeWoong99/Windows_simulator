# Common 규칙

> 최종 업데이트: 2026-08-26 (자산별 폴더 구조로 개편 · 자산 3종 이관 · 자산 문서가 따라간다) · 대상: `Assets/Scripts_Client/Common/`

**이 폴더는 이 프로젝트의 것이 아니다.** [Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit)이라는
별도 저장소에서 관리하는 범용 코드의 **사본**이고, 스킬 두 개가 심고 되돌린다.

| 항목 | 값 |
|------|-----|
| 마스터 원본 | `~/.claude/skills/unity-project-setup/templates/code/Common/` |
| 마스터 저장소 | `C:\Users\ASUS\.claude\skills` (이 폴더 자체가 git 저장소다) |
| 심는 스킬 | `/unity-project-setup` — 새 프로젝트에 복사 |
| 되돌리는 스킬 | `/unity-skill-sync` — 여기서 개선한 것을 마스터로 |

---

## 1. ⚠️ 여기에 함부로 파일을 만들지 않는다

새 스크립트를 만들기 전에 **반드시** 아래를 통과해야 한다.

```
1) 이 코드가 이 게임을 아는가?
   패킷(MikaProtocol)·테이블(GameData)·화면 이름·기획 용어를 참조하는가?
   → 하나라도 예 = Common에 둘 수 없다. 해당 기능 폴더로.

2) 어느 유니티 프로젝트에 그대로 옮겨도 쓸모가 있는가?
   → 아니오 = Common에 둘 수 없다.

3) 둘 다 통과했는가?
   → 그래도 바로 만들지 않는다. 사용자에게 승인을 받는다.
```

**왜 승인이 필요한가** — 여기 파일을 하나 만들면 그건 이 프로젝트의 결정이 아니라
**앞으로 만들 모든 유니티 프로젝트의 결정**이 된다. 마스터로 올라가면 다음 프로젝트에 자동으로 딸려간다.
되돌리기가 비싸므로 들어가는 문턱을 높인다.

### 경계의 실례 — `ClientLogger`는 왜 `Log/`에 있나

로거는 어느 프로젝트에나 있을 법한 코드다. 그런데 `ClientLogger`는 `PacketId`(이 게임의 패킷 열거형)와
`[↑송신]`·`[↓수신]` 같은 **이 게임의 태그 약속**을 안다. 1번에서 걸린다.

`Services`·`MonoService`는 반대다. 무엇을 등록하든 상관하지 않는다 — 그래서 여기 있다.

**같은 잣대로 갈라진 이웃** — `UI/Layout/`에 있던 넷 중 `FlexibleGridLayoutGroup`·`SquareLayoutElement`는
여기로 왔고, `WidgetPositionLayout`(위젯 열의 2:1 비율)·`WindowDragArea`(`WindowManager` 참조)는 남았다.

---

## 2. 폴더 구조 — 자산 하나 = 폴더 하나

> 2026-08-26 개편. 이전에는 `Attribute/`·`Editor/`·`Extensions/`·`Service/`라는
> **런타임 종류 축**으로 나눴는데, 그러면 자산 하나(CenterHeader)가 두 폴더로 갈라지고
> 자산이 늘수록 한 폴더 안에서 남남인 스크립트들이 섞였다.

```
Common/
├── service-locator/    Services.cs · MonoService.cs
├── mono-extensions/    MonoBehaviourExtensions.cs
├── center-header/      CenterHeaderAttribute.cs
│   └── Editor/         CenterHeaderDrawer.cs
├── hierarchy-styler/
│   └── Editor/         HierarchyPalette.cs · HierarchyStyler.cs · Hierarchy Palette.asset
├── ugui-layout/        FlexibleGridLayoutGroup.cs · SquareLayoutElement.cs
│   └── Editor/         FlexibleGridLayoutGroupEditor.cs
├── editor-shared/      Editor/  EditorGit.cs · EditorIcons.cs · ProjectPreferences.cs
└── memory-meter/       Editor/  EditorMemoryMeter.cs · EditorMemoryToolbarButton.cs
```

- 자산 폴더명은 **영문 소문자 kebab-case**. `Editor`만 Unity 예약어라 PascalCase 그대로다.
- ⚠️ **`Editor/` 중첩은 선택이 아니라 필수다.** Unity는 **정확히 `Editor`라는 이름의 폴더만**
  런타임 빌드에서 제외한다. `center-header/CenterHeaderDrawer.cs`처럼 평평하게 두면
  드로어가 빌드에 섞여 **컴파일이 깨진다.** 어트리뷰트와 짝꿍 드로어의 폴더가 갈라져 있는 이유다.
- **네임스페이스를 두지 않는다.** 전부 글로벌이다 — 프로젝트마다 다른 네임스페이스로 감싸면
  복사할 때마다 손봐야 한다. 프로젝트 코드가 자기 네임스페이스(`DesktopWindowControl.EditorTools`)
  안에서 이 타입들을 부르는 데는 아무 문제가 없다.
- **`Hierarchy Palette.asset`은 여기만 있고 마스터에는 없다.** `.asset`은 스크립트 GUID를
  참조하는데 마스터는 `.meta`를 두지 않으므로, 옮기면 새 프로젝트에서 Missing script가 된다.
  새 프로젝트에서는 `Create > Arca > Hierarchy Palette`로 새로 만든다.

---

## 3. 자산 목록

| 자산 | 폴더 | 내용 |
|------|------|------|
| **ServiceLocator** | `service-locator/` | 역할↔구현 등록·조회. 하드 싱글톤(`X.Inst`) 대체. ⚠️ **조회는 반드시 `Start`** |
| **MonoBehaviourExtensions** | `mono-extensions/` | `RequireRef` — 필수 인스펙터 참조 검증(fail-fast) |
| **CenterHeader** | `center-header/` | 인스펙터 섹션을 가운데 정렬 헤더로 구분. 문구에 `< >`를 넣지 않는다 |
| **HierarchyStyler** | `hierarchy-styler/` | 하이어라키에서 이름 앞 접두 문자(`!`·`@`·`#`)로 줄을 색칠 |
| **uGUI Layout** | `ugui-layout/` | 폭에 맞춰 셀을 역산하는 그리드 · "높이만큼 정사각형". [`ugui-layout 규칙.md`](<ugui-layout/ugui-layout 규칙.md>) |
| **EditorShared** | `editor-shared/` | 에디터 툴 공용 — git 실행 · 아이콘 캐시 · 환경 설정 뿌리. [`editor-shared 규칙.md`](<editor-shared/editor-shared 규칙.md>) |
| **MemoryMeter** | `memory-meter/` | 상단 툴바 메모리 표시 + 정리. [`memory-meter 규칙.md`](<memory-meter/memory-meter 규칙.md>) |

---

## 4. 여기 코드를 고쳤다면

1. **먼저 범용인지 본다.** 이 게임 사정 때문에 고친 거라면 애초에 이 폴더에 있으면 안 되는 코드다
2. 범용 개선이면 `/unity-skill-sync`로 **마스터에 되돌린다.** 안 하면 다음 프로젝트는 낡은 사본을 받는다
3. 주석에 **이 프로젝트의 클래스 이름을 남기지 않는다** — `XxxManager` 같은 자리 표시자를 쓴다
   (실제로 `SessionManager`가 박혀 있다가 그 클래스가 사라진 적이 있다)

> 마스터와 사본은 **양방향으로 어긋날 수 있다.** 다른 프로젝트에서 먼저 반영된 변경이 있을 수 있으므로
> 한쪽을 일방적으로 덮지 않는다. `/unity-skill-sync`가 그런 경우를 **⛔ 충돌**로 잡아
> 줄 단위로 확인을 받는다.

### 프로젝트 이름을 코드에 박지 않는 법

| 자리 | 어떻게 |
|---|---|
| 환경 설정 그룹 이름 · `EditorPrefs` 접두사 | `Application.productName` (`ProjectPreferences`가 그렇게 한다) |
| 어트리뷰트 인자 (`[MainToolbarElement]` · `[CreateAssetMenu]`) | ⚠️ **컴파일 타임 상수만 받으므로 `productName`을 못 쓴다.** 툴킷 브랜드 `Arca/`로 통일한다 |

---

## 5. 자산 문서(`<폴더명> 규칙.md`)는 마스터로 따라간다

> 2026-08-26 변경. 이전에는 "`Common/`은 md가 따라가지 않으니 설명을 전부 코드 주석에 넣는다"였다.
> 이제 자산 폴더의 규칙 문서도 `/unity-project-setup`이 코드와 함께 복사한다.

- 파일명은 다른 폴더와 같은 **`<폴더명> 규칙.md`** 공식을 지킨다.
- **그래도 코드 주석은 줄이지 않는다.** 문서는 "이 자산 전체가 무엇이고 왜 이런가",
  주석은 "이 줄이 왜 이런가" — 층이 다르다. 여기 코드의 주석 비중이 높은 것은 **의도된 것**이다.
- **자산 문서에서 프로젝트 문서를 상대 경로로 가리키지 않는다.** 프로젝트마다 깊이가 달라
  조용히 깨진다. 스킬을 가리킬 때는 저장소 루트 기준(`.claude/skills/…`)으로 적는다.
