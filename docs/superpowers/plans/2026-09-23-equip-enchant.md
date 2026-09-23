# 장비 인챈트 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 장비 개체에 `(인챈트 등급, 옵션 줄 2~3개)`를 붙여 속도·경험치 가산을 주고, 아이템으로 부여·재롤·줄 확장을 한다.

**Architecture:** 엑셀 3시트(`EnchantOptionTable`·`EnchantGradeTable`·`EnchantItemTable`)가 옵션 풀·등급 확률·아이템 동작을 모두 데이터로 쥔다. 서버는 `EnchantCatalog` 하나가 그 셋을 읽고, `User.TryEnchant`가 아이템을 소모해 상태를 바꾼다. **착용 중인 장비는 거부**하므로 정산·속도 재확정 경로를 건드리지 않는다 — 바뀐 값은 다음 장착 시 기존 `TryEquip`이 반영한다.

**Tech Stack:** .NET 10 · SQLite(Dapper) · MemoryPack(패킷) · xUnit + Shouldly + Moq

**Spec:** [`docs/superpowers/specs/2026-09-23-equip-enchant-design.md`](../specs/2026-09-23-equip-enchant-design.md)

## Global Constraints

- **서버 코드는 한글 주석.** 주석은 꼭 필요한 곳에만 — [`server-code-style`](../../../Server/.claude/skills/server-code-style/SKILL.md)
- **테스트 이름은 한글로 동작을 서술**한다 (`착용_중인_장비는_인챈트가_거부된다`). `using` 없이 xUnit·Shouldly·Moq 전역 등록됨
- **엑셀은 `GameDesign/Excel`에서만 고친다.** 생성물(`Server/GameData`·`Server/Shared/Data`·`GameDesign/DataLog`·Unity 미러)은 직접 수정 금지
- **엑셀 수정 시 [`excel-table-creator`](../../../.claude/skills/common/excel-table-creator/SKILL.md) 스킬을 매번 연다.** 파일명·시트명·컬럼명은 만들기 전에 사용자 확인
- **패킷 정의는 `Server/MikaProtocol`에서만** 수정한다. `Assets/Scripts_Server/Protocol`은 post-build가 덮어쓰는 사본
- **`Enum.xlsx`의 enum은 뒤에만 추가한다.** DB에 정수로 저장되므로 중간 삽입 금지
- **`EnchantOptionTID`는 번호를 재사용하지 않는다.** DB에 저장된다
- **DB 테이블은 `STRICT`, 모든 컬럼에 한글 인라인 주석** — [`sqlite-sql-creator`](../../../Server/.claude/skills/sqlite-sql-creator/SKILL.md)
- **커밋은 `<type>: <한글 제목>`**, trailer 없음. 푸시하지 않는다 — [`commit-convention`](../../../.claude/skills/common/commit-convention/SKILL.md)
- 실행 중인 `WSGameServer.exe`가 있으면 DLL 잠금(MSB3021)으로 빌드가 실패한다. 종료하고 돌린다
- 기본 줄 수 **2**, 상한 **3**. 등급 상승 확률 Rare→Epic **50‰**, Epic→Legendary **5‰**

---

## File Structure

| 파일 | 책임 |
| --- | --- |
| `GameDesign/Excel/Enum.xlsx` | `EnchantOptionType`·`EnchantAction` 추가 |
| `GameDesign/Excel/EquipEnchant.xlsx` | 3시트 — 옵션 풀 · 등급 확률 · 아이템 동작 |
| `GameDesign/Excel/Item.xlsx` | 인챈트 아이템 4종(`Special`) |
| `GameDesign/Excel/Gacha.xlsx` | 상자 개봉 풀(`GachaId` 6·7·8)에 인챈트 아이템 |
| `Server/WSGameServer/Common/EnchantCatalog.cs` | **신규.** 3시트 보관소 + 줄 뽑기 + 등급 상승 확률 |
| `Server/WSGameServer/User/Equip/Equip.cs` | 인챈트 상태 보유 + 효과 합산(`SpeedAddPermilleFor`·`ExpAddPermille`) |
| `Server/WSGameServer/User/User.Enchant.cs` | **신규.** `TryEnchant` — 검증·소모·판정·저장·응답 |
| `Server/WSGameServer/User/User.Equip.cs` | `LoadEquips`가 인챈트 컬럼을 싣고, `ToEquipInfo`가 패킷에 담는다 |
| `Server/WSGameServer/Repository/EquipRepository.cs` | `SaveEquipEnchantRepository` 추가 |
| `Server/WSGameServer/Repository/RepositoryContracts.cs` | `UserEquipRow`에 컬럼 4개 |
| `Server/WSGameServer/Repository/LoginRepository.cs` | SELECT에 컬럼 4개 |
| `Server/WSGameServer/User/User.WorkStation.cs` | `ResolveSlotSpeed` 가산 · 경험치 가산 |
| `Server/WSGameServer/Network/ClientPacketHandler.cs` | `Handle_C_EquipEnchantRequest` |
| `Server/MikaProtocol/MikaPacket.cs` · `PacketInfo.cs` · `PacketEnum.cs` | 패킷 2개 · `EquipInfo` 확장 · `EResultCode` 5개 |

**치트는 추가하지 않는다** — 인챈트 아이템은 평범한 아이템이라 기존 `ECheatCommand.GiveItem`(Arg1=ItemTID · Arg2=개수)이 이미 지급한다.

---

### Task 1: 엑셀 데이터와 파이프라인

**Files:**
- Modify: `GameDesign/Excel/Enum.xlsx`
- Create: `GameDesign/Excel/EquipEnchant.xlsx`
- Modify: `GameDesign/Excel/Item.xlsx` · `GameDesign/Excel/Gacha.xlsx`
- Generated: `Server/GameData/` · `Server/Shared/Data/` · `GameDesign/DataLog/` · Unity 미러

**Interfaces:**
- Consumes: 없음 (첫 작업)
- Produces: `GameData.EnchantOptionType`(`Speed=1`·`CharacterExp=2`) · `GameData.EnchantAction`(`Grant=1`·`GradeUp=2`·`ExpandLine=3`) · Row 클래스 `EnchantOptionTableRow`(`EnchantOptionTID`·`Grade`·`OptionType`·`Industry`·`Value`·`Weight`) · `EnchantGradeTableRow`(`Grade`·`UpPermille`) · `EnchantItemTableRow`(`ItemTID`·`Action`·`SuccessPermille`) · `GameTable.EnchantOptionTable.All` 등

- [ ] **Step 1: `excel-table-creator` 스킬을 연다**

스킬을 읽고 그 절차대로 한다 — 기억으로 대신하지 않는다.

**이름은 2026-09-23에 사용자 확인을 받았다**(파일 `EquipEnchant.xlsx` · 시트 3종 · 아이템 5종 이름과 TID `100013~100017`).
**컬럼명이나 이름을 바꿔야 할 일이 생기면 다시 확인받는다.**

열려 있는 엑셀이 있으면 그 파일만 저장하지 않고 닫는다(파일 잠금으로 파이프라인이 즉시 실패한다).

- [ ] **Step 2: `Enum.xlsx`에 enum 2개를 뒤에 추가**

기존 enum 시트 규약(`EID` 컬럼)을 그대로 따른다. **중간에 끼워 넣지 않는다.**

| enum | 멤버 |
| --- | --- |
| `EnchantOptionType` | `None=0` · `Speed=1` · `CharacterExp=2` |
| `EnchantAction` | `None=0` · `Grant=1` · `GradeUp=2` · `ExpandLine=3` |

- [ ] **Step 3: `EquipEnchant.xlsx` / `EnchantOptionTable` 시트 작성**

마커 행: `Type` · `Min` · `Max` · `Default(Null)` · `Ref`.

| 컬럼 | Type | Min | Max | Default(Null) | Ref |
| --- | --- | --- | --- | --- | --- |
| `EnchantOptionTID` | `int` | 1 | | | |
| `Grade` | `eGlobalRarity` | | | | |
| `OptionType` | `eEnchantOptionType` | | | | |
| `Industry` | `eIndustryType` | | | | |
| `Value` | `int` | 1 | | | |
| `Weight` | `int` | 1 | | | |
| `Description` | `string` | | | `""` | |

테스트값 행 (등급이 오를수록 값이 커지고 상위 옵션의 `Weight`가 낮다):

| `EnchantOptionTID` | `Grade` | `OptionType` | `Industry` | `Value` | `Weight` | `Description` |
| --- | --- | --- | --- | --- | --- | --- |
| 101 | Rare | Speed | None | 20 | 300 | 전 산업 +2% |
| 102 | Rare | Speed | Farming | 40 | 120 | 농사 +4% |
| 103 | Rare | Speed | Fishing | 40 | 120 | 낚시 +4% |
| 104 | Rare | Speed | Mining | 40 | 120 | 채굴 +4% |
| 105 | Rare | Speed | Logging | 40 | 120 | 벌목 +4% |
| 106 | Rare | Speed | Hunting | 40 | 120 | 사냥 +4% |
| 107 | Rare | CharacterExp | None | 30 | 100 | 경험치 +3% |
| 201 | Epic | Speed | None | 50 | 300 | 전 산업 +5% |
| 202 | Epic | Speed | Farming | 90 | 120 | 농사 +9% |
| 203 | Epic | Speed | Fishing | 90 | 120 | 낚시 +9% |
| 204 | Epic | Speed | Mining | 90 | 120 | 채굴 +9% |
| 205 | Epic | Speed | Logging | 90 | 120 | 벌목 +9% |
| 206 | Epic | Speed | Hunting | 90 | 120 | 사냥 +9% |
| 207 | Epic | CharacterExp | None | 70 | 100 | 경험치 +7% |
| 301 | Legendary | Speed | None | 100 | 300 | 전 산업 +10% |
| 302 | Legendary | Speed | Farming | 180 | 120 | 농사 +18% |
| 303 | Legendary | Speed | Fishing | 180 | 120 | 낚시 +18% |
| 304 | Legendary | Speed | Mining | 180 | 120 | 채굴 +18% |
| 305 | Legendary | Speed | Logging | 180 | 120 | 벌목 +18% |
| 306 | Legendary | Speed | Hunting | 180 | 120 | 사냥 +18% |
| 307 | Legendary | CharacterExp | None | 150 | 100 | 경험치 +15% |

