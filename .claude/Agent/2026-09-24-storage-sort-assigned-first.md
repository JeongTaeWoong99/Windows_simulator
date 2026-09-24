---
date: 2026-09-24
title: 창고 [정렬]에서 배치·장착 중인 칸을 맨 앞으로 고정
tags: [client, ui, storage]
---

# 창고 [정렬]에서 배치·장착 중인 칸을 맨 앞으로 고정

## 목적 / 배경
- [정렬]을 눌러도 작업슬롯에 배치된 캐릭터·장착 중인 장비가 미배치 칸들 사이에 흩어져
  "지금 쓰고 있는 것"을 눈으로 훑어야 했다.

## 변경 내용
- `Assets/Scripts_Client/UI/Storage/StorageGridPresenter/StorageSlotSource.cs`
  — `CompareAssignedFirst` 훅(기본 0)을 추가하고 `CompareByRule`이 규칙보다 먼저 본다.
- `CharacterSlotSource`·`EquipSlotSource` — 각각 `FindSlotIndexOf`·`IsEquipped`로 override.

## 주요 결정 / 근거
- **배치 우선 판정을 `CompareForSort` 안에 넣지 않았다.** `CompareByRule`이 오름차순일 때
  `CompareForSort` 결과를 통째로 뒤집기 때문에, 안에 넣으면 오름차순에서 배치 칸이
  **맨 아래로 내려간다.** 방향 뒤집기 **밖**에 두어야 "정렬 상관없이 앞"이 성립한다.
  → 훅을 새로 판 이유가 이것 하나다. 나중에 `CompareForSort`로 합치면 조용히 깨진다.
- 판정은 격자의 '배' 마크(`StorageGridPresenter.Redraw`)와 같은 함수를 쓴다 —
  각자 훑으면 "마크는 붙었는데 위로 안 오는 칸"이 생긴다.
- 기반 클래스에 기본값 0인 virtual로 둬서 자원 탭은 손대지 않는다.

## 후속 작업 / 주의사항
- **배치를 바꿔도 순서는 즉시 따라오지 않는다.** `Sort`가 Key별 자리를 `_rank`에 기억하고
  이후 `Rebuild`는 그 기억을 따르기 때문이다(기반 클래스 "정렬 — 누른 순간의 순서를 기억한다").
  다시 [정렬]을 누르면 맞춰진다. 기존 자리 기억 설계와 일관되어 그대로 뒀다 — 바꾸려면
  `ApplyRememberedOrder`까지 함께 봐야 한다.

---

## 업데이트 (2026-09-24) — 비교자를 걷어내고 분할로 바꿨다 · 딤 · 줄 끊기

같은 날 이어진 논의에서 **"장착·배치된 것을 창고에서 아예 숨기자"** 는 안이 나왔고,
**숨기지 않기로 했다.** 근거 둘 — 되짚지 않도록 남긴다.

- **창고가 보유 전체의 유일한 진실이다.** 작업슬롯 배치 목록(T-046)과 장비 고르기
  (`RefreshEquipPicker`)가 **이미** 배치·장착된 것을 걸러 낸다. 창고에서까지 숨기면
  보유 전량을 볼 수 있는 화면이 **0개**가 된다 — T-046이 걸러 내기로 한 전제가 창고였다.
- **창고 한도가 개체를 센다.** 서버 `User.StorageCapacity = 200`은 장착·배치를 가리지 않고,
  장비는 장착해도 `SlotPosition`을 비우지 않는다(`Equip.Wear`). 숨기면
  "칸이 꽉 차 뽑기가 거절됐는데 화면엔 180개"가 되어 원인을 추적할 길이 없다.
- 오판매 방어는 숨기기가 아니라 [T-075](../../tasks/T-075-캐릭터장비판매.md)가 맡는다 —
  담기 단계 차단(클라) + 거절 코드(서버)가 이미 그 일감에 적혀 있다.

### 대신 바꾼 것

- **`CompareAssignedFirst`(비교자 훅)를 걷어내고 `IsAssigned` + `PartitionAssignedFirst`(안정 분할)로.**
  비교자로 두면 **[정렬]을 누른 순간에만** 모인다 — 배치·장착은 수시로 바뀌고 그때는 `Rebuild`만
  돌기 때문에, 방금 뺀 캐릭터가 계속 맨 위에 남았다. 분할을 `Rebuild`·`Sort` 양쪽 끝에 두어
  **누르든 안 누르든** 앞에 모인다. (첫 판에 적어 둔 "배치를 바꿔도 즉시 안 따라온다"는 한계가 닫혔다)
