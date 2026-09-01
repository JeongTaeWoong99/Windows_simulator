---
date: 2026-09-02
title: 창고 탭 전환 — 격자 하나 + 탭별 공급자로 전환
tags: [client, ui, storage]
---

# 창고 탭 전환 — 격자 하나 + 탭별 공급자로 전환

## 목적 / 배경

창고에 탭 버튼 4개(캐릭터·장비·자원·특성)가 있었지만 **눌러도 아무것도 바뀌지 않았다.**
`StorageTabPresenter`가 버튼을 잡아 두고 "화면이 없다"를 로그로 남기는 것까지였다.

착수 전 데이터 현황을 확인하는 과정에서 **일감([T-036](../../tasks/archive/T-036-창고탭전환.md))에 적힌 선행 하나가
오해였다는 것이 드러났다.**

| 탭 | 일감에 적혀 있던 것 | 실제 |
|---|---|---|
| 캐릭터 | ⏸ T-026(가챠) 대기 | **이미 와 있었다** — `S_CharacterListResponse`가 로그인 때 1회 |
| 장비 | ⏸ T-002 대기 | 맞다. **게다가 `ItemType`이 산업 축이라 담을 칸조차 없다** |
| 특성 | ⏸ 서버 미구현 | 맞다 |

증분 패킷이 없는데도 캐릭터 탭이 성립하는 이유는 **캐릭터가 늘어날 경로 자체가 없기** 때문이다
(가챠는 아이템 전용). "T-026이 와야 한다"는 **여러 종으로 목록·필터를 실검증할 수 없다**는
뜻이었지, 화면을 못 만든다는 뜻이 아니었다.

## 변경 내용

### 구조 — 탭이 달라도 격자는 하나

- `UI/Storage/InventoryPresenter/` → **`UI/Storage/StorageGridPresenter/`** (폴더째 개명)
  - `StorageGridPresenter.cs` — 격자 본체. 프레임 200칸 · 칸 풀 · `ShowTab` · `HasSource`
  - `StorageSlotSource.cs` — 공급자 추상 뼈대(목록 캐시 + `Changed` + 구독 관리)
  - `StorageSlotData.cs` — 칸에 그릴 **완성값**(`Key`·`Name`·`Sub`·`Rarity`)
  - `ResourceSlotSource.cs` / `CharacterSlotSource.cs`
  - `InventorySlotView.cs` — 이 폴더로 함께 이동
- `StorageTabPresenter.cs` — `StorageTab` enum + `TabEntry`(탭↔버튼) 배선.
  `UIManager.mainScreens`와 같은 패턴이고 `ValidateTabs`가 배선을 검증한다
- `PlayerDataModel.cs` — `FindSlotIndexOf(characterId)` 조회 헬퍼 추가
- `GachaResultPresenter.cs` — `SetCountVisible` → `SetSubVisible` 한 줄

### 씬 · 프리팹 (Unity MCP)

- 씬 오브젝트 `Inventory Presenter (↓ SUB VIEW)` → **`Grid Presenter (↓ SUB VIEW)`**
- 칸 프리팹의 `Count Text` → **`Sub Text`** (필드도 `countText` → `subText`)
- `StorageTabPresenter`의 `tabButtons`(Button[4]) → `tabs`(TabEntry[4]) + `grid` 참조

### 문서 · 일감

- `Storage 규칙.md` — 전면 개정. **"탭 하나를 채우는 절차"** 신설
- `UI 배치 현황.md` · `Managers 규칙.md` 갱신
- T-036 완료 → `archive/`. **[T-041](../../tasks/T-041-창고장비특성탭.md)**(장비·특성 탭) ·
  **[T-042](../../tasks/T-042-창고칸이동.md)**(칸 이동) 신설. T-015·T-035에 연결 메모

## 주요 결정 / 근거

**격자를 탭마다 두지 않았다.** 자원·캐릭터·장비의 격자·스크롤·레이아웃이 완전히 같다 —
세 벌로 두면 한쪽만 고쳐지고 씬 오브젝트도 600개가 된다. 반대로 Presenter 하나에
`if (tab == …)`를 쌓는 것도 아니다. **탭마다 다른 것은 데이터와 표시 문구뿐**이라 그쪽만
공급자로 갈랐다.

**잠금을 탭 줄에 적지 않고 `HasSource`에서 파생시켰다.** 두 곳에 적으면 나중에 공급자를
붙이고도 버튼이 잠긴 채 남는다. 지금은 **공급자 파일 하나 + 등록 한 줄이면 탭이 저절로 열린다** —
`StorageTabPresenter`는 열지 않아도 된다.

