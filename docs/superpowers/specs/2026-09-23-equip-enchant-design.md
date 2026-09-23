# 장비 인챈트 — 설계

> 작성: 2026-09-23 · 선행: [`2026-09-17-equipment-design.md`](2026-09-17-equipment-design.md) (T-002 장비 시스템)

## 1. 범위

**포함** — 인챈트 등급·옵션 줄 정의 · 인챈트 아이템 3종 동작(부여·큐브·줄 확장) · 옵션 풀 테이블 ·
장비 개체에 인챈트 저장 · 인챈트 패킷 · 속도/경험치 반영 · 상자 드롭.

**제외** — 클라 UI · 줄 고정(잠금) · 인챈트 이전/추출 · 거래소에서의 인챈트 장비 취급.

## 2. 결정 사항

| # | 항목 | 결정 |
| --- | --- | --- |
| 1 | 이름 | **인챈트**(Enchant). 장비 개체에 붙는 추가옵션 |
| 2 | 효과 축 | **속도 가산 · 캐릭터 경험치** 둘. `(대상 산업, 천분율)` 쌍이며 산업은 `None`(전 산업) 가능 |
| 3 | 희귀도 | **옵션으로 두지 않는다.** 획득 아이템 희귀도에 손대지 않으므로 [자원채취 2.1](../../../GameDesign/design/gathering/README.md)의 2026-09-20 확정("판정 1회의 양과 질에 보정을 두지 않는다")을 건드리지 않는다 |
| 4 | 등급 | **Rare · Epic · Legendary 3단계** + 인챈트없음. `GlobalRarity` 재사용 — 색상이 `Enum.xlsx`에 이미 있다 |
| 5 | 줄 수 | **기본 2줄 · 상한 3줄.** **등급과 독립된 축이다** — 등급은 옵션 풀의 질만 정한다 |
| 6 | 아이템 | **동작 3종.** 부여(`Grant`) · 큐브(`GradeUp`) · 줄 확장(`ExpandLine`) |
| 7 | 아이템 종수 | 큐브는 **1종**. 부여·확장은 **여러 종**이며 **차이는 성공 확률 하나**뿐 |
| 8 | 실패 | **실패해도 아이템은 소모된다.** 단 큐브는 등급 상승에 실패해도 줄을 재롤하므로 빈손이 없다 |
| 9 | 중복 줄 | **허용.** "2줄 전부 낚시 속도"가 잭팟으로 성립한다 |
| 10 | 저장 | **`t_user_equip`에 컬럼 추가.** 별도 테이블로 쪼개지 않는다 → 4장 |
| 11 | 획득 | **상자 3종을 열면** 나온다 — `GachaItemTable`의 풀 `GachaId` 6·7·8. `CommonRewardTable`은 *상자 자체*의 드롭이라 건드리지 않는다 |
| 12 | **착용 중 인챈트** | **거부한다.** 캐릭터에서 벗겨야 인챈트할 수 있다 → 3.3 |
| 13 | 수치 | 등급 상승 확률만 확정(5% · 0.5%). 옵션 값·`Weight`·아이템 확률은 **테스트값** |

## 3. 동작

### 3.1 상태

장비 개체 하나가 **`(인챈트 등급, 옵션 줄 0~3개)`** 를 갖는다. 인챈트없음이면 줄은 0개다.

### 3.2 아이템 3종

| `Action` | 대상 | 성공하면 | 실패하면 |
| --- | --- | --- | --- |
| `Grant` | 인챈트없음 | **Rare 부여 + 2줄 롤** | 아무 변화 없음 |
| `GradeUp` | 인챈트 있음 | **등급 1단계 ↑** 후 그 등급 풀에서 **줄 전부 재롤** | **등급 유지 + 줄 전부 재롤** |
| `ExpandLine` | 인챈트 있음 · 2줄 | **3줄로 확장**, 새 줄만 롤 | 아무 변화 없음 |

- **부여는 항상 Rare에서 시작한다.** 등급은 `GradeUp`으로만 오른다.
- **Legendary 장비에 `GradeUp`을 쓰면 상승 판정 없이 재롤만 한다.** 응답의 `Success`는 `false`다
  — 올라간 등급이 없기 때문이다. 줄은 갱신된다.
