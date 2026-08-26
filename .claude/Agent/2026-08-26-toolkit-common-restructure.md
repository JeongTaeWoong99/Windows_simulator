---
date: 2026-08-26
title: Common을 자산별 폴더로 재편하고 범용 자산·스킬을 툴킷 마스터로 승격
tags: [client, editor, ui, docs, infra]
---

# Common 자산별 재편 + Arca Unity Toolkit 승격

## 목적 / 배경

곧 있을 기업 과제(짧은 기간에 기획서 → 게임 구현)에서 `/unity-project-setup` 한 번으로
쓸 만한 환경이 서게 하려고, 이 저장소에 쌓인 범용 자산을 마스터로 끌어올렸다.

착수 시점에 마스터와 사본은 **코드 7개·스킬 5개 전부 diff 0**이었다. 문제는 어긋남이 아니라
**마스터에 아예 없는 자산이 쌓여 있던 것**이다.

함께 정리한 구조 문제 둘 —
① `Common/`이 런타임 종류 축(`Attribute`·`Editor`·`Extensions`·`Service`)으로 나뉘어 자산 하나가
두 폴더로 갈라졌다. ② common 스킬이 여러 사람·여러 프로젝트가 고치는 물건이 되는데,
누가 원작이고 어느 변경이 범용인지 판정할 근거가 문서에 없었다.

## 변경 내용

### 1. `Common/` 자산별 재편 — 자산 하나 = 폴더 하나

```
service-locator/ · mono-extensions/ · center-header/ · hierarchy-styler/
ugui-layout/ · editor-shared/ · memory-meter/     (에디터 전용 파일만 각 자산 안 Editor/)
```

`git mv`로 `.cs` + `.cs.meta`를 함께 옮겨 GUID를 보존했다. 폴더 `.meta`는 유니티가 생성.

### 2. 코드 3건 이관 (프로젝트 → `Common/` → 마스터)

| 자산 | 원위치 |
|---|---|
| `ugui-layout/` (`FlexibleGridLayoutGroup`·`SquareLayoutElement`) | `UI/Layout/` |
| `editor-shared/` (`EditorGit`·`EditorIcons`·`ProjectPreferences`) | `Editor/shared/` |
| `memory-meter/` | `Editor/memory-meter/` |

이관 5개 파일에서 `namespace DesktopWindowControl.EditorTools`를 걷어 글로벌로 바꿨다.
`Editor/scene-copy`·`server-console`은 자기 네임스페이스 안에서 글로벌 타입을 그대로 부른다.

### 3. 신규 클라 스킬 2종

- `client/ugui-layout` — Canvas·LayoutGroup 함정 18종 + 정석 구조 (`Layout 규칙.md`에서 발췌)
- `client/ugui-mvp` — MVP 역할·이름 규칙·부착 위치·폴더 규칙·Presenter 뼈대 (`UI 규칙.md` §0~6에서 발췌)

### 4. 작업관리 스킬 4종 마스터 승격 + 중립화

`agent-log-writer`·`agent-log-reader`·`task-writer`·`task-reader`(원작: 서버 담당 동료).
엑셀 파이프라인·패킷·DB 컬럼·낚시 TID 같은 이 프로젝트 예시를 중립 예시로 바꾸고,
**1인 개발이면 `담당` 필드를 두지 않는다** 분기를 넣었다.

### 5. 공동 소유 안전장치 (3층)

- 스킬 상단 소유 표식 `🔗 공동 소유`(마스터 11개). ⛔ **표식은 마스터에만 둔다** —
  프로젝트 저장소의 스킬은 협업자와 함께 쓰는 파일이라, 내 개인 툴킷 사정을 적으면
  그 사람에게는 뜻 모를 문구가 된다. `/unity-project-setup`이 복사할 때 떼고,
  `/unity-skill-sync`는 이 블록을 **차이로 치지 않는다.**
- `/unity-skill-sync`를 **2분류 → 3분류**로 개정. **⛔ 충돌**(양쪽이 각각 바뀜)은 자동 병합 금지,
  양쪽 diff를 나란히 보이고 줄 단위로 확인받는다. 비교 범위도 스킬 → **스킬 + 범용 코드 + CLAUDE.md**로 확장
- 마스터에 `templates/skills/README.md` 신설 — 공동 소유 규약·원작자 표·불변 규칙

## 주요 결정 / 근거

