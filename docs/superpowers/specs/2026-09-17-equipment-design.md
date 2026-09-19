# 캐릭터 장비 시스템 — 설계 (T-002)

> 작성: 2026-09-17 · 일감: [`tasks/T-002-장비슬롯.md`](../../../tasks/T-002-장비슬롯.md)

## 1. 범위

**포함** — 장비 정의 테이블 · 유저 소유 장비 개체 · 캐릭터 착용 매핑 · 장착/해제 패킷 · 채취 속도 반영 · 치트 지급 · 창고 칸 번호 필드.
**제외** — 정식 획득 경로(가챠·상자·드롭) · 강화 · 창고 정렬/자리 이동 요청(T-058) · 클라 UI(T-043).

## 2. 결정 사항

| # | 항목 | 결정 |
| --- | --- | --- |
| 1 | 효과 | **산업 지정 + 속도 가산.** `Industry`(None=전 산업) · `SpeedAddPermille`(250 = +25%). 속도식의 가산 항에 합산 |
| 2 | 정의 테이블 | **별도 `Equip.xlsx` / `EquipTable`.** `ItemTable`은 건드리지 않는다 |
| 3 | 종류 vs 칸 | 테이블은 **종류 `EquipKind`**(Weapon·Accessory·Gem), 매핑은 **칸 `EquipSlot`**(Weapon·Accessory1·Accessory2·Gem). 장신구는 두 칸 어디든 |
| 4 | 칸 선택 | **클라가 지정**한다. 서버는 종류가 칸에 맞는지만 검사 |
| 5 | 이미 착용 중인 장비 재장착 | **자동 이동.** 이전 캐릭터에서 해제 → 새 캐릭터 착용. 새 캐릭터 같은 칸의 장비는 창고로 |
| 6 | 저장 구조 | **매핑 테이블.** `t_user_equip`(개체) + `t_character_equip`(캐릭터·칸 → 개체) |
| 7 | 강화 | **지금은 필드 없음.** 기획이 생기면 `ALTER TABLE ADD COLUMN` |
| 8 | 창고 칸 | `slot_position` 컬럼·패킷 필드 + **새 장비는 첫 빈 칸.** 정렬·이동은 T-058 |
| 9 | 획득 | **치트 `GiveEquip`만.** |
| 10 | 보석 | 독립 부위(일감 확정 표 그대로) |
| 11 | 적성 | 바뀌지 않는다. 속도 변화는 기존 슬롯 싱크로 전달 — **T-022 불필요** |

## 3. 데이터 (엑셀)

`Enum.xlsx` — 뒤에만 추가, DB에 정수 저장.

| 시트 | 값 |
| --- | --- |
| `EquipKind` | Weapon=1 · Accessory=2 · Gem=3 |
| `EquipSlot` | Weapon=1 · Accessory1=2 · Accessory2=3 · Gem=4 |

`Equip.xlsx` / 시트 `EquipTable`:

| 컬럼 | Type | 비고 |
| --- | --- | --- |
| `EquipTID` | int | 유일. 종류×1000 + 순번 (1001~ 무기 · 2001~ 장신구 · 3001~ 보석). **산업 전용은 둘째 자리가 산업**(2026-09-19) — `11xx` 농사 · `12xx` 낚시 · `13xx` 채굴 · `14xx` 벌목 · `15xx` 사냥, 끝자리가 등급 |
| `Name` | string | |
| `GlobalRarity` | eGlobalRarity | |
| `EquipKind` | eEquipKind | 허용 칸을 정한다 |
| `Industry` | eIndustryType | None(0) = 전 산업. `Ref` 없이 enum 검사 |
| `SpeedAddPermille` | int [−999..] | 가산 천분율. `Default(Null)` 없음 |
| `BasePrice` | int [0..] | 판매 기준가. 지금은 읽지 않는다 |
| `Description` | string `""` | 메모 |

행 구성 — **2026-09-19에 69종으로 채웠다**(그전에는 테스트용 5행).
무기 31(산업 전용 5×6 + 입문 1) · 장신구 7(전 산업 6 + 구 번호 1) · 보석 31(산업 전용 5×6 + 입문 1).
사다리와 그렇게 가른 이유는 [캐릭터 1.3](../../../GameDesign/design/character/README.md) — **수치의 단일 진실은 엑셀이다.**
구 번호 `1002`·`2002`는 대역 규칙 밖이지만 DB에 살아 있을 수 있어 지우지 않았다.

`EquipCatalog`(서버): TID → 행, `CanEquip(EquipKind, EquipSlot)` 규칙. 기동 시 `GameTable.LoadAll` 뒤 `LoadAll`.

## 4. DB

```sql
CREATE TABLE t_user_equip (
    equip_id      INTEGER PRIMARY KEY,                        -- 장비 개체 PK (rowid 별칭, DB 발급)
    user_id       INTEGER NOT NULL,                           -- 소유 유저 (t_user.user_id 참조). 착용과 무관하게 항상 유저 소유
    equip_tid     INTEGER NOT NULL,                           -- 장비 종류 (EquipTable.EquipTID). 번호 재사용 금지
    slot_position INTEGER NOT NULL DEFAULT 0,                 -- 창고 장비 탭 칸 번호 (0부터). 새 장비는 첫 빈 칸
    created_at    TEXT    NOT NULL DEFAULT (datetime('now'))  -- 획득 시각 (UTC)
) STRICT;

CREATE TABLE t_character_equip (
    character_id INTEGER NOT NULL,          -- 착용 캐릭터 개체 (t_character.character_id)
    slot         INTEGER NOT NULL,          -- 칸 = GameData.EquipSlot (무기1 장신구2·3 보석4)
    equip_id     INTEGER NOT NULL UNIQUE,   -- 착용 장비 개체 (t_user_equip.equip_id). 한 장비는 한 곳에만
    PRIMARY KEY (character_id, slot)        -- (캐릭터, 칸) 유일 = 한 칸에 하나. 로그인 조회는 유저의 캐릭터 목록으로 IN 조회
) STRICT;
```

