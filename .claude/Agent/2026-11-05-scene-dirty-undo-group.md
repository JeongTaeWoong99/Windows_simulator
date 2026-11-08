---
date: 2026-11-05
title: 재컴파일 뒤 씬 더티의 원인은 현재 Undo 그룹에 남은 기록이다
tags: [client, editor, unity-cli, mcp, scene, undo]
---

# 재컴파일 뒤 씬 더티의 원인

## 목적 / 배경
- 씬을 고치지 않았는데 `*`가 떠서 저장하거나 되돌려야 했다. 처음에는 `Unity_RunCommand`만 범인으로 봤으나, 원인을 정확히 밝힌 뒤 지침을 쓰자는 사용자 요청으로 끝까지 시험했다.
- 지침 → `.claude/skills/client/unity-editor-ops/SKILL.md` 1절.

## 변경 내용
- `unity-editor-ops` — 실행 순위에 `unity cmd eval`을 `RunCommand` 앞에 넣고, 원인·대처 절 추가.
- `unity-ai-assets` — 크레딧 읽기를 `unity cmd eval`로 교체(실행 확인, 잔액 809, 크레딧 소비 없음).
- 코드·씬은 바꾸지 않았다.
- Arca Unity Toolkit 마스터에도 두 스킬의 같은 내용을 반영했다. 마스터의 기존 일반화(절 번호·중립 표현)는 그대로 두고, 추가분도 중립 표현으로 넣었다.

## 원인
1. 자동화가 현재 Undo 그룹에 기록을 남긴다. `RunCommand`는 `ExecutionResult.Start`에서 `Undo.SetCurrentGroupName`을 부르고 다음 그룹으로 넘기지 않는다.
2. `DrivenRectTransformTracker.CanRecordModifications`가 True가 된다. 유니티 소스 주석은 다른 Undo 동작의 일부일 때만 기록하려는 조건이라고 설명한다(case 1268783).
3. 재컴파일 때 켜진 Scrollbar·Slider가 앵커를 다시 쓰고, 이 기록으로 씬이 더티가 된다.

사람이 클릭하면 그룹 번호가 올라가 조건이 꺼진다. 입력이 없는 자동화에서는 남아 있어서, 자동화 뒤에만 재현된다.

## 근거 (`can`은 `CanRecordModifications`를 6000.3.10f1에서 리플렉션으로 읽은 값)

| 재컴파일 전에 한 일 | can | `*` |
|---|---|---|
| 없음 · CLI 조회만 · 창 활성화만 | False | 안 뜸 |
| `Undo.IncrementCurrentGroup`만 | False | 안 뜸 |
| `Undo.SetCurrentGroupName` (CLI) | True | 뜸 — 진짜 컴파일에서도 |
| 코드로 `Selection` 변경 | True | 뜸 |
| `RunCommand` (읽기만) | True | 뜸 (유효 5회) |
| `create_gameobject` + `save_scene` | True | 뜸 — 저장 뒤 다시 켜짐 |
| 실제 Hierarchy 클릭 (사용자) | False | 안 뜸 |
| 코드로 기록 + 실제 클릭 (사용자) | False | 안 뜸 |
| 위 "뜸" 3종 + `IncrementCurrentGroup` (`RunCommand`·`create_gameobject`+저장·`Selection`) | False | 안 뜸 |
| `SetCurrentGroupName` + 플레이 왕복 (1회) | True | 안 뜸 |

재컴파일 없이 `Scrollbar.UpdateVisuals`만 직접 호출한 대조: 기록 없음 → 안 뜸, 기록 있음 → 뜸, 기록 있음 + `StopRecordingUndo` → 안 뜸.

## 주요 결정 / 근거
- **대처는 `Undo.IncrementCurrentGroup`.** 사람 클릭과 같은 효과이고, 호출 뒤에도 `PerformUndo`가 동작했다.
- **기각:** `Undo.ClearAll`(되돌리기 기록 삭제), `StopRecordingUndo` 상시 적용(전역이고 코드 리로드 때 초기화됨), UI 오브젝트 끄기·자동 더티 해제 스크립트(불필요).
- **`RunCommand`는 금지하지 않고 순위만 내렸다.** `eval`이 런타임 필드를 빈 값으로 읽는 경우가 있어(10/22 로그) 남긴다.
- **씬 정리 커밋을 따로 하지 않는다.** 자동화 쪽 원인을 막을 수 있고 사람 클릭으로는 켜지지 않았다. 다음에 씬을 실제로 고쳐 저장할 때 함께 들어간다.

## 후속 작업 / 주의사항
- ⚠️ **HEAD 씬의 1,185줄:** 구동 값이 숫자로 들어 있어 어떤 경로로 저장해도 같은 1,185줄이 바뀐다(`save_scene`·`eval`·`RunCommand`·에디터 저장 결과 동일). 정리된 파일은 이후 저장해도 바이트가 같다. 다음 씬 커밋 때 이 diff가 섞이니 플레이로 화면을 확인한다.
- **미확인 — 숫자가 처음 들어간 경위.** `1e4191d5`에 많이 들어갔으나 원인은 못 밝혔다. `RunCommand` 저장, 한 호출 안에서 Canvas 토글 뒤 저장으로는 재현되지 않았다. 10/22 로그의 "RunCommand 저장이 값을 굽는다"도 재현되지 않아 정정했다.
- ⚠️ 꺼진 `#Outpost Canvas`를 편집 모드에서 켰다 끄고 저장하면 883줄(프리팹 오버라이드)이 추가되고 다시 저장해도 안 빠진다. HEAD의 1,185줄과는 모양이 달라 같은 원인으로 보지 않는다.
- **미확인:** `HasRecordings`와 씬 더티 처리는 네이티브 코드다. "현재 그룹의 기록"이라는 해석은 위 결과에서 내린 판단이다. 소스 인용은 UnityCsReference `master`이고, 6000.3.10f1의 동작은 위 값으로 대조했다. 사람이 Inspector를 고친 뒤 저장하고 재컴파일하는 경우는 시험하지 않았다.
- `SceneDirtyTracer`를 켜 두면 경고가 `RunCommand`를 실패 응답으로 만든다(실행은 끝난 상태). 평소에는 끈다.