- **`Editor/` 중첩은 필수다.** Unity는 정확히 `Editor`라는 이름의 폴더만 빌드에서 제외한다.
  자산별로 묶으면서 평평하게 폈다면 드로어·툴이 런타임 빌드에 섞여 깨졌을 것이다.
- **`memory-meter 규칙.md`(237줄)를 지우지 않고 자산 폴더째 옮겼다.** 계획 초안은 삭제였는데,
  워킹셋/커밋/트림·Boehm 비압축·작업 관리자 대조는 전부 범용 지식이라 손실이 컸다.
  → **자산 문서(`<폴더명> 규칙.md`)가 코드와 함께 마스터로 따라가도록 규칙을 바꿨다.**
  이전 규칙("Common은 md가 안 따라가니 설명을 전부 주석에")이 이걸로 완화됐다.
- **`ProjectPreferences`는 `Application.productName` 기반으로 바꿨다.** `const` → `static readonly`.
  ⚠️ 그래도 **`EditorPrefs` 접두사 `DWC.`는 그대로 뒀다** — 바꾸면 사람들이 잡아 둔 설정이 초기화된다.
  새 키만 `PrefsPrefix`를 쓴다.
- **`[MainToolbarElement]`·`[CreateAssetMenu]`는 `productName`을 쓸 수 없다** — 어트리뷰트라
  컴파일 타임 상수만 받는다. 그 자리는 툴킷 브랜드 `Arca/`로 통일했다
  (툴바 요소 경로가 `DesktopWindowControl/메모리 사용량` → `Arca/메모리 사용량`).
- **`Hierarchy Palette.asset`은 마스터에서 제외했다.** `.asset`은 스크립트 GUID를 참조하는데
  마스터는 `.meta`를 두지 않으므로, 복사하면 새 프로젝트에서 Missing script가 된다.
- **`scene-copy`·`server-console`은 이관하지 않았다** — 협업 씬 관습(`Scenes/Original`·`Test Copy`)과
  WSGameServer 경로·포트 10050에 묶여 있다.
- **`excel-table-creator`·`game-design-reference`도 올리지 않았다** — `GameDesign/Excel` 파이프라인과
  `게임기획코어.md` 경로를 걷어내면 남는 게 껍데기다. sync는 **내용으로** 프로젝트 특화를 판정한다.
- **서버 스킬 2종(`server-code-style`·`server-tdd`)은 이번 범위에서 빠졌다.** 마스터 `server/` 슬롯은 계속 비어 있다.
- **프로젝트의 `.claude/skills/common/`은 손대지 않는다.** 서버 담당 동료가 만들고 함께 쓰는 파일이다.
  마스터 판만 범용화했고, 그래서 이 프로젝트와 sync를 돌리면 `agent-log-*`·`task-*` 4종은
  **항상 차이로 잡힌다 — 그게 정상이고 push 하지 않는다**(마스터 `templates/skills/README.md`에 명시).
- **`SceneCopySettings`의 `const` 둘을 `static readonly`로 바꿨다** — `ProjectPreferences.RootPath`가
  `Application.productName`에서 오는 런타임 값이 되면서 컴파일 타임 상수가 될 수 없다(CS0133).

## 후속 작업 / 주의사항

- ⚠️ **아직 커밋하지 않았다** (양쪽 저장소 모두 — 사용자 지시). 커밋 시
  `Server/Shared/game.sqlite3`는 평소대로 제외한다.
- ⚠️ **환경 설정 그룹 이름이 바뀌었다** — `데스크탑 윈도우 컨트롤` → `DesktopWindow_Control`
  (`Player Settings`의 Product Name). 한글 이름을 유지하려면 Product Name 자체를 바꾼다.
- ⚠️ **플레이어 빌드를 아직 돌리지 않았다.** 1단계에서 가장 깨지기 쉬운 지점이
  `Common/*/Editor/`의 빌드 제외이므로, 다음 빌드 때 한 번 확인한다.
  (에디터 컴파일·씬 참조·툴바·환경 설정은 MCP로 검증 완료 — Missing 0/422, GUID 보존)
- 마스터의 `templates/skills/README.md`에 **원작자 표**가 있다. 남이 쓴 스킬을 손댈 때
  의도를 지우지 않았는지 그 표를 먼저 본다.
- `→ tasks/` 등록 일감은 없다 (사용자 직접 지시 작업).
