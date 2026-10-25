---
date: 2026-10-25
title: 안 만진 씬에 저장 표시 — 테마 태그 역할과 색 어긋남
tags: [client, ui, editor]
---

# 안 만진 씬에 저장 표시 — 테마 태그 역할과 색 어긋남

## 목적 / 배경
- 씬을 만지지 않았는데 `*`(저장 필요)가 다시 떴다. 2026-08-28(`2026-08-28-scene-dirty-flag.md`)과 증상은 같지만 원인이 다르다.

## 변경 내용
- 씬 `Sell Cart Panel` — `Disassemble Button` 태그 역할 `Button` → `ButtonPrimary`, `Head Panel/Kind Text (TMP)` 역할 `TextMain` → `Highlight`. 화면 색은 그대로(디스크에 저장된 금색·노랑 = 두 역할의 팔레트 값과 정확히 같다)
- `UI/Shared/theme/UIThemeColor.cs` — `OnValidate`에서 고르지 않은 칸이 다시 칠해지면 경고 `[UI 테마] 태그 역할과 색이 달라 다시 칠했다`
- `UI/Shared/Shared 규칙.md` 임시 테마 절 — "태그가 붙은 칸은 역할로 색을 바꾼다"

## 주요 결정 / 근거
- **원인**: `9f232c1c`에서 버튼·글씨를 복제해 색만 손으로 바꿨고, 태그는 원본 역할을 그대로 들고 있었다. 씬을 열거나 컴파일할 때 `UIThemeColor.OnValidate`가 역할대로 다시 칠하는데 색이 다르니 `SetDirty` → `*`. 저장하지 않으면 매번 반복되고, 저장하면 의도한 색이 사라진다. 8월의 가드("같은 값이면 안 건드린다")로는 못 막는 종류다
- **찾은 방법**(다음에도 쓸 것): `EditorSceneManager.SaveScene(scene, "Temp/x.unity", saveAsCopy: true)`로 메모리의 씬을 사본으로 저장하고 디스크 씬과 `git diff --no-index`를 하면 무엇이 바뀌었는지 바로 보인다. `EditorUtility.IsDirty` 목록은 레이아웃 RectTransform·TMP까지 수백 개가 섞여 쓸모가 없었다
- 경고는 `Selection.Contains`로 사람이 인스펙터에서 역할을 바꾼 경우를 뺐다. 자동 재칠 자체는 그대로 둔다(팔레트 값을 고치면 씬이 따라오는 기능이다)

## 후속 작업 / 주의사항
- ⚠️ 편집 모드에서 `TMP.color`를 스크립트로 바꾸면 `m_fontColor32`가 옛 값으로 남아 저장될 수 있다(이번에 흰색이 남았다). `SerializedObject`로 `m_fontColor32`를 맞춰 저장했다
- 경고가 실제로 뜨는 것은 재현하지 않았다(지금 씬은 어긋남 0이라 컴파일 뒤 경고 0 · 씬 clean만 확인)
