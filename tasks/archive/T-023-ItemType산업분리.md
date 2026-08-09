---
id: T-023
제목: ItemType에서 산업(IndustryType)을 분리
담당: 공용
상태: 완료
우선순위: 보통
마감: 미정
---

# T-023 `ItemType`에서 산업(`IndustryType`)을 분리

## 배경

`Enum.xlsx`의 **`ItemType` 하나가 두 가지 일을 하고 있다.**

| 쓰임 | 값 | 어디 |
| --- | --- | --- |
| **아이템 분류** | `Misc` · `Special` (+산업 5종) | `ItemTable.ItemType` — 이 아이템이 무엇인가 |
| **산업** | `Farming`~`Hunting` | 배치·적성·드롭·산업 레벨 — 어떤 일을 하는가 |

증상은 **코드가 타입 이름을 매개변수 이름으로 보충하고 있다는 것**이다.

```csharp
public DropTable Get(ItemType industry, int level)      // DropTableCatalog
public int GetAptitude(ItemType industry)               // Character
public void AssignWorkStation(int slotIndex, ItemType industry, ...)
private readonly Dictionary<ItemType, int> _industryUnlocks;
```

전부 `industry`라고 적어 놓았다. **타입이 스스로 말하지 못하니 이름이 대신 말하는 중이다.**
그래서 `GetAptitude(ItemType.Misc)`처럼 의미 없는 호출이 **컴파일로 막히지 않는다**
(지금은 `switch`의 `_ => 0`이 받아 넘긴다).

> 프로토콜은 **먼저 갈라 두었다** — `EIndustryType`은 1차 산업만 담는다(2026-08-10).
> 서버 안쪽이 남았다.

## 왜 바로 못 고치는가 — 데이터가 숫자에 묶여 있다

`ItemType`의 **정수값이 이미 여러 곳에 박제돼 있다.**

| 대상 | 어떻게 묶였나 |
| --- | --- |
| `DropTID` | `산업 × 100000 + 레벨 × 100 + 순번` — 산업 자리가 `ItemType` 값이다 |
| `t_workstation_slot.industry` | 배치된 산업을 정수로 저장 |
| `t_user_industry_level.industry` | 해금 기록의 키 |
| `IndustryLevelTable.IndustryType` | 엑셀 컬럼(`eItemType`) |

**새 enum의 값을 `ItemType`과 다르게 잡으면 저장된 데이터가 전부 어긋난다.**
값을 1:1로 유지하면 이관 없이 갈 수 있는지부터 확인한다.

## 원본은 반드시 `Enum.xlsx`다 — 손으로 쓴 enum으로는 안 된다

산업 값은 **엑셀 컬럼 타입이자 DB 저장값**이라, 서버 코드에 enum을 손으로 선언하는 것으로는 못 푼다.

| 이유 | 내용 |
| --- | --- |
| **엑셀 마커가 못 찾는다** | `IndustryLevelTable.IndustryType` 컬럼은 지금 `eItemType`이다. `eIndustryType`으로 바꾸려면 그 enum이 **`Enum.xlsx`에 실재해야** 생성기가 타입을 붙인다 |
| **DB에 숫자가 남는다** | `t_workstation_slot.industry` · `t_user_industry_level.industry`가 정수로 저장한다. 값이 영구 계약이라 **기획자가 편집하는 자리와 같은 원본**이어야 한다 |
| **생성물이 따라온다** | `.bytes` · Unity 미러 · `Ref` 검사가 전부 이 원본에서 나온다 |

> 손으로 쓰는 예외는 **프로토콜 미러(`MikaProtocol.EIndustryType`) 하나뿐**이다.
> `MikaProtocol`이 `GameData`를 참조하지 않아서 그렇고, `GlobalRarity`/`EGlobalRarity`와 같은 구조다.
> 대신 `PacketEnumTest`가 1:1을 강제한다.

## 할 일

