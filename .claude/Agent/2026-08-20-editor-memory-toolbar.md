---
date: 2026-08-20
title: 에디터 메모리 사용량 상단 툴바 표시기 추가
tags: [client, editor]
---

# 에디터 메모리 사용량 상단 툴바 표시기 추가

## 목적 / 배경
- 유니티 에디터가 지금 메모리를 얼마나 쓰는지 곁눈질로 보고 싶다는 요청.
- 유니티 **내장 설정에는 상시 표시가 없다** — Profiler 창/Memory Profiler 패키지를 열어야만 보인다.
  하단 상태 표시줄에 얹는 공식 API도 없어(내부 API뿐) 상단 메인 툴바로 갔다.

## 변경 내용
- `Assets/Scripts_Client/Editor/memory-meter/EditorMemoryMeter.cs` — 수치 수집(`Take`)·정리(`Cleanup`).
- `Assets/Scripts_Client/Editor/memory-meter/EditorMemoryToolbarButton.cs` — 툴바 등록 + 1초 갱신.
- `Assets/Scripts_Client/Editor/Editor 규칙.md` — 폴더 트리·파일 표·설명 절 추가.

## 주요 결정 / 근거
- **표시 값은 `Process.WorkingSet64`.** 작업 관리자의 `Unity.exe`와 같은 값이라 "에디터가 쓰는 메모리"라는
  질문에 그대로 답한다. `Profiler.GetTotalAllocatedMemoryLong()`은 Unity 네이티브 몫만이라 늘 더 작게 나온다
  — 버리지 않고 툴팁에 Unity 할당/예약·Mono 힙과 함께 넣었다.
- **갱신 경로는 `_element.content = ...` + `MainToolbar.Refresh(path)`.** `MainToolbarElement`는
  `VisualElement`가 아니라 **서술자(descriptor)**다. `.text`를 직접 만지는 식은 애초에 불가능하고,
  `Refresh(path)`가 문서에 적힌 공식 갱신 통로다. (`content`는 public setter가 있다 — 메타데이터로 확인.)
- **아이콘은 `EditorGUIUtility.FindTexture("Profiler.Memory")`.** `IconContent`는 이름이 틀리면 콘솔에
  오류를 뱉지만 `FindTexture`는 조용히 null을 준다 — 아이콘이 없으면 텍스트만 나오면 그만이다.
  (이모지 라벨은 후보였다가 뺐다. 에디터 UI 폰트에서 두부(tofu)로 깨질 수 있다.)
- **클릭에 확인 팝업을 두지 않았다.** 씬 복사 버튼과 달리 되돌릴 게 없는 동작이다.

## 후속 작업 / 주의사항
- **`EditorProcess.Refresh()`를 빼면 숫자가 멈춘다** — `Process`가 값을 캐시한다. 반대로 매 갱신마다
  `Process.GetCurrentProcess()`를 새로 부르면 핸들이 샌다. 지금 구조(static 1개 + 매번 Refresh)를 유지한다.
- `MainToolbar.Refresh`가 내부적으로 팩토리(`Create`)를 다시 부를 수 있다. 그래서 `Create`의 구독은
  `-=` 후 `+=`이고, 표시 내용은 항상 호출 시점 수치로 새로 만든다. **여기에 상태를 쌓으면 안 된다.**
- 갱신 주기 1초는 상수(`RefreshPeriod`)다. 툴바가 깜빡이면 이 값을 올리거나 `Refresh` 호출을 빼고
  `content` 대입만 남겨 보는 게 첫 시도다.
- Right 도크 인덱스 2를 쓴다 — 씬 복사(0)·서버 콘솔(1) 뒤. 툴바 버튼을 더 붙이면 이어서 3.
- 아직 **에디터에서 실행 검증은 안 됐다**(Unity 번들 Roslyn으로 컴파일만 통과). `.meta`도 미생성.

## 업데이트 (2026-08-20)

에디터에서 확인해 보니 버튼이 계속 **`0 B`** 였다. 그리고 수치의 의미가 안 읽힌다는 피드백.

- **원인: `Process.WorkingSet64`가 유니티의 Mono에서 현재 프로세스에 대해 0을 돌려준다.**
  `Refresh()`를 불러도 마찬가지다. 이건 `Process` 클래스 구현 문제라 코드로 우회할 수 없다 —
  `psapi.dll`의 `GetProcessMemoryInfo`(P/Invoke)로 갈아탔다. **다시 `Process`로 되돌리지 말 것.**
  나머지 `Profiler.*` 수치는 정상이었으므로(툴팁에 값이 떴다) 1초 갱신 경로 자체는 문제없었다.
- **툴팁을 문장으로 풀었다.** '할당/예약'이라는 단어만으로는 뜻이 안 통한다는 지적 —
  `쓰는 중 / 미리 잡아 둔 자리`로 적고, **세 항목의 합이 전체와 다른 이유**(에디터 UI·플러그인·DLL 등
  프로파일러가 세지 않는 몫)를 툴팁 안에 적어 뒀다. 실측: 전체 4.7 GB vs 네이티브 0.76 GB.
- `Profiler.GetAllocatedMemoryForGraphicsDriver()`를 항목에 추가했다(전체와의 격차 설명에 필요).
- **아이콘은 `Editor/shared/EditorIcons.cs`로 모았다** — 툴바 버튼 3개가 함께 쓴다.
  스킨(밝게/어둡게)이 바뀌면 캐시한 텍스처가 '가짜 null'이 되므로 `== null`로 검사해 다시 찾는다.
  씬 복사 = `SceneAsset Icon`, 서버 콘솔 = `UnityEditor.ConsoleWindow`, 메모리 = `Profiler.Memory`.

## 업데이트 2 (2026-08-20)

- **툴팁 용어를 전문 용어로.** '쓰는 중 / 미리 잡아 둔 자리' → **`실제 사용량 / 예약(Reserved)`**.
  예약이 왜 안 줄어드는지도 한 줄로 못 박았다 — **Mono의 GC(Boehm)가 비압축(non-compacting)이라
  힙을 OS에 반납할 수 없다. 에디터 재시작이 유일한 방법이다.** (사용자 질문: 버튼으로 강제 해제 가능한가 → 불가)
- ⚠️ **`EditorGUIUtility.FindTexture`는 공백이 든 아이콘 이름을 못 찾는다**(실측 — `SceneAsset Icon`이
  조용히 null이라 씬 복사 버튼에만 아이콘이 안 떴다). 점으로 이어진 이름(`TreeEditor.Duplicate`,
  `UnityEditor.ConsoleWindow`, `Profiler.Memory`)은 정상. **아이콘 상수를 추가할 때 공백 이름을 쓰지 말 것.**