- **줄 끊기** — `StorageGridPresenter.ComputeRowGap`. 배치 블록이 줄을 다 채우고 끝나도록
  빈 프레임을 끼운다. ⚠️ **이때부터 프레임 번호 ≠ 목록 번호다**(`Redraw`가 gap만큼 민다).
  넘침 경고도 `count + gap` 기준으로 고쳤다 — 안 고치면 경고 없이 뒤가 잘리는 구간이 생긴다.
  열 수는 씬의 `FlexibleGridLayoutGroup`(= `GridLayoutGroup` 상속)에서 읽는다.
- **딤** — `SlotView`가 등급 배경의 **밝기만** 낮춘다(`AssignedDim = 0.45`).
  알파를 낮추면 뒤 배경이 비쳐 **등급색이 탭 배경에 따라 달라 보인다.** 글자는 안 건드린다 —
  배경이 어두워진 위에서 대비가 오히려 산다. 딤 전 원본 색을 `_rarityColor`에 들고 있는 이유는
  **어두워진 색에 또 곱하면 부를 때마다 검게 내려가기 때문**이다.

### 검증

Unity 리컴파일 통과 · 콘솔 에러·경고 0. **플레이 실측은 아직이다** — 확인할 것:
빈 칸이 줄을 정확히 끊는지 · 딤 세기가 적당한지 · 배치/해제 직후 자리가 즉시 옮겨지는지.

---

## 업데이트 2 — 줄 끊기를 철회하고 **스크롤 밖 고정 패널**로 바꿨다 (2026-09-24)

### 왜 철회했나 — 내가 만든 회귀

`ComputeRowGap`이 끼운 줄맞춤 빈 칸이 **표시 예산을 먹었다.** 프레임은 200개 고정인데
표시 대상이 `200 + gap 3 = 203`이 되어 **뒤쪽 3칸이 화면에서 사라졌다.**
`PlayerDataModel.OnEquipSynced` → `Rebuild` → `Redraw`에서 "뒤쪽이 잘렸다" 경고가 떴고,
사용자가 실행 중에 발견했다.

빈 칸으로 미는 방식은 **프레임 예산이 고정인 한 언제나 이 문제를 갖는다.**
프레임을 늘리는 건 답이 아니다 — 칸 수는 서버 상한(`User.Storage.StorageCapacity = 200`)과
같은 수여야 하고, 화면이 그보다 많은 칸을 약속하면 서버가 거절하는 자리를 보여 주게 된다.

### 대신 한 것

**배치·장착 칸을 스크롤 밖의 고정 패널로 올린다.** 프레임을 새로 깔지 않고 **옮긴다.**

- 씬 — `Assigned Panel`을 `#Storage Canvas` 직속, `Tool Presenter`와 `Grid Presenter` 사이에 뒀다.
  `FlexibleGridLayoutGroup`(FixedColumnCount 5 · 간격·패딩·비율 모두 `Content`와 동일) +
  `LayoutElement.flexibleHeight = 0`. 스크롤이 아니므로 내용만큼만 먹고, 남는 세로는 아래 격자가 갖는다.
  Unity MCP로 만들고 배선했다.
- 코드 — `StorageGridPresenter.PlaceFrames(assignedCount)`.
  앞쪽 `AssignedCount`개를 패널로, 나머지를 `Content`로 `SetParent` 한다.
  **위아래 합은 언제나 200이다** — 이게 이 방식을 고른 이유다.
- 부모·형제 순서가 다를 때만 옮긴다. 200개를 매번 다시 꽂으면 탭 전환마다 리빌드가 200번 돈다.
- 배치가 0이면 패널을 끈다. **끄기 전에 프레임을 모두 빼낸다** — 꺼진 패널 안에 칸이 갇히면
  그 칸은 어디에도 안 나타난다.
- `CacheFrames`가 패널에 남은 프레임을 **먼저 격자로 되돌린 뒤** 모은다(뒤에서부터 맨 앞으로 꽂아 순서 복원).
  모으는 순서가 곧 칸의 순서라, 두 부모에 나뉜 채 훑으면 순서를 잃는다.

`ComputeRowGap`·`ReadColumnCount`·`_grid` 필드는 삭제했다. 프레임 번호 ≠ 목록 번호였던 것도 함께 사라졌다.

### 서버 영향

없다. `HasStorageFor`·`ItemSlotsUsed` 계열은 **보유 개체를 센다** — 화면의 프레임이
어느 부모에 붙어 있는지와 무관하다. 총수를 바꾸지 않은 것이 이 무영향을 지켜 준다.

### 검증

