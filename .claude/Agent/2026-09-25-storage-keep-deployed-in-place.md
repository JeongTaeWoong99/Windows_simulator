---
date: 2026-09-25
title: 배치·장착 중인 개체도 창고 제자리에 남는다 (딤 + '배' 마크 + 정렬 맨 뒤)
tags: [client, ui, storage, docs]
---

# 배치·장착 중인 개체도 창고 제자리에 남는다

## 목적 / 배경

하루 전 채택한 **팰월드 방식**(배치·장착하면 창고 목록에서 뺀다 · 커밋 `b26777e` ·
[T-086](../../tasks/archive/T-086-배치개체인벤토리제외.md))을 **사용자 지시로 되돌렸다.**
같은 문제(배치·장착분이 목록에 섞여 보인다)에 대한 네 번째이자 최종 답이다.

새 사양은 셋이다.

| 항목 | 규칙 |
|------|------|
| 목록 | 배치·장착해도 **인벤토리에서 빼지 않는다.** 원래 칸에 그대로 남는다 |
| 구분 | 아이콘·등급색 **RGB × 0.55 딤** + 슬롯 위 **`배` 마크**. 둘은 항상 짝 |
| 정렬 | [정렬] 실행 시 **맨 뒤로** 밀린다 |
| 해제 | **원래 칸으로 복귀.** 위치 재할당 없음 (자리는 애초에 비워진 적이 없다) |

## 판단 — 왜 "빼기"가 무너졌나

목록에서 빼면 **해제로 인한 상한 초과가 정식 상태**가 된다. 해제는 언제나 성공해야 하니까
200칸을 넘는 순간이 생기고, 그러면 클라는 칸 UI를 **동적으로 더 만들어야** 하고
서버는 **확장 칸을 따로 관리**해야 한다([T-087](../../tasks/archive/T-087-창고칸풀동적관리.md)).
제자리에 남기면 **칸 수가 변하지 않으므로 이 두 가지가 통째로 사라진다** — 그게 이번 방향의 값이다.

## 구현

### `IsAway` 하나가 "지금 나가 있나"를 소유한다

딤·마크·정렬 세 군데가 각자 탭을 분기하면 같은 판정이 셋으로 갈라진다. 기반 클래스에 훅을 두고
탭별 공급자가 답한다.

```csharp
// StorageSlotSource
public virtual bool IsAway(long key) => false;

// CharacterSlotSource — 작업슬롯 화면과 같은 것을 읽는다
public override bool IsAway(long key) => _data.FindSlotIndexOf(key) >= 0;

// EquipSlotSource
public override bool IsAway(long key) => _data.IsEquipped(key);
```

`Fill`에서 걸러 내던 필터(어제 넣은 것)는 양쪽 모두 제거했다.

### 정렬 — **방향(▲▼)을 타지 않는 유일한 규칙**

```csharp
private int CompareByRule(SlotData a, SlotData b)
{
    bool awayA = IsAway(a.Key);
    if (awayA != IsAway(b.Key)) { return awayA ? 1 : -1; }   // 나가 있는 것은 항상 맨 뒤
    int compared = CompareForSort(a, b);
    return _order == StorageSortOrder.Ascending ? -compared : compared;
}
```

`Storage 규칙.md`의 "오름차순은 규칙 전체를 뒤집는다"에 대한 **의도된 예외**다.
이건 값의 순위가 아니라 **덩어리 가르기**라서, 뒤집으면 ▲를 누를 때마다 손댈 수 없는 것들이
맨 위를 차지한다 — [정렬]의 목적("지금 쓸 수 있는 것을 위로")과 정반대다. 코드·문서 양쪽에 근거를 남겼다.

### 딤은 `SlotView`가 상태로 갖는다

`Bind`가 등급색을 직접 쓰면 딤이 다음 `Bind`에 덮인다. `_rarityColor`·`_isDimmed`를 보관하고
`ApplyTint()` 한 곳에서만 색을 쓴다. 알파는 건드리지 않는다 (RGB만 × 0.55).

## 정리한 문서·일감

- [T-086](../../tasks/archive/T-086-배치개체인벤토리제외.md) → `tasks/archive/` · **취소**(기획 방향 변경)
- [T-087](../../tasks/archive/T-087-창고칸풀동적관리.md) → `tasks/archive/` · **불필요**(전제 소멸)
- [T-064](../../tasks/T-064-클라창고가득참.md) — "상한 규칙이 바뀐다"가 무효. 원래 계획대로 간다
- [T-075](../../tasks/T-075-캐릭터장비판매.md) — 배치분이 화면에 보이므로 **담기 단계 차단이 다시 필요**하다
- [T-058](../../tasks/T-058-창고칸위치서버.md) — 장착해도 `SlotPosition`을 비우지 않는 **지금 서버 동작이 맞다**
- [`Storage 규칙.md`](../../Assets/Scripts_Client/UI/Storage/Storage%20규칙.md) — 해당 절 교체
- 이슈 [#36](https://github.com/JeongTaeWoong99/Windows_simulator/issues/36) — 철회 사유를 댓글로 남기고 닫았다

## ⚠️ 남은 것

- **플레이 실측 미완.** 확인할 것: 배치·장착 시 그 칸이 어두워지고 `배`가 뜨는지 ·
  해제하면 같은 칸에서 원래 색으로 돌아오는지 · [정렬] 후 배치분이 맨 아래로 가는지 ·
  ▲로 뒤집어도 여전히 아래인지.
- 칸 번호는 **세션 한정**이다 (재접속하면 도착 순서). 영속화는 [T-058](../../tasks/T-058-창고칸위치서버.md) → [T-044](../../tasks/T-044-창고칸이동.md).
- 검증은 `dotnet build Assembly-CSharp.csproj` 오류 0개까지다.