- [x] **`Enum.xlsx`에 `IndustryType` 신설** (2026-08-10) — `Farming=1`~`Hunting=5`.
      값은 `ItemType` 시트에서 그대로 읽어 넣었고, `None`·`Max`는 생성기가 붙인다.
      **데이터(.bytes·DataLog)는 무변동** — `Enum.cs`만 늘었다
- [x] 가드 테스트 3종(`PacketEnumTest`) — 프로토콜 미러 1:1 ·
      **`IndustryType` 값 == `ItemType` 산업 구간**(DropTID·DB 제약) · 아이템 분류 불포함
- [x] `ItemTable.ItemType`은 그대로 뒀다 — 아이템 분류라는 원래 뜻이다
- [x] 산업을 뜻하는 자리를 전부 `IndustryType`으로 교체 — `Character.GetAptitude`·`Industries` ·
      `DropTableCatalog` · `IndustryLevelCatalog` · `WorkStationSlot.Industry` · `_industryUnlocks` ·
      `User.CanAssignCharacter` · `ClientPacketHandler` · 테스트 8개 파일
- [x] `IndustryLevelTable.IndustryType` 컬럼 타입을 `eIndustryType`으로 (산업별 시트 5개 전부)
- [x] 드롭 시트에는 산업 열이 없다 — `DropTID`에 인코딩돼 있어 손댈 것이 없었다
- [x] `EIndustryType` ↔ `IndustryType` 가드로 갱신. `IndustryType` == `ItemType` 산업 구간 가드는 유지

## 결과 (2026-08-10 완료)

**`GetAptitude(ItemType.Misc)`가 이제 컴파일되지 않는다.** 마이그레이션 중 뜬 컴파일 에러 2건이
정확히 그 자리였다(`CharacterTest`의 `[InlineData(ItemType.Misc/Special)]`) — 예전엔 `_ => 0`이
런타임에 조용히 삼키던 호출이다.

- **데이터 이관 없음.** `.bytes`·`DataLog` 무변동, DB 스키마·저장값 그대로.
- 서버 테스트 194건 통과 · 빌드 경고 0.

### ⚠️ Unity 클라이언트는 컴파일이 깨진다 (담당 분리라 손대지 않았다)

프로토콜의 `Industry` 필드가 `byte` → `EIndustryType`으로 바뀌었다. **와이어는 1바이트 그대로**라
통신은 안 깨지지만, 클라 코드가 `byte`로 다루던 자리는 고쳐야 한다.

| 파일 | 증상 |
| --- | --- |
| `UI/WorkStation/SelectPanel/WorkStationSelectPanelUI.cs` | `Send(byte industry, …)` → `Industry = industry` **컴파일 에러**. `Send(0, 0)`(해제)도 같다 |
| `Log/PlayerDataLogger.cs` · `UI/.../WorkStationSlotView.cs` | `(GameData.ItemType)slot.Industry` — **컴파일은 되지만** 이제 `slot.Industry`가 곧 enum이라 캐스팅이 불필요하다 |

## 완료 조건

`GetAptitude(ItemType.Misc)` 같은 호출이 **컴파일 단계에서 막히고**,
기존 DB의 배치·해금 기록과 `DropTID`가 이관 없이 그대로 읽힌다.

## 막고 있는 것 / 선행 일감

- 없음. 다만 **`ItemType`을 건드리는 다른 작업과 겹치면 충돌이 크다** — 단독으로 돌린다
- ⚠️ `Enum.xlsx`는 공용이다. 값 배치는 **뒤에만 추가** 규칙을 지킨다
  (게임기획코어 4장 식별자 규칙)

## 관련 커밋

- 없음

## 참고

- `GameDesign/Excel/Enum.xlsx` — `ItemType` 시트
- `Server/MikaProtocol/PacketEnum.cs` — `EIndustryType`(이미 분리됨)
- `Server/WSGameServer/User/Character/Character.cs` — `Industries`
- [캐릭터](../GameDesign/design/character/README.md) 7.1