- **재롤은 줄 수를 바꾸지 않는다.** 3줄짜리를 재롤하면 3줄이 그대로 다시 뽑힌다.
- **성공 확률의 출처가 동작마다 다르다.**

| `Action` | 확률 출처 | 이유 |
| --- | --- | --- |
| `Grant`·`ExpandLine` | `EnchantItemTable.SuccessPermille` | **아이템마다 다르다**(확률형·확정형) |
| `GradeUp` | `EnchantGradeTable.UpPermille` | **현재 등급마다 다르다.** 큐브는 1종이라 아이템에 실을 수 없다 |

> `GradeUp` 아이템의 `SuccessPermille`은 읽지 않는다. 시트에는 `1000`을 적어 둔다.

### 3.3 거부

| 상황 | `EResultCode` |
| --- | --- |
| **캐릭터가 착용 중인 장비** | **`EnchantEquipped`** — 동작 3종 전부. **벗기는 것이 선행 조건이다** |
| 인챈트 있는 장비에 `Grant` | `EnchantAlreadyRolled` |
| 인챈트 없는 장비에 `GradeUp`·`ExpandLine` | `EnchantNotRolled` |
| 3줄 장비에 `ExpandLine` | `EnchantLineMax` |
| 인챈트 아이템 미보유 | `EnchantItemNotOwned` |
| 장비 미보유 | 기존 `EquipNotOwned` |

> **착용 중 거부가 서버를 크게 줄인다.** 창고에 있는 장비만 바뀌므로 **인챈트는 가동 중인 슬롯의
> 속도·경험치에 영향을 줄 수 없다.** 정산(`SettleWorkStation`)도, 속도 재확정(`RefreshWorkStationSpeed`)도
> 부를 필요가 없다 — 소급 버그의 여지 자체가 사라진다. 바뀐 값은 **다음에 장착할 때** 기존 `TryEquip` 경로가 반영한다.

### 3.4 줄 뽑기

해당 등급의 `EnchantOptionTable` 행들을 `Weight` 비례로 뽑는다. 기존 `WeightedPicker<T>`를 그대로 쓴다.
`GradeUp`은 **확정된 등급의 풀에서 줄 수만큼** 새로 뽑아 기존 줄을 전부 버린다.

## 4. DB

```sql
ALTER TABLE t_user_equip ADD COLUMN enchant_grade INTEGER NOT NULL DEFAULT 0;  -- 0=인챈트없음, 그 외 GlobalRarity
ALTER TABLE t_user_equip ADD COLUMN enchant_1     INTEGER NOT NULL DEFAULT 0;  -- EnchantOptionTable.EnchantOptionTID
ALTER TABLE t_user_equip ADD COLUMN enchant_2     INTEGER NOT NULL DEFAULT 0;
ALTER TABLE t_user_equip ADD COLUMN enchant_3     INTEGER NOT NULL DEFAULT 0;  -- 0 = 2줄짜리
```

T-002가 *"강화 기획이 생기면 `ALTER TABLE ADD COLUMN`"* 으로 비워 둔 자리를 그대로 쓴다.

**별도 테이블로 쪼개지 않는 이유** — ① 줄 수 상한 3이 기획 확정이라 가변이 아니고,
② 줄은 `EnchantOptionTID` 정수 하나뿐이며 **항상 통째로 읽고 통째로 쓴다**(재롤은 전 줄 교체 = `UPDATE` 1행),
③ 인챈트는 장비 개체와 수명이 같은 1:0..1 관계다. 분리하면 로그인 조회가 하나 늘고
한 개체의 상태가 두 테이블로 갈라진다.

> **분리로 갈아탈 신호** — **줄마다 속성이 붙는 순간.** 줄 고정(잠금) 같은 기획이 들어와
> `enchant_lock_1~3`이 또 늘어나면 그때 `t_user_equip_enchant(equip_id, line_no, …)`로 옮긴다.
> 지금 미리 하지 않는다.

**줄 수는 컬럼으로 두지 않는다** — `enchant_3 != 0`으로 유도한다. 확장 성공 시 새 줄이 즉시 롤되므로 0일 수 없다.

## 5. 데이터 (엑셀)

`Enum.xlsx` — **뒤에만 추가.** DB에 정수로 저장된다.

