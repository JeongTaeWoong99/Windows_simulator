---
date: 2026-09-15
title: memory-meter 플레이 중 갱신 정지 · Material 예외 원인 재조사
tags: [client, editor, uitk, memory-meter]
---

# memory-meter 플레이 중 갱신 정지 · Material 예외 원인 재조사

## 목적 / 배경
- 플레이 종료 때 뜨는 `MissingReferenceException: ... 'UnityEngine.Material' has been destroyed`
  (UITK `MeshGenerator.DrawText`)가 거슬린다는 요청. 사용자 제안은 "플레이 중엔 memory-meter를 돌리지 말자".
- [2026-08-31 로그](2026-08-31-uitk-material-missing-reference.md)는 memory-meter의 `MainToolbar.Refresh`를
  원인 후보로 보고 확률만 낮췄다. 이번에 그 인과를 실험으로 확인하려 했다.

## 변경 내용
- `Common/memory-meter/Editor/EditorMemoryToolbarButton.cs` — 플레이 중(진입·종료 과정 포함) `플레이 중` 라벨로
  고정, `ExitingPlayMode`·`EnteredEditMode`에서 다음 갱신 3초 지연.
- `memory-meter 규칙.md` — 위 동작 + **원인 서술 정정**(인과 미확인)과 실험표. 마스터에도 반영.

## 주요 결정 / 근거
- 🔴 **memory-meter가 원인이라는 증거는 없다.** 실험표는 `memory-meter 규칙.md`에 있다. 요지:
  - 매 프레임 `Refresh` 강제 + 종료 순간 `Refresh`로도 **0/5**. 갱신 끔 0/10, 수정 전 동작도 이날은 1/17.
  - 전날(9/14)은 4회 중 3회. 세션마다 빈도가 크게 달라 **"갱신 끔 10회 0회 = 원인 확정"은 착오였다**
    (두 세션을 합친 30%로 계산해 우연 확률을 2%로 과소평가했다).
  - 이날 난 1회는 플레이 중 스크립트가 바뀌어 컴파일이 걸린 채 종료한 판. 같은 조건(컴파일 중 자동 종료)을
    3회 만들었지만 재현 안 됨.
- 그래도 정지 동작은 **유지** — 사용자가 원래 원한 동작이고, 종료 과정의 불필요한 UITK 재생성을 없애는 예방 조치.
  문서에는 "예방 조치, 원인 해결 아님"으로만 적었다.
- 플레이 중 라벨이 한글(`플레이 중`)이라 폴백 폰트 Material을 새로 만들 위험을 짚었으나, 추적 결과
  에디터 폰트(`MALGUN Atlas`) Material은 종료 때 한 번도 파괴되지 않았다.

## 겪은 함정
- **`Editor.log`의 복원 표식은 진입·종료에 두 번 찍힌다.** 종료는 `Loaded scene '…0.backup'` 바로 뒤
  `Unloading N unused Assets`로 센다. 8/31의 "18회 중 2회"는 실제 9회였을 가능성이 크다.
- **Material 추적 시 static에 참조를 들고 있으면 언로드 대상에서 빠져 결과가 바뀐다** — ID·문자열만 저장했다.
  진입 때 도메인 리로드로 static이 날아가므로 기준 목록은 `SessionState`에 둔다.
- 종료 때 파괴되는 Material은 매번 UITK 내부용(`Internal-UIRDefault`·`UIRAtlasBlitCopy`)과 UGUI 마스크
  스텐실 Material. `HideFlags`에 `DontUnloadUnusedAsset`이 있어 **언로드가 아니라 주인이 직접 파괴**한 것 —
  8/31의 "언로드가 폰트 아틀라스 Material을 파괴" 서술은 근거가 없다.
- 에디터 설정 `ScriptCompilationDuringPlay=0`(컴파일하고 계속 플레이). `AssetDatabase.Refresh()`로 스크립트를
  바꾸면 **플레이 중에 컴파일이 끝나 버려** "컴파일 걸린 채 종료"가 재현되지 않는다 — 즉시 종료해야 한다.

## 후속 작업 / 주의사항
- 원인 미해결. 다시 파려면 **예외가 실제로 뜬 판**에서 `Application.logMessageReceived`로 그 순간 사라진
  Material을 찍어야 한다(진단 스크립트는 삭제함 — 위 함정대로 다시 만들면 된다).
- 에디터 전용이라 빌드 영향 없음. 뜨면 무시해도 된다.