> `CharacterExp` 행의 `Industry`는 `None`으로 둔다 — 경험치에는 산업 구분이 없다.

- [ ] **Step 4: `EnchantGradeTable` 시트 작성**

| 컬럼 | Type | Min | Max | Default(Null) |
| --- | --- | --- | --- | --- |
| `Grade` | `eGlobalRarity` | | | |
| `UpPermille` | `int` | 0 | 1000 | |
| `Description` | `string` | | | `""` |

| `Grade` | `UpPermille` | `Description` |
| --- | --- | --- |
| Rare | 50 | → Epic 5% |
| Epic | 5 | → Legendary 0.5% |
| Legendary | 0 | 최고 등급 — 재롤만 |

- [ ] **Step 5: `EnchantItemTable` 시트 작성**

| 컬럼 | Type | Min | Max | Default(Null) | Ref |
| --- | --- | --- | --- | --- | --- |
| `ItemTID` | `int` | 1 | | | `ItemTable.ItemTID` |
| `Action` | `eEnchantAction` | | | | |
| `SuccessPermille` | `int` | 1 | 1000 | | |
| `Description` | `string` | | | `""` | |

| `ItemTID` | `Action` | `SuccessPermille` | `Description` |
| --- | --- | --- | --- |
| 100013 | Grant | 500 | 인챈트 원석 — 50% 부여 |
| 100014 | Grant | 1000 | 정제된 인챈트 원석 — 확정 부여 |
| 100015 | GradeUp | 1000 | 인챈트 큐브 — **이 값은 읽지 않는다.** 상승 확률은 `EnchantGradeTable` |
| 100016 | ExpandLine | 300 | 인챈트 확장석 — 30% 확장 |
| 100017 | ExpandLine | 1000 | 정제된 인챈트 확장석 — 확정 확장 |

`Ref`가 `ItemTable.ItemTID`를 검사하므로 **Step 6을 먼저 하지 않으면 파이프라인이 실패한다.**

- [ ] **Step 6: `Item.xlsx`에 인챈트 아이템 5종 추가**

`ItemType = Special`. 기존 특수 아이템이 `100001~100012`를 쓰므로 **`100013`부터** 붙인다 —
**번호를 재사용하지 않는다.** `OpenGachaId = 0`(상자가 아니다), `MaxStack = 9999`.

| `ItemTID` | `Name` | `GlobalRarity` | `BasePrice` | `Description` |
| --- | --- | --- | --- | --- |
| 100013 | 인챈트 원석 | Uncommon | 200 | 인챈트 부여 50% |
| 100014 | 정제된 인챈트 원석 | Rare | 600 | 인챈트 부여 확정 |
| 100015 | 인챈트 큐브 | Rare | 500 | 등급 판정 + 줄 재롤 |
| 100016 | 인챈트 확장석 | Uncommon | 300 | 3줄 확장 30% |
| 100017 | 정제된 인챈트 확장석 | Epic | 1200 | 3줄 확장 확정 |

`Name`은 사용자 확인을 받은 값이다(2026-09-23).

- [ ] **Step 7: `Gacha.xlsx`의 `GachaItemTable`에 인챈트 아이템을 넣는다**

상자를 **열었을 때** 나오는 풀이다 — `GachaId` 6(나무) · 7(은) · 8(황금).
**`CommonRewardTable`은 건드리지 않는다**: 그건 채취 판정마다 *상자가* 떨어질 확률이다.

기존 행 형태(`GachaItemTID`·`GachaId`·`ItemTID`·`Count`·`Weight`·`Description`)를 그대로 따른다.
`GachaItemTID`는 풀별 대역을 지킨다(`6001~`·`7001~`·`8001~`). 테스트값:

| `GachaItemTID` | `GachaId` | `ItemTID` | `Count` | `Weight` | `Description` |
| --- | --- | --- | --- | --- | --- |
| (6 풀 다음 번호) | 6 | 100013 | 1 | 40 | 인챈트 원석 |
| (6 풀 다음 번호) | 6 | 100016 | 1 | 30 | 인챈트 확장석 |
| (7 풀 다음 번호) | 7 | 100013 | 1 | 60 | 인챈트 원석 |
| (7 풀 다음 번호) | 7 | 100015 | 1 | 40 | 인챈트 큐브 |
| (7 풀 다음 번호) | 7 | 100016 | 1 | 40 | 인챈트 확장석 |
| (8 풀 다음 번호) | 8 | 100015 | 1 | 80 | 인챈트 큐브 |
| (8 풀 다음 번호) | 8 | 100014 | 1 | 20 | 정제된 인챈트 원석 |
| (8 풀 다음 번호) | 8 | 100017 | 1 | 15 | 정제된 인챈트 확장석 |

**상자 등급이 오를수록 확정형이 나온다** — 나무 상자에는 확정형을 넣지 않는다.

- [ ] **Step 8: 파이프라인 실행**

```powershell
powershell -File GameDesign/generate-tables.ps1
```

Expected: `[완료] 코드(GameData) + 바이너리(Shared/Data) + 로그(DataLog) 생성 완료`
`Server/GameData/EnchantOptionTable.cs` 등 3개가 생기고 `GameDesign/DataLog/EnchantOptionTable.json`이 값을 그대로 담는다.

실패하면 메시지를 읽는다 — `Ref` 무결성(`ItemTable.ItemTID`에 없는 TID)과 엑셀 파일 잠금이 가장 흔하다.

- [ ] **Step 9: 서식 정리 후 커밋**

```powershell
python GameDesign/format-excel.py
```

```bash
git add GameDesign/Excel GameDesign/DataLog Server/GameData Server/Shared/Data Assets/Scripts_Server/GameData Assets/StreamingAssets/Data
git commit -m "feat: 인챈트 데이터 테이블 추가

- EquipEnchant.xlsx 3시트(옵션 풀·등급 확률·아이템 동작)
- Enum.xlsx에 EnchantOptionType·EnchantAction 추가
- 인챈트 아이템 5종을 Item.xlsx·상자 개봉 풀(GachaItemTable)에 등록"
```

---

### Task 2: DB 컬럼 추가

**Files:**
- Modify: `Server/Shared/game.sqlite3` (DDL 실행)
- Modify: `Server/WSGameServer.Tests/Repository/SqliteFixture.cs:86-101`
- Modify: `Server/WSGameServer/Repository/RepositoryContracts.cs:65-71`
- Modify: `Server/WSGameServer/Repository/LoginRepository.cs:78`
- Test: `Server/WSGameServer.Tests/Repository/EquipRepositoryTest.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `UserEquipRow`에 `int enchant_grade` · `int enchant_1` · `int enchant_2` · `int enchant_3`

- [ ] **Step 1: 운영 DB에 DDL을 적용한다**

```sql
ALTER TABLE t_user_equip ADD COLUMN enchant_grade INTEGER NOT NULL DEFAULT 0;  -- 인챈트 등급. 0=없음, 그 외 GlobalRarity 정수
ALTER TABLE t_user_equip ADD COLUMN enchant_1     INTEGER NOT NULL DEFAULT 0;  -- 1번째 옵션 줄 (EnchantOptionTable.EnchantOptionTID). 0=빈 줄
ALTER TABLE t_user_equip ADD COLUMN enchant_2     INTEGER NOT NULL DEFAULT 0;  -- 2번째 옵션 줄
ALTER TABLE t_user_equip ADD COLUMN enchant_3     INTEGER NOT NULL DEFAULT 0;  -- 3번째 옵션 줄. 0이면 2줄짜리다
```

`Server/Shared/game.sqlite3`에 실행한다. `DEFAULT 0`이라 기존 행은 전부 "인챈트 없음"으로 읽힌다.

- [ ] **Step 2: `SqliteFixture.CreateEquipTables`의 `t_user_equip`을 운영과 같게 맞춘다**

`CREATE TABLE`에 컬럼 4개를 더한다. 주석은 "운영 DDL과 같아야 한다"는 기존 doc 주석의 약속이다.

```csharp
CREATE TABLE t_user_equip (
    equip_id      INTEGER PRIMARY KEY,
    user_id       INTEGER NOT NULL,
    equip_tid     INTEGER NOT NULL,
    slot_position INTEGER NOT NULL DEFAULT 0,
    enchant_grade INTEGER NOT NULL DEFAULT 0,
    enchant_1     INTEGER NOT NULL DEFAULT 0,
    enchant_2     INTEGER NOT NULL DEFAULT 0,
    enchant_3     INTEGER NOT NULL DEFAULT 0,
    created_at    TEXT    NOT NULL DEFAULT (datetime('now'))
) STRICT;
```

- [ ] **Step 3: 실패하는 테스트를 쓴다**

`Server/WSGameServer.Tests/Repository/EquipRepositoryTest.cs`에 추가:

```csharp
[Fact]
public async Task 인챈트_컬럼은_기본값_0으로_읽힌다()
{
    using var fx = new SqliteFixture();
    fx.CreateEquipTables();
    fx.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (7, 1, 1001)");

    var rows = (await fx.Connection.QueryAsync<UserEquipRow>(
        "SELECT equip_id, equip_tid, slot_position, enchant_grade, enchant_1, enchant_2, enchant_3 FROM t_user_equip")).ToList();

    rows.Count.ShouldBe(1);
    rows[0].enchant_grade.ShouldBe(0);
    rows[0].enchant_1.ShouldBe(0);
    rows[0].enchant_2.ShouldBe(0);
    rows[0].enchant_3.ShouldBe(0);
}
```

> `SqliteFixture`의 기존 테스트가 커넥션·`Execute`를 어떻게 여는지 보고 그 형태에 맞춘다 — 위 코드의 `fx.Connection`·`fx.Execute`는 기존 멤버 이름을 따른다.

- [ ] **Step 4: 실패를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter 인챈트_컬럼은_기본값_0으로_읽힌다
```