| 시트 | 값 |
| --- | --- |
| `EnchantOptionType` | Speed=1 · CharacterExp=2 |
| `EnchantAction` | Grant=1 · GradeUp=2 · ExpandLine=3 |

`EquipEnchant.xlsx` / 시트 `EnchantOptionTable` — **등급별 옵션 풀과 확률**:

| 컬럼 | Type | 비고 |
| --- | --- | --- |
| `EnchantOptionTID` | int | 유일. **DB에 저장된다 → 번호 재사용 금지** |
| `Grade` | eGlobalRarity | Rare·Epic·Legendary만 |
| `OptionType` | eEnchantOptionType | |
| `Industry` | eIndustryType | None(0) = 전 산업. `Speed`에만 의미 |
| `Value` | int [1..] | 천분율 |
| `Weight` | int [1..] | **같은 `Grade` 안에서의 가중치** |
| `Description` | string `""` | 메모 |

같은 `EquipEnchant.xlsx` / 시트 `EnchantGradeTable` — **등급이 갖는 값**:

| 컬럼 | Type | 비고 |
| --- | --- | --- |
| `Grade` | eGlobalRarity | Rare·Epic·Legendary |
| `UpPermille` | int [0..1000] | **다음 등급으로 오를 확률.** Legendary는 0 |
| `Description` | string `""` | 메모 |

| `Grade` | `UpPermille` | 뜻 |
| --- | --- | --- |
| Rare | **50** | → Epic **5%** |
| Epic | **5** | → Legendary **0.5%** |
| Legendary | 0 | 최고 등급 — 재롤만 |

같은 `EquipEnchant.xlsx` / 시트 `EnchantItemTable` — **아이템이 무엇을 하는가**:

| 컬럼 | Type | 비고 |
| --- | --- | --- |
| `ItemTID` | int | `Ref: ItemTable.ItemTID` |
| `Action` | eEnchantAction | |
| `SuccessPermille` | int [1..1000] | 1000 = 확정 |
| `Description` | string `""` | 메모 |

`Item.xlsx` — 인챈트 아이템을 `ItemType = Special`로 추가한다. TID 대역은 기존 특수 아이템 규칙을 따른다.
`Gacha.xlsx` / `GachaItemTable` — 상자 3종의 **개봉 풀**(`GachaId` 6 나무 · 7 은 · 8 황금)에 인챈트 아이템을 넣는다.
**`CommonRewardTable`은 건드리지 않는다** — 그건 채취 판정마다 *상자가* 떨어질 확률이다.

> **새 확장·부여 아이템을 추가하는 일은 엑셀 두 줄**(`ItemTable` + `EnchantItemTable`)**로 끝난다.**
> 확률이 코드가 아니라 데이터에 있기 때문이다.

## 6. 패킷 (`MikaProtocol`)

```
EquipInfo             + int EnchantGrade; int[] EnchantOptions      (줄 수만큼 0~3개)

C_EquipEnchantRequest   { long EquipId; int ItemTid }
S_EquipEnchantResponse  { EResultCode Result; long EquipId; bool Success;
                          int BeforeGrade; int AfterGrade; int[] Options }
```

- **요청 패킷은 하나다** — 무엇을 하는 아이템인지는 `EnchantItemTable`이 정하므로 클라가 동작을 고르지 않는다.
- 개체 갱신은 기존 `S_EquipSyncResponse`, 아이템 차감은 기존 인벤토리 싱크를 그대로 탄다.
- `Success`는 **아이템의 성공 판정 결과**다. `GradeUp` 실패도 줄은 재롤되므로 `Options`는 항상 갱신된 값이다.
- `EResultCode` 610번대 신설: `EnchantItemNotOwned=610` · `EnchantAlreadyRolled=611` · `EnchantNotRolled=612` · `EnchantLineMax=613` · `EnchantEquipped=614`.
- **치트를 추가하지 않는다** — 인챈트 아이템은 평범한 아이템이라 기존 `ECheatCommand.GiveItem`(Arg1 = ItemTID · Arg2 = 개수)이 이미 지급한다.

## 7. 서버 흐름