Unity 리컴파일 통과 · 콘솔 에러·경고 0 · 씬 저장 완료
(`Assets/Scenes/Original/DesktopWindow_Control.unity`).
**플레이 실측은 아직이다** — 확인할 것: 배치가 있을 때 패널이 위에 뜨는지 ·
배치 0일 때 패널이 사라지는지 · 스크롤 쪽 칸 수가 `200 - 배치수`인지 ·
장비 탭처럼 배치가 많을 때 패널이 화면을 너무 먹지 않는지(먹으면 그때 높이 상한을 논의한다).

---

## 업데이트 3 — **전부 롤백**하고 팰월드 방식으로 갈아탔다 (2026-09-25)

### 무엇을 되돌렸나

이 문서가 적어 온 것이 **전부 원상 복구됐다.** 고정 패널까지 만들어 보인 뒤
사용자 판정은 "원하는 느낌이 아니다"였다.

| 되돌린 것 | 파일 |
|-----------|------|
| 배치 칸 앞으로 모으기 (`AssignedCount`·`IsAssigned`·`PartitionAssignedFirst`) | `StorageSlotSource` · `CharacterSlotSource` · `EquipSlotSource` |
| 딤 처리 (`AssignedDim`·`ApplyRarityColor`) | `SlotView` |
| 배치 패널 (`PlaceFrames`·배선 필드) | `StorageGridPresenter` |
| `Assigned Panel` 오브젝트 + 세션 중 쌓인 좌표 잡음 3,219줄 | 씬 (`git checkout` 후 Unity에서 `OpenScene`으로 재로드) |

### 왜 세 번이나 헛돌았나 — 남겨 둘 교훈

**전부 "섞여 보이는 것"을 표시로 풀려고 했다.** 앞으로 몰기 → 줄 끊기 → 패널 분리.
셋 다 배치·장착분이 **창고에 남아 있다는 전제** 위에 있었고, 그 전제가 문제의 원천이었다.

전제를 건드리자 **표시 문제가 통째로 사라졌다** — 목록에서 빼면 섞일 것이 없다.
표시를 세 번 고쳐도 안 풀리면 **판정을 의심할 차례**다.

### 대신 한 것 (T-086 클라 선행분)

배치·장착 개체를 **공급자의 `Fill`에서 뺀다.** 팰월드식으로 "슬롯·캐릭터에게 옮겨 간 것"으로 본다.

```csharp
// CharacterSlotSource.Fill
if (_data.FindSlotIndexOf(character.CharacterId) >= 0) { continue; }

// EquipSlotSource.Fill
if (_data.IsEquipped(equip.EquipId)) { continue; }
```

- **격자가 아니라 공급자에서 거른다.** 화면 쪽에서 거르면 그 자리에 빈 칸이 생겨
  "사라진 아이템"으로 읽힌다 — `Redraw`는 받은 목록을 앞 칸부터 채울 뿐이다.
- `Assign Mark` 판정은 **남겨 뒀다.** 이제 켜질 일이 없지만,
  걸러내기가 깨지면 **마크가 다시 뜨는 것이 화면에 드러나는 유일한 신호**다.
  마크를 실제로 걷어내는 건 서버 몫이 들어온 뒤다.

### ⚠️ 알고 남긴 틈

**화면만 먼저 바뀌었다.** 서버는 `Equip.Wear`에서 `SlotPosition`을 비우지 않고
`StorageCapacity` 검사도 장착 여부를 가리지 않으므로,
**"빈 칸이 보이는데 뽑기가 거절되는" 구간**이 열려 있다.
사용자 지시로 클라 선행분만 먼저 넣은 것이고, 서버·기획 몫은 이슈 #36으로 넘겼다.

### 산출물

- 이슈 [#36](https://github.com/JeongTaeWoong99/Windows_simulator/issues/36) — `[기획·서버 요청]` 규칙 6개 + 칸 풀 정책
- 일감 [T-086](../../tasks/T-086-배치개체인벤토리제외.md)(공용·진행중) · [T-087](../../tasks/T-087-창고칸풀동적관리.md)(클라·대기)
- 연계 갱신 — [T-064](../../tasks/T-064-클라창고가득참.md)(문구가 "상한 초과" 쪽으로 바뀐다) · [T-075](../../tasks/T-075-캐릭터장비판매.md)(배치분이 애초에 안 담긴다)
- [`Storage 규칙.md`](../../Assets/Scripts_Client/UI/Storage/Storage%20규칙.md) — "배치·장착 중인 개체는 창고에 없다" 절 신설

### 검증

Unity 리컴파일 통과 · 콘솔 에러·경고 0 · 씬은 HEAD와 동일(`Assigned Panel` 없음, dirty=False).
**플레이 실측은 아직이다** — 확인할 것: 배치하면 캐릭터 탭에서 사라지는지 ·
해제하면 돌아오는지 · 장비 장착·해제도 같은지 · `배` 마크가 어디에도 안 뜨는지.