Expected: FAIL — `UserEquipRow`에 `enchant_grade` 속성이 없어 컴파일 오류.

- [ ] **Step 5: `UserEquipRow`에 컬럼 4개를 더한다**

```csharp
// t_user_equip 조회 전용 Row. equip_id는 개체 PK(long), equip_tid는 테이블 정의(int).
public sealed record UserEquipRow
{
    public long equip_id      { get; init; }
    public int  equip_tid     { get; init; }
    public int  slot_position { get; init; }

    /// <summary>인챈트 등급. 0이면 인챈트 없음, 그 외는 GlobalRarity 정수다.</summary>
    public int enchant_grade { get; init; }

    // 옵션 줄. 0은 빈 줄이며, enchant_3가 0이면 2줄짜리다 — 줄 수를 따로 저장하지 않는다.
    public int enchant_1 { get; init; }
    public int enchant_2 { get; init; }
    public int enchant_3 { get; init; }
}
```

- [ ] **Step 6: `LoginRepository`의 SELECT에 컬럼을 더한다**

`Server/WSGameServer/Repository/LoginRepository.cs:78`

```csharp
_equipRows = await connection.QueryAsync<UserEquipRow>(
    @"SELECT equip_id, equip_tid, slot_position, enchant_grade, enchant_1, enchant_2, enchant_3
      FROM t_user_equip WHERE user_id = @userId",
    new { userId = User.Uid });
```

- [ ] **Step 7: 테스트가 통과하는지 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter 인챈트_컬럼은_기본값_0으로_읽힌다
```

Expected: PASS

- [ ] **Step 8: 전체 테스트를 돌린다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

Expected: 기존 테스트 전부 통과 (컬럼 추가는 기존 동작을 바꾸지 않는다)

- [ ] **Step 9: 커밋**

```bash
git add Server/WSGameServer/Repository Server/WSGameServer.Tests/Repository
git commit -m "feat: t_user_equip에 인챈트 컬럼 추가

- enchant_grade와 옵션 줄 3개. 기존 행은 DEFAULT 0으로 인챈트 없음
- 줄 수는 enchant_3 != 0으로 유도한다 — 별도 컬럼을 두지 않는다"
```

---

### Task 3: EnchantCatalog

**Files:**
- Create: `Server/WSGameServer/Common/EnchantCatalog.cs`
- Test: `Server/WSGameServer.Tests/Common/EnchantCatalogTest.cs`

**Interfaces:**
- Consumes: Task 1의 `EnchantOptionTableRow` · `EnchantGradeTableRow` · `EnchantItemTableRow` · `GameTable`
- Produces:
  - `EnchantCatalog.BaseLineCount = 2` · `MaxLineCount = 3` (const int)
  - `void LoadAll()`
  - `void Load(IEnumerable<EnchantOptionTableRow>, IEnumerable<EnchantGradeTableRow>, IEnumerable<EnchantItemTableRow>)`
  - `bool TryGetOption(int optionTid, out EnchantOptionTableRow row)`
  - `bool TryGetItem(int itemTid, out EnchantItemTableRow row)`
  - `int UpPermilleOf(GlobalRarity grade)`
  - `List<EnchantOptionTableRow> RollOptions(GlobalRarity grade, int lineCount, Random? random = null)`
  - `static GlobalRarity NextGrade(GlobalRarity grade)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Server/WSGameServer.Tests/Common/EnchantCatalogTest.cs`:

```csharp
using GameData;

namespace WSGameServer;

/// <summary>
/// <see cref="EnchantCatalog"/> — 등급별 옵션 풀 · 등급 상승 확률 · 아이템 동작.
/// 확률이 코드가 아니라 데이터에 있으므로, 여기서 보는 것은 "표를 그대로 읽었는가"다.
/// </summary>
public class EnchantCatalogTest
{
    private static readonly EnchantOptionTableRow[] Options =
    {
        new() { EnchantOptionTID = 101, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed,        Industry = IndustryType.None,    Value = 20, Weight = 300 },
        new() { EnchantOptionTID = 103, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed,        Industry = IndustryType.Fishing, Value = 40, Weight = 120 },
        new() { EnchantOptionTID = 107, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None,    Value = 30, Weight = 100 },
        new() { EnchantOptionTID = 201, Grade = GlobalRarity.Epic, OptionType = EnchantOptionType.Speed,        Industry = IndustryType.None,    Value = 50, Weight = 300 },
    };

    private static readonly EnchantGradeTableRow[] Grades =
    {
        new() { Grade = GlobalRarity.Rare,      UpPermille = 50 },
        new() { Grade = GlobalRarity.Epic,      UpPermille = 5 },
        new() { Grade = GlobalRarity.Legendary, UpPermille = 0 },
    };

    private static readonly EnchantItemTableRow[] Items =
    {
        new() { ItemTID = 9001, Action = EnchantAction.Grant,      SuccessPermille = 500 },
        new() { ItemTID = 9003, Action = EnchantAction.GradeUp,    SuccessPermille = 1000 },
        new() { ItemTID = 9004, Action = EnchantAction.ExpandLine, SuccessPermille = 300 },
    };

    private static EnchantCatalog Loaded()
    {
        var catalog = new EnchantCatalog();
        catalog.Load(Options, Grades, Items);
        return catalog;
    }

    [Fact]
    public void 등급별로_그_등급의_옵션만_뽑는다()
    {
        var catalog = Loaded();

        var rolled = catalog.RollOptions(GlobalRarity.Rare, 2, new Random(1));

        rolled.Count.ShouldBe(2);
        rolled.ShouldAllBe(o => o.Grade == GlobalRarity.Rare);
    }

    [Fact]
    public void 같은_줄이_겹쳐_나올_수_있다()
    {
        // 후보를 한 줄로 줄이면 2줄 모두 그 줄이어야 한다 — 중복 허용이 설계다.
        var catalog = new EnchantCatalog();
        catalog.Load(new[] { Options[0] }, Grades, Items);

        var rolled = catalog.RollOptions(GlobalRarity.Rare, 2, new Random(1));

        rolled.Select(o => o.EnchantOptionTID).ShouldBe(new[] { 101, 101 });
    }

    [Fact]
    public void 등급_상승_확률은_현재_등급이_정한다()
    {
        var catalog = Loaded();

        catalog.UpPermilleOf(GlobalRarity.Rare).ShouldBe(50);
        catalog.UpPermilleOf(GlobalRarity.Epic).ShouldBe(5);
        catalog.UpPermilleOf(GlobalRarity.Legendary).ShouldBe(0);
    }

    [Fact]
    public void 다음_등급은_Legendary에서_멈춘다()
    {
        EnchantCatalog.NextGrade(GlobalRarity.Rare).ShouldBe(GlobalRarity.Epic);
        EnchantCatalog.NextGrade(GlobalRarity.Epic).ShouldBe(GlobalRarity.Legendary);
        EnchantCatalog.NextGrade(GlobalRarity.Legendary).ShouldBe(GlobalRarity.Legendary);
    }

    [Fact]
    public void 아이템의_동작과_확률을_돌려준다()
    {
        var catalog = Loaded();

        catalog.TryGetItem(9004, out var row).ShouldBeTrue();
        row.Action.ShouldBe(EnchantAction.ExpandLine);
        row.SuccessPermille.ShouldBe(300);

        catalog.TryGetItem(1, out _).ShouldBeFalse();
    }

    [Fact]
    public void EnchantOptionTID가_중복되면_예외다()
    {
        var catalog = new EnchantCatalog();
        var dup = new[] { Options[0], Options[0] };

        Should.Throw<InvalidOperationException>(() => catalog.Load(dup, Grades, Items));
    }