인덱스: `t_user_equip(user_id)`는 로그인 조회 1회뿐이라 두지 않는다(유저당 행이 수십 개 이하).

## 5. 패킷 (`MikaProtocol`)

```
EquipInfo { long EquipId; int EquipTid; long EquippedCharacterId (0=창고); EEquipSlot EquippedSlot; int SlotPosition }

S_EquipListResponse { List<EquipInfo> Equips }          로그인 스냅샷. 캐릭터 목록 뒤 · 슬롯 스냅샷 앞
S_EquipSyncResponse { List<EquipInfo> Equips }          바뀐 개체만. 지급·장착·해제·자동 이동(최대 3개)
C_EquipRequest      { long CharacterId; long EquipId; EEquipSlot Slot }
C_UnequipRequest    { long CharacterId; EEquipSlot Slot }
S_EquipResponse     { EResultCode Result; long CharacterId; EEquipSlot Slot }
```

- `EEquipSlot`(프로토콜)은 `GameData.EquipSlot`과 이름·값 1:1 — `PacketEnumTest`에 대조 추가.
- `EResultCode` 600번대: `EquipNotOwned=600` · `InvalidEquipSlot=601` · `EquipKindMismatch=602` · `EquipSlotEmpty=603`(해제할 것이 없음). 캐릭터 미보유는 기존 `CharacterNotOwned`.
- `ECheatCommand.GiveEquip = 8` — Arg1 = EquipTID. `Server/docs/치트.md`에 등록.

## 6. 서버 흐름

**적재** — `LoginRepository`가 `t_user_equip`(유저)·`t_character_equip`(유저 캐릭터 IN)을 읽어 `PlayerLoginData`에 싣는다. `LoadEquips`는 캐릭터 뒤·작업슬롯 앞. 캐릭터가 없어진 매핑·테이블에 없는 TID는 경고만 남기고 건너뛴다.

**메모리** — `User.Equip.cs`(partial): `Dictionary<long, Equip> _equips`, `Dictionary<(long CharacterId, EquipSlot), long> _equippedBy`. `Equip`은 `Id · Row · EquippedCharacterId · EquippedSlot · SlotPosition`.

**장착 `TryEquip(characterId, equipId, slot, now)`**
1. 캐릭터 미보유 → `CharacterNotOwned` · 장비 미보유 → `EquipNotOwned` · 칸 범위 밖 → `InvalidEquipSlot` · 종류 불일치 → `EquipKindMismatch`.
2. 이미 그 캐릭터 그 칸에 그 장비면 `Ok`로 끝(멱등).
3. `SettleWorkStation(now)` — 속도가 바뀌기 전에 정산.
4. 메모리 갱신: 장비의 이전 착용 (캐릭터, 칸) 해제 → 대상 칸에 있던 장비 창고로 → 새 매핑.
5. `RefreshWorkStationSpeed(now)` — 영향받은 슬롯이 `S_WorkStationSlotSyncResponse`로 나간다.
6. `SaveCharacterEquipRepository`(트랜잭션: DELETE 이전 매핑·DELETE 대상 칸·INSERT) → `S_EquipResponse(Ok)` + `S_EquipSyncResponse`(바뀐 개체 전부).

**해제 `TryUnequip(characterId, slot, now)`** — 비어 있으면 `EquipSlotEmpty`. 정산 → 매핑 제거 → 속도 재확정 → DELETE → 응답 + 싱크.

**속도** — `ResolveSlotSpeed`: 배치 캐릭터의 착용 장비 중 `Industry == 슬롯 산업 || Industry == None`인 것의 `SpeedAddPermille`를 합해 `.Add()`. 정산 → 변경 순서는 기존 경로라 소급이 없다.

**지급** — `GrantEquipRepository`(INSERT RETURNING) → `OnEquipGranted(equipId, tid)` → 첫 빈 `slot_position` 배정(메모리에서 계산해 INSERT에 포함) → `S_EquipSyncResponse`.

**저장 실패** — 기존 규약대로 `OnDbFailed`가 세션을 끊는다. 되돌리는 코드를 두지 않는다.

## 7. 테스트

| 파일 | 내용 |
| --- | --- |
| `Common/EquipCatalogTest` | 종류↔칸 허용표 · 중복 TID 예외 |
| `User/UserEquipTest` | 장착·해제 · 자동 이동(3개체 싱크) · 종류 불일치 · 미보유 · 멱등 · 배치 중 장착 시 정산 후 속도 반영 · 산업 불일치 장비는 가산 0 |
| `Repository/EquipRepositoryTest` | `:memory:` SQLite로 매핑 트랜잭션 · UNIQUE 위반 시 롤백 |
| `Protocol/PacketEnumTest` | `EEquipSlot` ↔ `EquipSlot` |

실서버 확인: Unity CLI eval로 치트 지급 → 장착 → `S_WorkStationSlotSyncResponse`의 `CurrentWorkSpeed` 변화.

## 8. 문서 반영

`character/README.md`(장비 절 확정) · `workslot/README.md` 3.4(가산 항에 장비) · `게임기획코어.md` 5장 표 · `Server/docs/치트.md` · `Server/docs/채취-정산.md` 5장 · `tasks/T-002` 갱신 · 프로토콜 미러 커밋.
