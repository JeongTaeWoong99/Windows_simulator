---
date: 2026-08-28
title: 씬이 저절로 더티가 되는 원인 제거 + 더티 추적 도구 추가
tags: [client, ui, editor]
---

# 씬이 저절로 더티가 되는 원인 제거 + 더티 추적 도구 추가

## 목적 / 배경

씬을 하나도 안 만졌는데 에디터를 열 때·작업 중간중간 씬에 `*`가 붙고, 종료·빌드에서
"저장하시겠습니까"가 떴다. **저장해도 `git diff`가 0**이라 원인이 안 보였다.

핵심은 유니티의 더티 플래그가 **"내용이 달라졌다"가 아니라 "누군가 이 씬을 건드렸다고
신고했다"**는 표시라는 점이다. 신고만 하고 값이 그대로면 저장해도 바이트가 같다.
diff를 아무리 봐도 안 나오는 이유이자, 이 증상의 진단이 어려운 이유다.

## 변경 내용

- `Assets/Scripts_Client/Managers/WindowManager.cs` — `MirrorAnchorToWidgetIfAlive`가
  앵커와 위젯 위치가 **실제로 다를 때만** `SetPosition` + `SetDirty`를 하도록. `OnValidate`의
  `delayCall` 중복 구독도 제거(`-=` 후 `+=`).
- `Assets/Scripts_Client/UI/Layout/WidgetPositionLayout.cs` — 배치 메서드들이 "실제로 뭘 바꿨는가"를
  `bool`로 돌려주고, `Apply`가 그걸 모아 **편집 모드에서는 무변경이면 리빌드도 예약하지 않는다.**
  `MoveToSibling` 헬퍼 신설(이미 그 자리면 `SetSiblingIndex`를 안 부른다).
- `Assets/Scripts_Client/Common/scene-dirty-tracer/` — 신규 진단 도구.
  → [`scene-dirty-tracer 규칙.md`](<../../Assets/Scripts_Client/Common/scene-dirty-tracer/scene-dirty-tracer 규칙.md>)

## 주요 결정 / 근거

**주범은 `WindowManager.OnValidate`였다.** `OnValidate`가 사람의 인스펙터 조작에만 오는
콜백이라고 전제한 코드였는데, 실제로는 **씬 로드 직후·도메인 리로드(스크립트 컴파일)·Undo·
프리팹 리임포트에도 전부 온다.** 그때마다 `EditorUtility.SetDirty`를 조건 없이 불렀고,
`SetDirty`는 값이 정말 바뀌었는지 보지 않는다. 씬을 여는 것만으로 `*`가 붙었다.

**미리보기는 유지하기로 했다.** `[ExecuteAlways]`와 거울질을 걷어내면 더티는 근본적으로
사라지지만, 재생 없이 인스펙터로 6칸 배치를 확인하는 수단을 잃는다. 대신 **"값이 실제로
달라질 때만 씬을 건드린다"**는 가드로 같은 결과를 얻었다.

**`Apply`의 리빌드 생략은 편집 모드에만 적용한다.** `changed || Application.isPlaying`이다.
런타임은 열 폭이 반쯤만 갱신된 A-1 회귀가 났던 자리라, 더티를 줄이자고 재생 중 동작까지
바꾸지 않았다. 편집 모드에는 그 위험이 없다.

**진단 도구가 두 갈래인 이유** — `sceneDirtied`의 스택트레이스만으로는 못 잡는다.
`EditorApplication.delayCall` 안에서 더티가 나면 스택에 원래 호출자가 없고(이번 주범이 정확히
그 경우다), 네이티브 레이아웃 리빌드가 원인이면 관리 코드가 아예 안 찍힌다.
그래서 `ObjectChangeEvents.changesPublished`로 "무엇이 바뀌었나"를 함께 본다.

## 후속 작업 / 주의사항

- ⚠️ **`bool` 누적은 전부 `|=`다. `||`로 바꾸면 앞이 참인 순간 뒤 배치가 통째로 건너뛰어진다.**
  세 열 중 하나만 정렬이 바뀌고 나머지는 옛 값으로 남는 조용한 버그가 된다. 주석으로 박아 뒀다.
- ⚠️ `sceneDirtied`는 씬마다 **`깨끗 → 더티` 전환에서 한 번만** 온다. 추적기를 켠 뒤에는
  반드시 씬을 저장해 깨끗하게 만들고 재현해야 한다.
- **`Common/`은 [Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit)의 사본이다.**
  `scene-dirty-tracer`는 이번엔 **프로젝트 사본에만** 넣었다. 마스터 반영은 `/unity-skill-sync`로
  따로, 사용자 확인을 받고 한다.
- 검증은 유니티 MCP로 여기까지 확인했다 — 도메인 리로드 직후 씬 clean · `Apply()` 5회 반복
  호출 후에도 clean · `setStartAnchor`와 위젯 `Position`이 `LowerLeft`로 일치.
  **에디터 재기동·Game 뷰 해상도 변경·앵커 변경 시 미리보기 추종은 사람이 확인해야 한다.**

## 업데이트 (2026-08-28) — obsolete API 대응 + 마스터 동기화

- **CS0618 해소** — `EditorUtility.InstanceIDToObject`가 유니티 6.3에서 obsolete가 됐다.
  `#if UNITY_6000_3_OR_NEWER`로 `EntityIdToObject`와 갈랐다.
  ⚠️ **한쪽으로 정리하면 안 된다** — 새 이름은 6.3 **이전에는 아예 없어서**, 신 API만 남기면
  구버전 프로젝트에서 컴파일이 깨진다. 마스터에 올라간 자산이라 여러 버전을 상대한다.
  심볼이 실제로 정의되는지는 임시 스크립트를 프로젝트 어셈블리에 넣어 확인했다
  (MCP `RunCommand`의 동적 어셈블리에서는 버전 심볼이 안 잡혀 판별에 쓸 수 없다).
- **`Common/` 배치 재확인** — `Common 규칙.md` 1절 관문을 다시 통과시켰다.
  게임 요소 참조 0 · 외부 의존은 같은 Common의 `ProjectPreferences` 하나뿐 · 프로젝트 고유명사 0.
- **마스터 반영 완료** — `~/.claude/skills/unity-project-setup/templates/code/Common/scene-dirty-tracer/`
  (`.meta` 제외) + `templates/code/README.md`의 트리·자산 목록.
  **양쪽 저장소 모두 커밋하지 않았다** — 사용자가 직접 한다.