    [Fact]
    public void 후보가_없는_등급을_뽑으면_예외다()
    {
        var catalog = Loaded();

        Should.Throw<InvalidOperationException>(() => catalog.RollOptions(GlobalRarity.Legendary, 2, new Random(1)));
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter EnchantCatalogTest
```

Expected: FAIL — `EnchantCatalog` 타입이 없어 컴파일 오류.

- [ ] **Step 3: `EnchantCatalog`를 구현한다**

`Server/WSGameServer/Common/EnchantCatalog.cs`:

```csharp
using GameData;
using MikaUtils;

namespace WSGameServer;

// 인챈트 3시트 보관소 — 등급별 옵션 풀 · 등급 상승 확률 · 아이템 동작.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md
public sealed class EnchantCatalog : Singleton<EnchantCatalog>
{
    /// <summary>인챈트를 부여하면 생기는 줄 수.</summary>
    public const int BaseLineCount = 2;

    /// <summary>줄 확장의 상한. 넘기면 EnchantLineMax로 거절한다.</summary>
    public const int MaxLineCount = 3;

    private readonly Dictionary<int, EnchantOptionTableRow>                _optionByTid = new();
    private readonly Dictionary<GlobalRarity, WeightedPicker<EnchantOptionTableRow>> _poolByGrade = new();
    private readonly Dictionary<GlobalRarity, int>                        _upPermille  = new();
    private readonly Dictionary<int, EnchantItemTableRow>                 _itemByTid   = new();

    public void LoadAll()
    {
        Load(GameTable.EnchantOptionTable.All, GameTable.EnchantGradeTable.All, GameTable.EnchantItemTable.All);
    }

    public void Load(
        IEnumerable<EnchantOptionTableRow> options,
        IEnumerable<EnchantGradeTableRow>  grades,
        IEnumerable<EnchantItemTableRow>   items)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grades);
        ArgumentNullException.ThrowIfNull(items);

        _optionByTid.Clear();
        _poolByGrade.Clear();
        _upPermille.Clear();
        _itemByTid.Clear();

        var byGrade = new Dictionary<GlobalRarity, List<EnchantOptionTableRow>>();

        foreach (var row in options)
        {
            if (!_optionByTid.TryAdd(row.EnchantOptionTID, row))
            {
                throw new InvalidOperationException($"EnchantOptionTable에 EnchantOptionTID가 중복됐습니다: {row.EnchantOptionTID}");
            }

            if (!byGrade.TryGetValue(row.Grade, out var list))
            {
                list = new List<EnchantOptionTableRow>();
                byGrade[row.Grade] = list;
            }
            list.Add(row);
        }

        foreach (var (grade, list) in byGrade)
        {
            _poolByGrade[grade] = WeightedPicker<EnchantOptionTableRow>.From(list, r => r.Weight);
        }

        foreach (var row in grades)
        {
            _upPermille[row.Grade] = row.UpPermille;
        }

        foreach (var row in items)
        {
            if (!_itemByTid.TryAdd(row.ItemTID, row))
            {
                throw new InvalidOperationException($"EnchantItemTable에 ItemTID가 중복됐습니다: {row.ItemTID}");
            }
        }
    }

    public bool TryGetOption(int optionTid, out EnchantOptionTableRow row)
        => _optionByTid.TryGetValue(optionTid, out row!);

    public bool TryGetItem(int itemTid, out EnchantItemTableRow row)
        => _itemByTid.TryGetValue(itemTid, out row!);

    /// <summary>다음 등급으로 오를 확률(천분율). 표에 없는 등급은 0 — 오르지 않는다.</summary>
    public int UpPermilleOf(GlobalRarity grade)
        => _upPermille.TryGetValue(grade, out var permille) ? permille : 0;

    /// <summary>그 등급의 풀에서 줄 수만큼 뽑는다. 같은 줄이 겹쳐 나오는 것은 허용이다(잭팟).</summary>
    public List<EnchantOptionTableRow> RollOptions(GlobalRarity grade, int lineCount, Random? random = null)
    {
        if (!_poolByGrade.TryGetValue(grade, out var picker))
        {
            throw new InvalidOperationException($"EnchantOptionTable에 {grade} 등급 옵션이 없습니다.");
        }

        return picker.PickMany(lineCount, random);
    }

