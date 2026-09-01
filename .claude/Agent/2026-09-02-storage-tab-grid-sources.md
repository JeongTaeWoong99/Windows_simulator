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
- T-036 완료 → `archive/`. **[T-043](../../tasks/T-043-창고장비특성탭.md)**(장비·특성 탭) ·
  **[T-044](../../tasks/T-044-창고칸이동.md)**(칸 이동) 신설. T-015·T-035에 연결 메모

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
- **커밋하지 않았다** (요청 없음). 작업 트리에 그대로 있다.
  → 이후 커밋함: `3412433`(구현) · `0c5ab97`(문서·일감).

## 업데이트 (2026-09-02) — 탭 순서를 씬·enum·배열 셋 다 맞췄다

사용자가 씬에서 탭 버튼을 **자원 · 캐릭터 · 장비 · 특성** 순으로 바꿨다(기본 탭인 자원이 맨 앞).
처음에는 **enum은 그대로 두고**(`Character` 먼저) 주석에 "배열 순서는 화면과 달라도 된다"고만
적었는데, **"씬과 논리적 서술이 일치했으면 좋겠다"는 요청**을 받아 enum도 재정렬했다.

- `StorageTab` → **`Resource, Character, Equipment, Trait`**
- 씬의 `tabs` 배열도 **버튼 순서대로 tab 값 0·1·2·3**이 되도록 다시 배선(MCP)

**enum 재정렬이 안전했던 것은 씬 배선을 함께 고쳤기 때문이다.** 위험한 것은 "코드만 바꾸는 것" —
씬에 int로 저장돼 있어 같은 숫자가 다른 탭으로 읽히고, **컴파일도 경고도 통과한다.**
주석·문서의 경고 문구도 "재정렬 금지"가 아니라 **"재정렬하려면 씬을 함께 고친다"** 로 고쳤다
(방금 실제로 재정렬했는데 금지라고 적혀 있으면 다음 사람이 모순을 만난다).

> 그냥 **늘리기만** 할 때는 여전히 **끝에 붙이면** 씬을 안 건드려도 된다.

### 곁가지 — 배선을 검증하다 초기화 결함이 드러났다

에디트 모드 검증 스크립트가 `HasSource`에서 `KeyNotFoundException`을 냈다.
`EnsureInitialized`가 **`_isReady = true`를 맨 앞에서 세우고** 있어서, 그 뒤의
`Services.Get<PlayerDataModel>()`이 예외로 끊겨도 **플래그만 켜진 채 격자가 영구히 빈다.**

- `_isReady = true`를 **메서드 끝으로** 옮겼다 — 중간에 끊기면 다음 호출이 다시 시도하고,
  원인도 매번 같은 예외로 드러난다.
- 다시 시도될 수 있으므로 `CacheFrames()`는 `_frames`·`_views`를 **비우고 시작한다.**

> 플래그를 세우는 자리는 "들어올 때"가 아니라 **"끝까지 갔을 때"** 다.

## 업데이트 (2026-09-02) — 일감 번호가 겹쳐 T-043·T-044로 내렸다

머지해 보니 **상대도 같은 날 `T-041`·`T-042`를 발급했다**(클라 핑 타이머 · 재화 패킷 대응).
파일 이름이 달라(`T-041-창고장비특성탭.md` vs `T-041-클라핑타이머.md`) **Git이 충돌로 잡지 않고
넷 다 그대로 넣었다.** INDEX 표에도 같은 번호가 두 줄씩 섰다.

- 창고 쪽을 **T-043 · T-044**로 내렸다 — 상대 것은 이미 이슈 #20·#21에 묶여 있어 되돌리기 비싸다.
- 참조 18곳(일감 표 · archive · 클라 문서 2건 · 코드 주석 4곳 · 이 로그)을 함께 고쳤다.

> **머지는 같은 줄만 충돌로 본다.** 번호·이름처럼 **저장소 전체에서 유일해야 하는 것**은
> Git이 지켜 주지 않는다. 새 번호는 INDEX의 마지막 번호 다음으로 따되,
> **떨어져 작업하는 동안에는 겹칠 수 있다고 보고 머지 직후 확인한다.**
