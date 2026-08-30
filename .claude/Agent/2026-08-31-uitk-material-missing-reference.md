---
date: 2026-08-31
title: 플레이 종료 때 뜨던 UITK Material MissingReferenceException
tags: [client, editor, uitk, memory-meter]
---

# 플레이 종료 때 뜨던 UITK Material MissingReferenceException

## 증상

에디터 콘솔에 두 줄이 짝지어 떴다.

```
MissingReferenceException: The object of type 'UnityEngine.Material' has been destroyed ...
  at UnityEngine.UIElements.UIR.MeshGenerator.DrawText ...
  at UnityEngine.UIElements.UITKTextJobSystem.AddDrawEntries ...
MeshGenerationContext is assigned to a VisualElement after ... Did you forget to call 'End'?
```

## 원인 — 우리 코드가 아니다

스택이 전부 UI Toolkit(에디터 UI) 내부다. `Editor.log`에서 **두 번 모두 직전이 같았다**.

```
Loaded scene 'Temp/__Backupscenes/0.backup'   ← 플레이 모드 종료(씬 복원)
Unloading 79 unused Assets                     ← 유니티가 자동으로 도는 언로드
MissingReferenceException ...
```

언로드가 에디터 UI의 **폰트 아틀라스 Material을 파괴**했는데, 이미 예약된
텍스트 그리기(`MeshGenerationDeferrer` = 지연 실행)가 그걸 뒤늦게 참조했다.

**간헐적임을 수치로 확인했다 — 플레이 종료 18회 중 2회.** 결함이면 매번 나야 한다.

### 두 번째 줄은 별개가 아니다

`Did you forget to call 'End'?`는 각 예외 **30줄 뒤에 1:1로** 붙었다(17744→17774, 19163→19193).
예외가 `MeshGenerationDeferrer.Invoke` 안에서 던져져 `End()`가 불리지 못한 **후유증**이다.
🔴 이 둘을 따로 쫓지 말 것.

## 한 것 — 확률만 낮춘다

`EditorMemoryToolbarButton.Tick()`이 **1초마다 무조건** `MainToolbar.Refresh()`를 불러
UITK 텍스트 메시를 다시 만들고 있었다. 우리 프로젝트에서 UITK 텍스트를 상시 재생성하는
유일한 곳이라, 언로드와 겹칠 창을 계속 열어 두고 있었다.

→ **라벨이 실제로 바뀐 경우에만** `Refresh`를 부른다.

⚠️ **이건 근본 해결이 아니다.** 유니티 내부 레이스라 우리가 없앨 수 없고, 확률을 줄일 뿐이다.
   여전히 뜰 수 있다 — 에디터 전용이고 빌드에는 영향이 없으니 그때는 무시한다.

⚠️ 트레이드오프 — 라벨(워킹셋)이 같은 동안 툴팁 세부 수치도 멈춘다. 툴팁은 네이티브가 그려
   이 문제와 무관하고, 라벨이 같으면 MB 단위로 그대로라는 뜻이라 감수했다.

## 진단 도구

| 무엇 | 어디 |
|---|---|
| 에디터 로그 | `%LOCALAPPDATA%\Unity\Editor\Editor.log` (직전 세션 `Editor-prev.log`) |
| 플레이 종료 표식 | `Loaded scene 'Temp/__Backupscenes/0.backup'` — 이 횟수와 예외 횟수를 견주면 상시/간헐이 갈린다 |

## 후속

`memory-meter`는 [Arca Unity Toolkit] 사본이다. **마스터 반영은 사용자가 `/unity-skill-sync`를
직접 돌려야 한다** — 이 스킬은 모델이 호출할 수 없다.