    /// <summary>인챈트 등급 사다리. Legendary가 최고라 거기서 멈춘다.</summary>
    public static GlobalRarity NextGrade(GlobalRarity grade)
    {
        return grade switch
        {
            GlobalRarity.Rare => GlobalRarity.Epic,
            GlobalRarity.Epic => GlobalRarity.Legendary,
            _                 => grade,
        };
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter EnchantCatalogTest
```

Expected: PASS (7개)

- [ ] **Step 5: 서버 기동 시 로드를 붙인다**

`EquipCatalog.Instance.LoadAll()`을 부르는 자리를 찾아(`grep -rn "EquipCatalog.Instance.LoadAll" Server/WSGameServer`) 그 옆에 `EnchantCatalog.Instance.LoadAll();`을 더한다. `GameTable.LoadAll` 뒤여야 한다.

- [ ] **Step 6: 커밋**

```bash
git add Server/WSGameServer/Common/EnchantCatalog.cs Server/WSGameServer.Tests/Common/EnchantCatalogTest.cs
git commit -m "feat: EnchantCatalog 추가

- 등급별 옵션 풀은 WeightedPicker로 만들어 재사용한다
- 등급 상승 확률은 EnchantGradeTable이 정한다 — 큐브가 1종이라
  아이템의 SuccessPermille로는 등급별 확률을 담을 수 없다"
```

---

### Task 4: Equip에 인챈트 상태와 효과 합산

**Files:**
- Modify: `Server/WSGameServer/User/Equip/Equip.cs`
- Modify: `Server/WSGameServer/User/User.Equip.cs` (`LoadEquips`)
- Test: `Server/WSGameServer.Tests/User/UserEnchantTest.cs` (신규 — 이 태스크에서는 합산만)

**Interfaces:**
- Consumes: Task 2의 `UserEquipRow` · Task 3의 `EnchantCatalog.TryGetOption`
- Produces:
  - `Equip.EnchantGrade` (`GlobalRarity`, 없으면 `GlobalRarity.None`)
  - `Equip.EnchantOptions` (`IReadOnlyList<EnchantOptionTableRow>`)
  - `Equip.EnchantOptionTids` (`IReadOnlyList<int>`)
  - `Equip.EnchantLineCount` (`int`)
  - `Equip.SetEnchant(GlobalRarity grade, IReadOnlyList<EnchantOptionTableRow> options)`
  - `Equip.SpeedAddPermilleFor(IndustryType industry)` — **기본 + 인챈트**
  - `Equip.ExpAddPermille` (`int`)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Server/WSGameServer.Tests/User/UserEnchantTest.cs`:

```csharp
using GameData;

namespace WSGameServer;

/// <summary>
/// 인챈트 — 효과 합산 · 아이템 3종 동작 · 거절.
/// 착용 중 거부가 설계의 중심이다: 창고 장비만 바뀌므로 정산·속도 재확정을 부르지 않는다.
/// </summary>
public class UserEnchantTest
{
    private static readonly EnchantOptionTableRow FishSpeed =
        new() { EnchantOptionTID = 103, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = 40, Weight = 120 };

    private static readonly EnchantOptionTableRow AllSpeed =
        new() { EnchantOptionTID = 101, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 20, Weight = 300 };

    private static readonly EnchantOptionTableRow FarmSpeed =
        new() { EnchantOptionTID = 102, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Farming, Value = 40, Weight = 120 };

    private static readonly EnchantOptionTableRow Exp =
        new() { EnchantOptionTID = 107, Grade = GlobalRarity.Rare, OptionType = EnchantOptionType.CharacterExp, Industry = IndustryType.None, Value = 30, Weight = 100 };

    // Epic 풀 — 큐브가 Rare에서 Epic으로 올린 뒤 재롤할 대상이 있어야 한다(없으면 RollOptions가 던진다).
    private static readonly EnchantOptionTableRow EpicAllSpeed =
        new() { EnchantOptionTID = 201, Grade = GlobalRarity.Epic, OptionType = EnchantOptionType.Speed, Industry = IndustryType.None, Value = 50, Weight = 300 };

    private static readonly EnchantOptionTableRow EpicFishSpeed =
        new() { EnchantOptionTID = 203, Grade = GlobalRarity.Epic, OptionType = EnchantOptionType.Speed, Industry = IndustryType.Fishing, Value = 90, Weight = 120 };

    private static Equip RodWith(params EnchantOptionTableRow[] options)
    {
        // 낚시 무기 +30%. 인챈트 줄이 그 위에 더해진다.
        var row = new EquipTableRow
        {
            EquipTID = 1002, Name = "대", EquipKind = EquipKind.Weapon,
            Industry = IndustryType.Fishing, SpeedAddPermille = 300,
        };

        var equip = new Equip(11, row, 0);
        equip.SetEnchant(GlobalRarity.Rare, options);
        return equip;
    }

    [Fact]
    public void 산업이_일치하는_줄과_전_산업_줄이_기본값에_더해진다()
    {
        var equip = RodWith(FishSpeed, AllSpeed);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40 + 20);
    }

    [Fact]
    public void 산업이_다른_줄은_더해지지_않는다()
    {
        var equip = RodWith(FarmSpeed, AllSpeed);

        // 장비 자체가 낚시 전용이라 농사 슬롯에서는 기본값도 0이다.
        equip.SpeedAddPermilleFor(IndustryType.Farming).ShouldBe(40 + 20);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 20);
    }

    [Fact]
    public void 경험치_줄은_속도에_섞이지_않는다()
    {
        var equip = RodWith(Exp, FishSpeed);

        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300 + 40);
        equip.ExpAddPermille.ShouldBe(30);
    }

    [Fact]
    public void 인챈트가_없으면_기본값만_남는다()
    {
        var row = new EquipTableRow
        {
            EquipTID = 1002, Name = "대", EquipKind = EquipKind.Weapon,
            Industry = IndustryType.Fishing, SpeedAddPermille = 300,
        };
        var equip = new Equip(11, row, 0);

        equip.EnchantGrade.ShouldBe(GlobalRarity.None);
        equip.EnchantLineCount.ShouldBe(0);
        equip.SpeedAddPermilleFor(IndustryType.Fishing).ShouldBe(300);
        equip.ExpAddPermille.ShouldBe(0);
    }
}
```

> `GlobalRarity.None = 0`이 이미 있다(`Server/GameData/Enum.cs:37`). "인챈트 없음"을 그 값으로 표현하므로 DB의 `enchant_grade = 0`과 그대로 맞물린다.

- [ ] **Step 2: 실패를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter UserEnchantTest
```

Expected: FAIL — `SetEnchant`·`SpeedAddPermilleFor`가 없어 컴파일 오류.

- [ ] **Step 3: `Equip`에 인챈트를 붙인다**

`Server/WSGameServer/User/Equip/Equip.cs` — 기존 `SpeedAddPermille` 프로퍼티는 **남겨 둔다**(테이블 기본값의 이름이다). 새 합산 메서드를 더한다.

```csharp
    /// <summary>인챈트 등급. None이면 인챈트가 없다.</summary>
    public GlobalRarity EnchantGrade { get; private set; }

    private readonly List<EnchantOptionTableRow> _enchantOptions = new();

    /// <summary>옵션 줄. 순서가 곧 DB의 enchant_1~3 순서다.</summary>
    public IReadOnlyList<EnchantOptionTableRow> EnchantOptions => _enchantOptions;

    /// <summary>줄 수. 2 또는 3이며 인챈트가 없으면 0이다.</summary>
    public int EnchantLineCount => _enchantOptions.Count;

    /// <summary>패킷·DB에 싣는 EnchantOptionTID 목록.</summary>
    public IReadOnlyList<int> EnchantOptionTids => _enchantOptions.Select(o => o.EnchantOptionTID).ToList();

    /// <summary>인챈트 상태를 통째로 바꾼다(부여·재롤·확장 모두 이 경로다). 줄은 항상 전부 넘긴다.</summary>
    public void SetEnchant(GlobalRarity grade, IReadOnlyList<EnchantOptionTableRow> options)
    {
        EnchantGrade = grade;
        _enchantOptions.Clear();
        _enchantOptions.AddRange(options);
    }

    /// <summary>이 슬롯 산업에 붙는 속도 가산 — 테이블 기본값 + 인챈트 줄. 산업이 맞지 않는 쪽은 빠진다.</summary>
    public int SpeedAddPermilleFor(IndustryType industry)
    {
        var total = AppliesTo(industry) ? SpeedAddPermille : 0;

        foreach (var option in _enchantOptions)
        {
            if (option.OptionType != EnchantOptionType.Speed)
            {
                continue;
            }
            if (option.Industry != IndustryType.None && option.Industry != industry)
            {
                continue;
            }

            total += option.Value;
        }

        return total;
    }

    /// <summary>캐릭터 경험치 가산(천분율). 경험치에는 산업 구분이 없다.</summary>
    public int ExpAddPermille
    {
        get
        {
            var total = 0;
            foreach (var option in _enchantOptions)
            {
                if (option.OptionType == EnchantOptionType.CharacterExp)
                {
                    total += option.Value;
                }
            }
            return total;
        }
    }
```

- [ ] **Step 4: `LoadEquips`가 인챈트를 싣게 한다**

`Server/WSGameServer/User/User.Equip.cs`의 `LoadEquips` 안, `new Equip(...)` 직후에 더한다.

```csharp
            var equip = new Equip(r.equip_id, row, r.slot_position);

            if (r.enchant_grade != 0)
            {
                // 테이블에 없는 EnchantOptionTID는 그 줄만 버린다 — 데이터 한 줄 때문에 로그인이 막히면 안 된다.
                var options = new List<EnchantOptionTableRow>();
                foreach (var tid in new[] { r.enchant_1, r.enchant_2, r.enchant_3 })
                {
                    if (tid == 0)
                    {
                        continue;
                    }
                    if (!_enchantCatalog.TryGetOption(tid, out var option))
                    {
                        ServerLog.Warn("로그인", $"EnchantOptionTable에 없는 EnchantOptionTID, 건너뜀: {tid} (개체 {r.equip_id})");
                        continue;
                    }
                    options.Add(option);
                }

                equip.SetEnchant((GlobalRarity)r.enchant_grade, options);
            }

            _equips[r.equip_id] = equip;
```

`User`에 `private readonly EnchantCatalog _enchantCatalog;`를 더하고, 생성자에서 `_enchantCatalog = enchants ?? EnchantCatalog.Instance;`로 받는다 — `_equipCatalog`(`User.cs:37,149`)와 같은 형태다. 생성자 매개변수 `EnchantCatalog? enchants = null`을 **기존 선택 매개변수 뒤에** 붙인다.

`TestUserBuilder`에도 `public EnchantCatalog Enchants { get; } = new();`를 더하고 `Build`에서 넘긴다. 비어 있을 때 실데이터를 넣을지는 **넣지 않는다** — 난수 롤이 다른 테스트를 흔들지 않게, `CommonRewards`와 같은 방침이다.

- [ ] **Step 5: 통과를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter UserEnchantTest
```

Expected: PASS (4개)

- [ ] **Step 6: 전체 테스트를 돌린다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

Expected: 전부 통과

- [ ] **Step 7: 커밋**

```bash
git add Server/WSGameServer/User Server/WSGameServer.Tests/User
git commit -m "feat: 장비 개체에 인챈트 상태와 효과 합산 추가

- SpeedAddPermilleFor는 테이블 기본값과 인챈트 줄을 함께 더한다
- 옵션 줄은 Row를 들고 있어 Equip이 카탈로그를 몰라도 된다"
```

---

### Task 5: 패킷

**Files:**
- Modify: `Server/MikaProtocol/PacketInfo.cs:88-95` (`EquipInfo`)
- Modify: `Server/MikaProtocol/MikaPacket.cs` (`PacketId` · 패킷 2개)
- Modify: `Server/MikaProtocol/PacketEnum.cs` (`EResultCode`)
- Modify: `Server/WSGameServer/User/User.Equip.cs` (`ToEquipInfo`)
- Test: `Server/WSGameServer.Tests/Protocol/PacketEnumTest.cs`

**Interfaces:**
- Consumes: Task 4의 `Equip.EnchantGrade` · `EnchantOptionTids`
- Produces: `C_EquipEnchantRequest { long EquipId; int ItemTid }` · `S_EquipEnchantResponse { EResultCode Result; long EquipId; bool Success; int BeforeGrade; int AfterGrade; List<int> Options }` · `EResultCode.EnchantItemNotOwned=610`~`EnchantEquipped=614`

- [ ] **Step 1: `PacketId`에 2개를 뒤에 추가한다**

`MikaPacket.cs`의 `PacketId` enum 끝(`S_MailDeleteResponse = 44` 다음):

```csharp
        C_EquipEnchantRequest = 45,
        S_EquipEnchantResponse = 46,
```

- [ ] **Step 2: `EquipInfo`를 확장한다**

`PacketInfo.cs:88`

```csharp
    public partial class EquipInfo
    {
        public long       EquipId             { get; set; }
        public int        EquipTid            { get; set; }
        public long       EquippedCharacterId { get; set; }  // 0=창고
        public EEquipSlot EquippedSlot        { get; set; }  // 창고면 None
        public int        SlotPosition        { get; set; }
        public int        EnchantGrade        { get; set; }  // 0=인챈트 없음, 그 외 GlobalRarity
        public List<int>  EnchantOptions      { get; set; } = new();  // EnchantOptionTID. 줄 수만큼(0·2·3개)
    }
```

- [ ] **Step 3: 패킷 2개를 추가한다**

`MikaPacket.cs`의 장비 구역(`S_EquipResponse` 다음):

```csharp
    /// <summary>
    /// 인챈트 요청. <b>무엇을 하는지는 아이템이 정한다</b>(EnchantItemTable의 Action) — 클라가 동작을 고르지 않는다.
    /// 착용 중인 장비는 거절된다(EnchantEquipped): 벗기는 것이 선행 조건이다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_EquipEnchantRequest)]
    public partial class C_EquipEnchantRequest : IPacket
    {
        public long EquipId { get; set; }  // 개체 PK
        public int  ItemTid { get; set; }  // 인챈트 아이템 (EnchantItemTable.ItemTID)
    }

    /// <summary>
    /// 인챈트 결과. Success는 아이템의 성공 판정이며, <b>실패해도 GradeUp은 줄을 재롤</b>하므로
    /// Options는 항상 갱신된 값이다. 바뀐 개체는 S_EquipSyncResponse가 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_EquipEnchantResponse)]
    public partial class S_EquipEnchantResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public long        EquipId     { get; set; }
        public bool        Success     { get; set; }
        public int         BeforeGrade { get; set; }
        public int         AfterGrade  { get; set; }
        public List<int>   Options     { get; set; } = new();
    }
```

- [ ] **Step 4: `EResultCode`에 5개를 추가한다**

`PacketEnum.cs`의 600번대 블록(`EquipSlotEmpty = 603` 다음, 700번대 앞):

```csharp
        // ── 610~: 인챈트 ──
        EnchantItemNotOwned = 610, // 인챈트 아이템 미보유
        EnchantAlreadyRolled = 611, // 이미 인챈트가 있는 장비에 부여
        EnchantNotRolled    = 612, // 인챈트가 없는 장비에 재롤·확장
        EnchantLineMax      = 613, // 이미 상한(3줄)
        EnchantEquipped     = 614, // 착용 중 — 벗겨야 인챈트할 수 있다
```

- [ ] **Step 5: `ToEquipInfo`가 인챈트를 싣게 한다**

`User.Equip.cs`의 `ToEquipInfo`를 찾아(`grep -n "ToEquipInfo" Server/WSGameServer/User/User.Equip.cs`) 두 필드를 더한다.

```csharp
            EnchantGrade   = (int)equip.EnchantGrade,
            EnchantOptions = equip.EnchantOptionTids.ToList(),
```

- [ ] **Step 6: `PacketEnumTest`에 실패하는 대조를 추가한다**

기존 `EEquipSlot` ↔ `EquipSlot` 대조가 어떤 형태인지 보고 같은 형태로 쓴다. 이 태스크에서는 **프로토콜 전용 enum을 새로 만들지 않으므로**(등급·EnchantOptionTID를 `int`로 싣는다) 대조할 쌍이 없다. 대신 PacketId 중복만 막는다 — 기존 테스트가 이미 그 일을 한다면 추가하지 않고 아래로 넘어간다.

```csharp
[Fact]
public void PacketId는_값이_겹치지_않는다()
{
    var values = Enum.GetValues<PacketId>().Select(v => (ushort)v).ToList();

    values.Distinct().Count().ShouldBe(values.Count);
}
```

- [ ] **Step 7: 빌드와 테스트**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

Expected: 전부 통과. 빌드 시 post-build가 `Assets/Scripts_Server/Protocol`로 미러링한다 — **Unity 에디터가 켜져 있으면 분석기 DLL 복사가 실패할 수 있다**(빌드는 통과).

- [ ] **Step 8: 커밋**

```bash
git add Server/MikaProtocol Assets/Scripts_Server/Protocol Server/WSGameServer/User Server/WSGameServer.Tests/Protocol
git commit -m "feat: 인챈트 패킷 추가

- 요청 패킷은 하나다. 무엇을 하는지는 EnchantItemTable의 Action이 정한다
- EquipInfo에 등급과 옵션 줄을 실어 기존 싱크 경로를 그대로 쓴다"
```

---

### Task 6: TryEnchant

**Files:**
- Create: `Server/WSGameServer/User/User.Enchant.cs`
- Modify: `Server/WSGameServer/Repository/EquipRepository.cs` (`SaveEquipEnchantRepository` 추가)
- Modify: `Server/WSGameServer/Network/ClientPacketHandler.cs`
- Test: `Server/WSGameServer.Tests/User/UserEnchantTest.cs` (Task 4 파일에 추가)

**Interfaces:**
- Consumes: Task 3 `EnchantCatalog` · Task 4 `Equip.SetEnchant` · Task 5 패킷 · 기존 `User.TryConsumeItems(IReadOnlyDictionary<int,int>, out List<ItemChangeInfo>)`
- Produces: `User.TryEnchant(long equipId, int itemTid, Random? random = null)` · `SaveEquipEnchantRepository(User, long equipId, int grade, int o1, int o2, int o3)`

- [ ] **Step 1: 거절 경로의 실패하는 테스트를 쓴다**

`UserEnchantTest.cs`에 추가. 조립기는 Task 4의 `RodWith`와 달리 `User`를 거쳐야 하므로 헬퍼를 새로 둔다.

```csharp
    private const int GrantTid  = 9001;  // Grant · 확정
    private const int CubeTid   = 9003;  // GradeUp
    private const int ExpandTid = 9004;  // ExpandLine · 확정
    private const int RodTid    = 1002;
    private const long Rod      = 11;
    private const long CharA    = 500;

    private static readonly EnchantGradeTableRow[] GradeRows =
    {
        new() { Grade = GlobalRarity.Rare,      UpPermille = 1000 },  // 테스트에서는 확정 상승으로 둔다
        new() { Grade = GlobalRarity.Epic,      UpPermille = 0 },
        new() { Grade = GlobalRarity.Legendary, UpPermille = 0 },
    };

    private static readonly EnchantItemTableRow[] ItemRows =
    {
        new() { ItemTID = GrantTid,  Action = EnchantAction.Grant,      SuccessPermille = 1000 },
        new() { ItemTID = CubeTid,   Action = EnchantAction.GradeUp,    SuccessPermille = 1000 },
        new() { ItemTID = ExpandTid, Action = EnchantAction.ExpandLine, SuccessPermille = 1000 },
    };

    private static (User User, TestUserBuilder B) UserWithRod(params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = RodTid, Name = "대", EquipKind = EquipKind.Weapon, Industry = IndustryType.Fishing, SpeedAddPermille = 300 },
        });
        b.Enchants.Load(new[] { AllSpeed, FishSpeed, Exp, EpicAllSpeed, EpicFishSpeed }, GradeRows, ItemRows);

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[] { new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0 } }, worn);
        user.GainItem(GrantTid, 5);
        user.GainItem(CubeTid, 5);
        user.GainItem(ExpandTid, 5);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static S_EquipEnchantResponse LastResponse(TestUserBuilder b)
        => b.Channel.SentOf<S_EquipEnchantResponse>().Last();

    [Fact]
    public void 착용_중인_장비는_인챈트가_거부된다()
    {
        var (user, b) = UserWithRod(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryEnchant(Rod, GrantTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantEquipped);
        user.GetItemCount(GrantTid).ShouldBe(5);   // 소모되지 않는다
    }

    [Fact]
    public void 인챈트가_없는_장비에_큐브를_쓰면_거부된다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, CubeTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantNotRolled);
    }

    [Fact]
    public void 이미_인챈트가_있으면_부여가_거부된다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryEnchant(Rod, GrantTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantAlreadyRolled);
    }

    [Fact]
    public void 아이템이_없으면_거부된다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, 99999, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantItemNotOwned);
    }

    [Fact]
    public void 세_줄이_되면_확장이_거부된다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryEnchant(Rod, ExpandTid, new Random(1));

        user.TryEnchant(Rod, ExpandTid, new Random(1));

        LastResponse(b).Result.ShouldBe(EResultCode.EnchantLineMax);
    }
```

- [ ] **Step 2: 성공 경로의 실패하는 테스트를 쓴다**

```csharp
    [Fact]
    public void 부여하면_Rare_두_줄이_생긴다()
    {
        var (user, b) = UserWithRod();

        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        equip.EnchantGrade.ShouldBe(GlobalRarity.Rare);
        equip.EnchantLineCount.ShouldBe(2);

        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeTrue();
        res.BeforeGrade.ShouldBe(0);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Rare);
        res.Options.Count.ShouldBe(2);

        user.GetItemCount(GrantTid).ShouldBe(4);
        b.DB.PostedOf<SaveEquipEnchantRepository>().Count.ShouldBe(1);
        b.Channel.SentOf<S_EquipSyncResponse>().Count.ShouldBe(1);
    }

    [Fact]
    public void 큐브가_성공하면_등급이_오르고_줄을_다시_뽑는다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));

        user.TryEnchant(Rod, CubeTid, new Random(1));

        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        equip.EnchantGrade.ShouldBe(GlobalRarity.Epic);
        equip.EnchantLineCount.ShouldBe(2);   // 줄 수는 그대로다

        var res = LastResponse(b);
        res.BeforeGrade.ShouldBe((int)GlobalRarity.Rare);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Epic);
        res.Success.ShouldBeTrue();
    }

    [Fact]
    public void 큐브가_실패해도_줄은_다시_뽑는다()
    {
        // Epic의 UpPermille이 0이라 상승은 반드시 실패한다.
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryEnchant(Rod, CubeTid, new Random(1));   // Rare → Epic
        b.Channel.Sent.Clear();

        user.TryEnchant(Rod, CubeTid, new Random(7));

        var res = LastResponse(b);
        res.Result.ShouldBe(EResultCode.Ok);
        res.Success.ShouldBeFalse();
        res.BeforeGrade.ShouldBe((int)GlobalRarity.Epic);
        res.AfterGrade.ShouldBe((int)GlobalRarity.Epic);
        res.Options.Count.ShouldBe(2);        // 재롤은 됐다
        user.GetItemCount(CubeTid).ShouldBe(3);
    }

    [Fact]
    public void 확장하면_세_줄이_되고_새_줄만_늘어난다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryGetEquip(Rod, out var before).ShouldBeTrue();
        var kept = before.EnchantOptionTids.ToList();

        user.TryEnchant(Rod, ExpandTid, new Random(1));

        user.TryGetEquip(Rod, out var after).ShouldBeTrue();
        after.EnchantLineCount.ShouldBe(3);
        after.EnchantOptionTids.Take(2).ShouldBe(kept);   // 기존 줄은 유지된다
    }
```

- [ ] **Step 3: 실패를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter UserEnchantTest
```

Expected: FAIL — `TryEnchant`가 없어 컴파일 오류.

- [ ] **Step 4: `SaveEquipEnchantRepository`를 추가한다**

`Server/WSGameServer/Repository/EquipRepository.cs` 끝에:

```csharp
/// <summary>
/// 장비 개체의 인챈트를 저장한다. 줄을 전부 덮어쓰므로 UPDATE 1행으로 끝난다 —
/// 재롤이 "줄 전부 교체"라서 부분 갱신이 없다.
/// </summary>
public sealed class SaveEquipEnchantRepository : IRepository
{
    private readonly long _equipId;
    private readonly int  _grade;
    private readonly int  _option1;
    private readonly int  _option2;
    private readonly int  _option3;

    public SaveEquipEnchantRepository(User user, long equipId, int grade, int option1, int option2, int option3)
    {
        User     = user;
        _equipId = equipId;
        _grade   = grade;
        _option1 = option1;
        _option2 = option2;
        _option3 = option3;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.ExecuteAsync(
            @"UPDATE t_user_equip
                 SET enchant_grade = @grade, enchant_1 = @o1, enchant_2 = @o2, enchant_3 = @o3
               WHERE equip_id = @equipId",
            new { equipId = _equipId, grade = _grade, o1 = _option1, o2 = _option2, o3 = _option3 });
    }

    public void Apply()
    {
    }
}
```

`connection.ExecuteAsync`의 반환형이 `Task<int>`라 `Task`로 그냥 돌려도 된다. 기존 `SaveCharacterEquipRepository`가 `Task`를 그대로 반환하는 것과 같은 형태다.

- [ ] **Step 5: `User.Enchant.cs`를 구현한다**

```csharp
using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 인챈트. 검증 → 아이템 소모 → 성공 판정 → 상태 갱신 → 저장 → 응답·싱크.
    ///
    /// <para>
    /// <b>정산도 속도 재확정도 하지 않는다.</b> 착용 중인 장비를 거절하므로(EnchantEquipped)
    /// 가동 중인 슬롯의 속도·경험치가 바뀔 수 없다 — 소급의 여지가 없다.
    /// 바뀐 값은 다음에 장착할 때 TryEquip이 반영한다.
    /// </para>
    /// </summary>
    public void TryEnchant(long equipId, int itemTid, Random? random = null)
    {
        if (!TryGetEquip(equipId, out var equip))
        {
            Reject(EResultCode.EquipNotOwned, $"미보유 장비 {equipId}");
            return;
        }

        // 착용 중 거부가 이 경로를 단순하게 만든다 — 정산 순서를 신경 쓸 필요가 없다.
        if (equip.IsEquipped)
        {
            RejectEnchant(EResultCode.EnchantEquipped, equipId, "착용 중");
            return;
        }

        if (!_enchantCatalog.TryGetItem(itemTid, out var item))
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"인챈트 아이템이 아님 {itemTid}");
            return;
        }