**적재** — `LoginRepository`의 `t_user_equip` SELECT에 컬럼 4개가 붙는다. 쿼리 수는 그대로다.
`UserEquipRow`에 필드 추가. 테이블에 없는 `EnchantOptionTID`는 경고만 남기고 그 줄을 버린다(기존 미보유 TID 처리와 같은 규약).

**카탈로그** — `Common/EnchantCatalog.cs`: `Grade → WeightedPicker<EnchantOptionTableRow>` · `ItemTID → (Action, SuccessPermille)`.
기동 시 `GameTable.LoadAll` 뒤 `LoadAll`. `EquipCatalog`과 같은 자리.

**메모리** — `Equip` 개체에 `EnchantGrade` · `IReadOnlyList<int> EnchantOptions` 추가.
착용 효과 합산을 `Equip`이 직접 제공한다: `SpeedAddPermilleFor(IndustryType)` · `ExpAddPermille`.

**`TryEnchant(equipId, itemTid)`**
1. 장비·아이템 보유와 3.3의 거부 조건 검사. **착용 중이면 여기서 끝난다**(`EnchantEquipped`).
2. 아이템 1개 차감.
3. 성공 판정 → 3.2대로 등급·줄 갱신.
4. `SaveEquipEnchantRepository`(`UPDATE t_user_equip` 1행) → `S_EquipEnchantResponse` + `S_EquipSyncResponse` + 인벤 싱크.

> **`now`를 받지 않는다.** 정산도 속도 재확정도 없기 때문이다 — 3.3의 착용 중 거부가 만든 결과다.
> `TryEquip`이 6단계인 것과 대비된다.

**속도** — `ResolveSlotSpeed`의 장비 가산이 `기본 SpeedAddPermille`에서
`기본 + 인챈트 Speed 줄(Industry가 슬롯 산업과 같거나 None)`로 바뀐다. **기존 코드에 닿는 수정은 이것과 아래 경험치 둘뿐이다.**

**경험치** — `SettleWorkStation`의 캐릭터 경험치 지급에 **그 캐릭터가 착용한 장비의 `CharacterExp` 줄 합**을 가산한다.

**저장 실패** — 기존 규약대로 `OnDbFailed`가 세션을 끊는다. 되돌리는 코드를 두지 않는다.

## 8. 테스트

| 파일 | 내용 |
| --- | --- |
| `Common/EnchantCatalogTest` | 등급별 풀 구성 · 중복 `EnchantOptionTID` 예외 · `EnchantItemTable`의 `ItemTID`가 `ItemTable`에 있는지 |
| `User/UserEnchantTest` | 부여/큐브/확장 각 성공·실패 · 3.3의 거부 5종(**착용 중 포함**) · Legendary에서 재롤만 · 시드 고정 롤 결과 · **인챈트 후 장착했을 때** 속도·경험치 반영 · 산업 불일치 줄은 가산 0 |
| `Repository/EquipRepositoryTest` | `:memory:` SQLite로 `UPDATE` 왕복 · 마이그레이션 후 기존 행이 0으로 읽히는지 |
| `Protocol/PacketEnumTest` | `EEnchantOptionType`·`EEnchantAction` ↔ `GameData` 쪽 enum |

실서버 확인: Unity CLI eval로 치트 지급 → 인챈트 → `S_WorkStationSlotSyncResponse`의 `CurrentWorkSpeed` 변화.

## 9. 문서 반영

`character/README.md`(장비 절에 인챈트 추가) · `workslot/README.md` 3.4(가산 항에 인챈트) ·
`item/README.md`(특수 아이템) · `게임기획코어.md` 3장 시스템 지도 · 5장 확정 표 ·
`Server/docs/치트.md` · `Server/docs/채취-정산.md` · `tasks/`에 일감 등록 · 프로토콜 미러 커밋.

전파 후 `powershell -File GameDesign/check-doc-graph.ps1 -Changed`로 검사한다.

## 10. 미정 — 전부 테스트값으로 시작

| 항목 | 비고 |
| --- | --- |
| 옵션 풀의 `Value`·`Weight` | 등급 간 격차가 곧 큐브의 동기다 |
| 부여·확장 아이템의 종수와 확률 | 확률형/확정형 최소 2종씩 |
| 상자별 드롭 수량 | 상자 등급이 올라갈수록 좋은 아이템이 나오게 할지 포함 |