**칸의 보조 문구를 `Count Text` → `Sub Text`로 개명했다.** 자원은 수량, 캐릭터는 배치 상태가
들어와 더 이상 수량 칸이 아니다. `Count + State`처럼 **용도를 나열한 이름은 세 번째 용도에서
또 바뀌므로**, "이름 아래 한 줄"이라는 **자리**를 가리키는 이름으로 두었다.

**`InventorySlotView`(클래스·프리팹)는 개명하지 않았다.** GUID가 씬과 `GachaResultPresenter`
양쪽에 걸려 있어 위험이 이득을 넘는다. 대신 `Bind(in StorageSlotData)`를 본체로 두고
기존 `Bind(itemId, count, rarity)`를 **얇은 래퍼**로 남겨 가챠 호출부를 안 건드렸다.

**정렬을 막고 있던 구조를 함께 고쳤다.** 옛 코드는 `ItemId → 프레임`을 고정 매핑하고
빈 프레임을 앞에서부터 찾아, 아이템이 처음 들어온 순서로 칸이 **영구히 고정**됐다.
`i번째 항목 → i번째 프레임`으로 바꿔 **순서의 주인을 공급자로 옮겼다** — 정렬·필터(T-015)는
이제 `Fill` 안에서 끝나고 격자는 고치지 않는다.

## 작업 방식 — 다음에도 쓸 것

**GUID를 유지한 채 개명하면 씬 수술이 거의 사라진다.** `AssetDatabase.MoveAsset`으로
`InventoryPresenter.cs` → `StorageGridPresenter.cs`를 옮기니 **씬의 `m_Script` 참조도,
직렬화된 `slotPrefab`·`slotParent` 값도 그대로 살아남았다.** 컴포넌트를 떼었다 붙일 필요가 없었다.
(계획 단계에서는 컴포넌트 교체 + 재배선을 예상했는데, 실제로는 불필요했다)

**직렬화 필드 이름을 바꿀 때는 `[FormerlySerializedAs]`로 값을 넘긴 뒤 걷어낸다.**
`countText` → `subText`에서 썼다. 프리팹을 새 이름으로 저장한 것을 파일에서 확인하고
(`grep subText`) 어트리뷰트를 제거했다 — 마이그레이션용 일회성이라 남기면 노이즈가 된다.

## 후속 작업 / 주의사항

- ⚠️ **재생(플레이) 검증이 아직 안 끝났다.** 컴파일 에러 0 · 배선 누락 0 · 프리팹 참조 4/4까지
  확인했고, **로그인 → 창고 → 탭 전환은 사람이 눈으로 봐야 한다**
  (Screen Space 캔버스는 스크립트로 측정되지 않는다 → `2026-08-01-ui-canvas-skeleton.md`).
- ⚠️ **씬 diff에 내가 만지지 않은 변경이 16건 섞여 있다.** 작업 시작 시점에 **씬이 이미 더티**였고,
  저장하면서 함께 기록됐다. 전부 **레이아웃이 계산하는 드리븐 값**
  (ScrollRect의 `Viewport`·`Content`·`Handle`, `WorkStation Select Presenter` 아래 버튼들의
  앵커·`sizeDelta`)이라 런타임 동작에는 영향이 없다. 커밋할 때 이 줄들이 함께 들어간다.
- ⚠️ **`Start` 순서에 기대지 않는다.** 탭 줄이 격자보다 먼저 돌면 공급자가 비어 있어
  **모든 탭이 잠긴 채로 굳는다.** `StorageGridPresenter.EnsureInitialized()`가 양쪽에서 불릴 수
  있게 열려 있다 — **격자에 무언가를 묻는 public 메서드를 추가하면 그 앞에도 같은 호출을 둔다.**
- `StorageInformationPresenter`(칸 클릭 → 상세)를 이을 때, **칸의 `Key`가 자원은 `ItemId`,
  캐릭터는 개체 번호다.** 어느 탭인지까지 함께 넘겨야 한다.
- **탭 버튼 순서는 사용자가 씬에서 바꿨다** — 화면 왼쪽부터 **`자원 · 캐릭터 · 장비 · 특성`**
  (기본 탭인 자원이 맨 앞). **`StorageTab` enum 순서(`Character` 먼저)와 다르지만 정상이다** —
  `TabEntry`가 탭↔버튼을 짝지으므로 배열 순서는 화면과 달라도 된다.
  ⚠️ **화면 순서에 맞추려고 enum 값을 재정렬하지 않는다** — 씬에 int로 저장돼 배선이 조용히 어긋난다.