        if (GetItemCount(itemTid) <= 0)
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"보유 0 {itemTid}");
            return;
        }

        var hasEnchant = equip.EnchantGrade != GlobalRarity.None;

        if (item.Action == EnchantAction.Grant && hasEnchant)
        {
            RejectEnchant(EResultCode.EnchantAlreadyRolled, equipId, "이미 인챈트 있음");
            return;
        }

        if (item.Action != EnchantAction.Grant && !hasEnchant)
        {
            RejectEnchant(EResultCode.EnchantNotRolled, equipId, "인챈트 없음");
            return;
        }

        if (item.Action == EnchantAction.ExpandLine && equip.EnchantLineCount >= EnchantCatalog.MaxLineCount)
        {
            RejectEnchant(EResultCode.EnchantLineMax, equipId, "줄 상한");
            return;
        }

        if (!TryConsumeItems(new Dictionary<int, int> { [itemTid] = 1 }, out var consumed))
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"소모 실패 {itemTid}");
            return;
        }

        var rng         = random ?? Random.Shared;
        var beforeGrade = equip.EnchantGrade;
        var success     = false;

        switch (item.Action)
        {
            case EnchantAction.Grant:
                success = Rolled(item.SuccessPermille, rng);
                if (success)
                {
                    // 부여는 항상 Rare에서 시작한다. 등급은 GradeUp으로만 오른다.
                    equip.SetEnchant(GlobalRarity.Rare,
                        _enchantCatalog.RollOptions(GlobalRarity.Rare, EnchantCatalog.BaseLineCount, rng));
                }
                break;

            case EnchantAction.GradeUp:
                // 상승 확률은 아이템이 아니라 현재 등급이 정한다(큐브가 1종이므로).
                success = Rolled(_enchantCatalog.UpPermilleOf(beforeGrade), rng);
                var grade = success ? EnchantCatalog.NextGrade(beforeGrade) : beforeGrade;

                // 상승에 실패해도 줄은 다시 뽑는다 — 빈손이 없게 한 설계다. 줄 수는 유지한다.
                equip.SetEnchant(grade, _enchantCatalog.RollOptions(grade, equip.EnchantLineCount, rng));
                break;

            case EnchantAction.ExpandLine:
                success = Rolled(item.SuccessPermille, rng);
                if (success)
                {
                    var options = equip.EnchantOptions.ToList();
                    options.AddRange(_enchantCatalog.RollOptions(equip.EnchantGrade, 1, rng));
                    equip.SetEnchant(equip.EnchantGrade, options);
                }
                break;
        }

        var tids = equip.EnchantOptionTids;
        PostDBTask(new SaveEquipEnchantRepository(
            this, equipId, (int)equip.EnchantGrade,
            TidAt(tids, 0), TidAt(tids, 1), TidAt(tids, 2)));

        Send(new S_EquipEnchantResponse
        {
            Result      = EResultCode.Ok,
            EquipId     = equipId,
            Success     = success,
            BeforeGrade = (int)beforeGrade,
            AfterGrade  = (int)equip.EnchantGrade,
            Options     = tids.ToList(),
        });

        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
        Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = consumed });
    }

    /// <summary>천분율 판정. 1000이면 항상 성공, 0이면 항상 실패다.</summary>
    private static bool Rolled(int permille, Random random)
        => permille > 0 && random.Next(1000) < permille;

    /// <summary>빈 줄은 0으로 저장한다.</summary>
    private static int TidAt(IReadOnlyList<int> tids, int index)
        => index < tids.Count ? tids[index] : 0;

    private void RejectEnchant(EResultCode code, long equipId, string reason)
    {
        ServerLog.Debug("인챈트", $"거절 {code} 장비={equipId} 사유={reason}");

        Send(new S_EquipEnchantResponse { Result = code, EquipId = equipId });
    }
}
```

> `Reject`·`PostDBTask`·`Send`·`ToEquipInfo`는 기존 `User` 멤버다. `ToEquipInfo`가 `private`이면
> 같은 `partial class`라 그대로 부를 수 있다. `S_UpdateItemResponse`의 필드 이름은
> `User.Inventory.cs:52`의 사용례를 그대로 따른다.

- [ ] **Step 6: 통과를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter UserEnchantTest
```

