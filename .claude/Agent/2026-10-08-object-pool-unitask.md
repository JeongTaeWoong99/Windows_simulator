---
date: 2026-10-08
title: 오브젝트 풀 · 코루틴 → UniTask (T-132)
tags: [client, infra, docs]
---

# 오브젝트 풀 · 코루틴 → UniTask (T-132)

## 목적 / 배경
- UniTask·DOTween 도입에 맞춰 클라 비동기 규칙을 정하고, 풀링을 들인다 → `tasks/T-132`
- 규칙은 스킬 `unitask-dotween` · `object-pool`, 코드는 `Common/object-pool/`

## 주요 결정 / 근거
- **풀이 둘이다.** 짧게 사는 연출은 `PrefabPool<T>`(부모를 옮김), 목록 줄은 `UIRowList<T>`(제자리에서 끄기).
  `ObjectPool`을 목록에 쓰면 레이아웃 그룹 양쪽이 매번 다시 배치되고, 스택이라 줄 순서·툴팁 연결이 섞인다.
- **대여 토큰(`PooledObject.RentToken`)** — 풀링된 오브젝트는 파괴되지 않아 destroy 토큰이 발동하지 않는다.
  반납·파괴 중 먼저 오는 쪽에서 취소되게 `destroyCancellationToken`에 링크했다.
- **이중 반납에 경고를 찍지 않는다** — "밖에서 반납 → 토큰 취소 → 연출이 깨어나 finally에서 또 반납"이 정상 경로라 경고가 소음이 된다.
- **Common 풀은 UniTask·DOTween을 참조하지 않는다** — 툴킷이 그 패키지 없는 프로젝트에도 들어간다. 트윈 Kill은 `IPoolable` 구현 몫.
- **슬롯 뷰는 ObjectPool이 아니라 칸별 재사용** — 뷰가 칸 프레임 하나에 묶여 있어 그 자리에서 끄고 켜는 것이 가장 단순하다.
- **목록 줄 8곳은 아직 안 바꿨다** — T-103이 같은 RowView를 만지고 있어 실측 뒤에.

## 후속 작업 / 주의사항
- ⚠️ **재사용되는 뷰의 `Bind`는 모든 표시값을 덮어야 한다.** `WidgetMiniSlotView`는 머리 그림이 없을 때 이전 캐릭터 그림이 남던 것을
  자리 표시로 되돌리게 고쳤다. 새 조건 분기(`if (x != null) 그리기`)를 넣을 때 else에서 되돌리는 것을 잊지 않는다.
- ⚠️ **코루틴은 오브젝트가 꺼지면 멈췄지만 UniTask는 계속 돈다.** `LoadingPresenter`는 `OnDisable`에서 대기를 끊게 옮겼다.
- `WindowManager`의 대기는 `#if !UNITY_EDITOR` 안이라 **빌드에서만** 확인된다.
- 검증은 외부 빌드(생성된 `Assembly-CSharp.csproj` 사본 + 고친 `Services.cs` + 새 파일 추가)로 오류 0을 확인했다 —
  작업 당시 작업 트리의 `Services.cs` 오타(`/Services`)와 DOTween EPO 모듈 켜짐 때문에 에디터 컴파일이 깨져 있었다(이 작업과 무관, 손대지 않음).