Expected: PASS (13개)

- [ ] **Step 7: 패킷 핸들러를 붙인다**

`Server/WSGameServer/Network/ClientPacketHandler.cs`의 장비 핸들러 옆:

```csharp
    /// <summary>인챈트. 보유·착용·동작 검증은 User가 맡는다.</summary>
    [PacketHandler]
    public static void Handle_C_EquipEnchantRequest(ISession session, C_EquipEnchantRequest req)
    {
        ServerLog.Debug("인챈트", $"요청 장비={req.EquipId} 아이템={req.ItemTid} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_EquipEnchantResponse { Result = EResultCode.NotLoggedIn, EquipId = req.EquipId });
            return;
        }

        user.TryEnchant(req.EquipId, req.ItemTid);
    }
```

- [ ] **Step 8: 전체 테스트를 돌린다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

Expected: 전부 통과. Roslyn 생성기가 `Handle_C_EquipEnchantRequest`를 등록하므로 MIKA001 경고가 없어야 한다.

- [ ] **Step 9: 커밋**

```bash
git add Server/WSGameServer Server/WSGameServer.Tests
git commit -m "feat: 인챈트 부여·재롤·줄 확장 구현

- 착용 중인 장비는 거부한다. 그래서 정산·속도 재확정을 부르지 않는다
- 큐브는 상승에 실패해도 줄을 다시 뽑아 빈손이 없다
- 등급 상승 확률은 현재 등급이, 나머지는 아이템이 정한다"
```

---

### Task 7: 속도·경험치에 반영

**Files:**
- Modify: `Server/WSGameServer/User/User.WorkStation.cs` (`ResolveSlotSpeed` · 경험치 지급)
- Test: `Server/WSGameServer.Tests/User/UserEnchantTest.cs`

**Interfaces:**
- Consumes: Task 4의 `Equip.SpeedAddPermilleFor(IndustryType)` · `Equip.ExpAddPermille`
- Produces: 없음 (기존 경로 수정)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`UserEnchantTest.cs`에 추가. `UserEquipTest`의 `GiveFishingSlot`·`ExpectedSpeed` 헬퍼를 같은 형태로 옮겨 온다.

```csharp
    [Fact]
    public void 인챈트한_장비를_장착하면_가산이_속도에_실린다()
    {
        var (user, b) = UserWithRod();
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryGetEquip(Rod, out var equip).ShouldBeTrue();
        var expectedAdd = equip.SpeedAddPermilleFor(IndustryType.Fishing);

        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, TestUserBuilder.Base) });
        user.TryEquip(CharA, Rod, EquipSlot.Weapon, TestUserBuilder.Base);

        // expectedAdd는 장비에서 읽은 값이라 롤 결과와 무관하게 맞는다 —
        // "300보다 크다" 같은 단언은 2줄 모두 경험치로 뽑히면 거짓이 되므로 두지 않는다.
        user.WorkStation.Slots[0].CurrentWorkSpeed.ShouldBe(
            ExpectedFishingSpeed(user, CharA, expectedAdd));
    }

    private static int ExpectedFishingSpeed(User user, long characterId, int addPermille)
    {
        user.TryGetCharacter(characterId, out var c).ShouldBeTrue();
        return WorkSpeed.From(c.GetBaseWorkSpeed(IndustryType.Fishing))
            .Add(addPermille)
            .Multiply(Global.GatherSpeedMultiplier)
            .Value;
    }
```

> `WorkStation.Slots[0].CurrentWorkSpeed`·`WorkSpeed.From(...).Add(...).Multiply(...).Value`의 정확한 형태는
> `UserEquipTest.ExpectedSpeed`(`Server/WSGameServer.Tests/User/UserEquipTest.cs:61-`)를 그대로 따른다.

- [ ] **Step 2: 실패를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter 인챈트한_장비를_장착하면
```

Expected: FAIL — `ResolveSlotSpeed`가 아직 `SpeedAddPermille`(기본값)만 더해 기대값보다 작다.

- [ ] **Step 3: `GetEquipSpeedAdd`를 바꾼다**

`Server/WSGameServer/User/User.Equip.cs:178-195`. `ResolveSlotSpeed`(`User.WorkStation.cs:263`)는 이미 이 메서드를 부르므로 **호출부는 건드리지 않는다.**

`AppliesTo` 필터를 **밖에서 걸지 않는다** — 장비 자체는 산업이 달라도 인챈트 줄은 붙을 수 있으므로, 판단이 `SpeedAddPermilleFor` 안으로 들어간다.

```csharp
    public int GetEquipSpeedAdd(long characterId, IndustryType industry)
    {
        if (characterId == 0)
        {
            return 0;
        }

        var sum = 0;
        foreach (var ((wornBy, _), equip) in _worn)
        {
            if (wornBy == characterId)
            {
                // 산업 판단은 장비가 한다 — 장비 기본값과 인챈트 줄의 대상 산업이 다를 수 있다.
                sum += equip.SpeedAddPermilleFor(industry);
            }
        }

        return sum;
    }
```

- [ ] **Step 4: 통과를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter UserEnchantTest
```

Expected: PASS

- [ ] **Step 5: 경험치 가산의 실패하는 테스트를 쓴다**

경험치 옵션만 있는 풀로 고정하면 롤 결과가 확정된다(2줄 모두 `Exp`).

```csharp
    [Fact]
    public void 인챈트_경험치_줄이_착용_장비에서_합산된다()
    {
        var b = new TestUserBuilder();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = RodTid, Name = "대", EquipKind = EquipKind.Weapon, Industry = IndustryType.Fishing, SpeedAddPermille = 0 },
        });
        b.Enchants.Load(new[] { Exp }, GradeRows, ItemRows);   // 후보가 1종 → 2줄 모두 Exp

        var user = b.Build();
        user.LoadCharacters(new[] { new CharacterRow { character_id = CharA, character_tid = 1001, level = 1, exp = 0 } });
        user.LoadEquips(new[] { new UserEquipRow { equip_id = Rod, equip_tid = RodTid, slot_position = 0 } }, Array.Empty<CharacterEquipRow>());
        user.GainItem(GrantTid, 1);
        user.TryEnchant(Rod, GrantTid, new Random(1));
        user.TryEquip(CharA, Rod, EquipSlot.Weapon, TestUserBuilder.Base);

        user.GetEquipExpAdd(CharA).ShouldBe(Exp.Value * EnchantCatalog.BaseLineCount);
        user.GetEquipExpAdd(999).ShouldBe(0);   // 배치되지 않은 캐릭터
    }
```

- [ ] **Step 6: 실패를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter 인챈트_경험치_줄이
```

Expected: FAIL — `GetEquipExpAdd`가 없어 컴파일 오류.

- [ ] **Step 7: `GetEquipExpAdd`를 만들고 정산에 붙인다**

`Server/WSGameServer/User/User.Equip.cs`에 `GetEquipSpeedAdd` 바로 아래:

```csharp
    /// <summary>착용 장비의 인챈트 경험치 가산(천분율). 경험치에는 산업 구분이 없다.</summary>
    public int GetEquipExpAdd(long characterId)
    {
        if (characterId == 0)
        {
            return 0;
        }

        var sum = 0;
        foreach (var ((wornBy, _), equip) in _worn)
        {
            if (wornBy == characterId)
            {
                sum += equip.ExpAddPermille;
            }
        }

        return sum;
    }
```

`Server/WSGameServer/User/User.WorkStation.cs:154-159`의 경험치 지급을 고친다.

```csharp
            // 판정 1회마다 배치된 캐릭터가 (산업, 레벨)의 ExpPerJudge만큼 경험치를 번다 (캐릭터 기획 5.2).
            // 착용 장비의 인챈트 경험치 줄이 천분율 가산으로 붙는다.
            if (WorkStation.TryGet(harvest.SlotIndex, out var slot) &&
                TryGetCharacter(slot.CharacterId, out var worker))
            {
                var baseExp = harvest.JudgeCount * ResolveExpPerJudge(slot.Industry, slot.IndustryLevel);
                var gained  = baseExp * (1000 + GetEquipExpAdd(slot.CharacterId)) / 1000;

                GrantCharacterExp(worker, gained, notify);
            }
```

가산이 0이면 `baseExp * 1000 / 1000 == baseExp`라 기존 테스트가 흔들리지 않는다.

- [ ] **Step 8: 통과를 확인한다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter UserEnchantTest
```

Expected: PASS

- [ ] **Step 9: 전체 테스트를 돌린다**

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

Expected: 전부 통과. 기존 `UserWorkStationTest`·`UserEquipTest`가 빨개지면 인챈트가 없을 때의 값이 달라진 것이다 — 가산이 0일 때 `gained == baseExp`인지 확인한다.

- [ ] **Step 10: 커밋**

```bash
git add Server/WSGameServer/User Server/WSGameServer.Tests/User
git commit -m "feat: 인챈트 가산을 채취 속도와 경험치에 반영

- ResolveSlotSpeed는 장비마다 SpeedAddPermilleFor로 산업을 판단한다
  인챈트 줄의 산업이 장비 자체와 다를 수 있기 때문이다
- 경험치는 착용 장비의 ExpAddPermille 합을 천분율로 곱한다"
```

---

### Task 8: 실서버 확인과 문서 반영

**Files:**
- Modify: `GameDesign/design/character/README.md` · `GameDesign/design/workslot/README.md` · `GameDesign/design/item/README.md` · `GameDesign/design/게임기획코어.md`
- Modify: `Server/docs/채취-정산.md` · `Server/docs/데이터-카탈로그.md`
- Modify: `tasks/T-085-인챈트.md` · `tasks/README.md`

**Interfaces:**
- Consumes: Task 1~7 전부
- Produces: 없음

- [ ] **Step 1: 실서버에서 확인한다**

서버를 띄우고 Unity CLI eval로 확인한다 — 절차는 `unity-cli-live-test` 관련 기존 방식을 따른다.

1. 치트 `GiveItem`으로 인챈트 아이템 지급 (Arg1=ItemTID · Arg2=개수)
2. `C_EquipEnchantRequest`로 부여 → `S_EquipEnchantResponse`의 `AfterGrade`·`Options` 확인
3. 큐브 반복 → 등급이 오르는지, 실패해도 `Options`가 바뀌는지
4. 장착 → `S_WorkStationSlotSyncResponse`의 `CurrentWorkSpeed`가 인챈트만큼 오르는지
5. 착용 상태로 인챈트 요청 → `EnchantEquipped`로 거절되는지

- [ ] **Step 2: 기획 문서를 고친다**

| 문서 | 무엇을 |
| --- | --- |
| `character/README.md` | 장비 절(1장 #6 · 1.3)에 인챈트를 더한다. 헤더의 `바뀌면 갱신` 블록을 따라 전파 |
| `workslot/README.md` 3.4 | 속도식 가산 항 표에 **인챈트** 줄 추가 |
| `item/README.md` | 특수 아이템에 인챈트 아이템 5종 |
| `게임기획코어.md` | 3장 시스템 지도에 인챈트(또는 아이템/캐릭터 항목에 편입) · 5장 확정 표 |

**기획 문서는 "지금 상태"만 담는다** — 취소선·"폐지" 같은 이력을 쌓지 않는다.
`최종 업데이트` 줄이 있는 문서는 그날 날짜로 갱신한다. **README에는 그 줄을 두지 않는다.**

- [ ] **Step 3: 문서 그래프를 검사한다**

```powershell
powershell -File GameDesign/check-doc-graph.ps1 -Changed
```

Expected: 깨진 링크·갱신일 역전 없음

- [ ] **Step 4: 서버 문서를 고친다**

`Server/docs/데이터-카탈로그.md`에 `EnchantCatalog`를 등록하고, `Server/docs/채취-정산.md`에 속도 가산·경험치 가산의 출처를 더한다.

- [ ] **Step 5: 일감을 완료 처리한다**

`tasks/T-085-인챈트.md`의 체크박스를 닫고 상태를 갱신한 뒤, **main에서** `tasks/README.md` INDEX를 맞춘다.
(워크트리 안이라면 INDEX를 건드리지 않고 일감 파일만 고친다 — 머지 충돌 원천이다.)

- [ ] **Step 6: 커밋**

```bash
git add GameDesign/design Server/docs tasks
git commit -m "docs: 인챈트 기획·서버 문서 반영

- 속도식 가산 항과 장비 절에 인챈트를 더했다
- 데이터 카탈로그에 EnchantCatalog 등록"
```

---

## 남은 미정 (구현과 무관)

| 항목 | 언제 정하나 |
| --- | --- |
| 옵션 풀의 `Value`·`Weight` 실값 | Task 1의 테스트값을 플레이 후 조정 |
| 부여·확장 아이템의 종수와 확률 | 같음 |
| 상자별 드롭 수량 | T-029 상자 개봉과 함께 |
| 줄 고정(잠금) | 기획에 들어오면 DB를 `t_user_equip_enchant`로 분리한다 — 스펙 4장의 "분리로 갈아탈 신호" |
