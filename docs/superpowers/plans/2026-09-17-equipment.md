# 캐릭터 장비 시스템(T-002) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 유저가 소유한 장비 개체를 캐릭터의 4칸(무기·장신구2·보석)에 장착·해제하고, 장착 장비의 속도 가산이 작업슬롯 채취 속도에 실제로 반영되게 한다.

**Architecture:** 엑셀 `EquipTable`(정의) → `EquipCatalog`(서버 인덱스) → `t_user_equip`(개체) + `t_character_equip`(캐릭터·칸 → 개체 매핑) → `User.Equip.cs`(장착·해제·적재·지급) → `ResolveSlotSpeed`의 가산 항. 패킷은 스냅샷·싱크·요청·응답 5개. 획득은 치트 `GiveEquip`뿐이다.

**Tech Stack:** .NET 10 · MemoryPack · Dapper + SQLite(STRICT) · xUnit + Shouldly · openpyxl(엑셀 편집) · `GameDesign/generate-tables.ps1`

**Spec:** `docs/superpowers/specs/2026-09-17-equipment-design.md`

## Global Constraints

- 브랜치는 **main**, 워크트리 없음. 커밋 메시지는 `commit-convention`(`<type>: <한글 제목>`), **트레일러(Co-Authored-By) 붙이지 않음**, 푸시는 사용자가.
- 서버 코드 스타일: 주석 한글 1~2줄, `if`/`foreach` 중괄호 필수, early-return, `=>`는 한 줄 getter·위임만.
- 생성물(`Server/GameData`, `Server/Shared/Data`, `GameDesign/DataLog`, `Assets/Scripts_Server/Protocol`·`GameData`, `Assets/StreamingAssets/Data`)은 직접 수정하지 않는다. 엑셀·`MikaProtocol` 원본만 고치고 파이프라인/빌드가 미러링한다.
- enum·TID는 뒤에만 추가. `EquipSlot`: Weapon=1 · Accessory1=2 · Accessory2=3 · Gem=4. `EquipKind`: Weapon=1 · Accessory=2 · Gem=3.
- `PacketId`: S_EquipListResponse=27 · S_EquipSyncResponse=28 · C_EquipRequest=29 · C_UnequipRequest=30 · S_EquipResponse=31.
- `EResultCode` 600대: EquipNotOwned=600 · InvalidEquipSlot=601 · EquipKindMismatch=602 · EquipSlotEmpty=603. `ECheatCommand.GiveEquip = 8`.
- 테스트 실행: `dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj` (실행 중인 `WSGameServer.exe`가 있으면 DLL 잠금으로 실패 — 먼저 끈다). 셸에서 python 출력이 깨지면 `PYTHONIOENCODING=utf-8`.
- 파일은 CRLF일 수 있다. 통째로 새로 쓰는 파일은 LF로 써도 된다(`core.autocrlf=true`).

---

## 파일 구조

| 파일 | 책임 |
| --- | --- |
| `GameDesign/Excel/Enum.xlsx` (수정) | `EquipKind`·`EquipSlot` 시트 |
| `GameDesign/Excel/Equip.xlsx` (신규) | `EquipTable` 시트 — 장비 정의 |
| `Server/Shared/game.sqlite3` (수정) | `t_user_equip`·`t_character_equip` DDL |
| `Server/WSGameServer/Common/EquipCatalog.cs` (신규) | TID → 행, 종류↔칸 허용 규칙 |
| `Server/WSGameServer/User/Equip/Equip.cs` (신규) | 장비 개체 도메인 |
| `Server/WSGameServer/User/User.Equip.cs` (신규) | 적재·장착·해제·지급·전송 |
| `Server/WSGameServer/Repository/EquipRepository.cs` (신규) | `GrantEquipRepository`·`SaveCharacterEquipRepository` |
| `Server/WSGameServer/Repository/RepositoryContracts.cs` (수정) | `UserEquipRow`·`CharacterEquipRow`·`PlayerLoginData` 확장 |
| `Server/WSGameServer/Repository/LoginRepository.cs` (수정) | 장비·매핑 조회 |
| `Server/WSGameServer/User/User.cs`·`User.DB.cs`·`User.WorkStation.cs`·`User.Cheat.cs` (수정) | 생성자 카탈로그·적재 순서·전송 순서·속도 가산·치트 |
| `Server/MikaProtocol/PacketEnum.cs`·`PacketInfo.cs`·`MikaPacket.cs` (수정) | enum·`EquipInfo`·패킷 5개 |
| `Server/WSGameServer/Network/ClientPacketHandler.cs` (수정) | 요청 핸들러 2개 |
| `Assets/Scripts_Server/Network/MikaNetwork.Unity/ServerPacketHandler.cs` (수정) | Unity 수신 핸들러 3개 |
| `Server/MikaDummyClient/Menu/PacketMenu.cs` (수정) | 장착·해제 메뉴 |
| 테스트 | `Common/EquipCatalogTest.cs` · `User/UserEquipTest.cs` · `Repository/EquipRepositoryTest.cs` · `Protocol/PacketEnumTest.cs`(추가) · `User/TestUserBuilder.cs`(수정) · `Repository/SqliteFixture.cs`(수정) |

---

### Task 1: 엑셀 — `EquipKind`·`EquipSlot` enum과 `EquipTable`

**Files:**
- Modify: `GameDesign/Excel/Enum.xlsx`
- Create: `GameDesign/Excel/Equip.xlsx`
- 생성물(자동): `Server/GameData/Enum.cs` · `Server/GameData/Tables/EquipTable.cs` · `Server/Shared/Data/EquipTable.bytes` · `GameDesign/DataLog/EquipTable.json` · Unity 미러

**Interfaces:**
- Produces: `GameData.EquipKind { None, Weapon=1, Accessory=2, Gem=3, Max }` · `GameData.EquipSlot { None, Weapon=1, Accessory1=2, Accessory2=3, Gem=4, Max }` · `GameData.EquipTableRow { int EquipTID; string Name; GlobalRarity GlobalRarity; EquipKind EquipKind; IndustryType Industry; int SpeedAddPermille; int BasePrice; string Description }` · `GameTable.EquipTable.TryGet(int, out EquipTableRow)` · `GameTable.EquipTable.All`

- [ ] **Step 1: Excel이 열려 있지 않은지 확인하고 시트를 추가하는 스크립트를 스크래치패드에 쓴다**

```python
# scratchpad/add_equip_excel.py
import openpyxl
from openpyxl import Workbook

ROOT = 'C:/Users/wlsdn/workspace/Windows_simulator/GameDesign/Excel/'

# 1) Enum.xlsx — 시트명이 곧 enum 이름. 1~3행은 헤더, 4행부터 데이터. None=0·Max는 생성기가 붙인다.
wb = openpyxl.load_workbook(ROOT + 'Enum.xlsx')
def add_enum(name, rows):
    ws = wb.create_sheet(name)
    ws.append(['EID', 'Name', 'Desc'])
    ws.append(['EnumID', 'Value', 'Description'])
    ws.append(['int', 'enum', 'string'])
    for r in rows:
        ws.append(list(r))
add_enum('EquipKind', [(1, 'Weapon', '무기'), (2, 'Accessory', '장신구'), (3, 'Gem', '보석')])
add_enum('EquipSlot', [(1, 'Weapon', '무기 칸'), (2, 'Accessory1', '장신구 칸 1'), (3, 'Accessory2', '장신구 칸 2'), (4, 'Gem', '보석 칸')])
wb.save(ROOT + 'Enum.xlsx')

# 2) Equip.xlsx — A열 마커, 1행 비움, 마커 행 뒤 A열 빈 행이 컬럼명 행.
wb2 = Workbook()
ws = wb2.active
ws.title = 'EquipTable'
ws.append([None] * 9)
ws.append(['//', '장비 TID (종류×1000+순번 — 무기 1xxx · 장신구 2xxx · 보석 3xxx)', '이름', '레어도 (전역 등급)',
           '종류 (허용 칸을 정한다 — 장신구는 Accessory1·2 어디든)', '대상 산업 (None=전 산업)',
           '작업속도 가산 (천분율 · 250 = +25% · 기본값 기준)', '기본 가격 (즉시 판매 기준 · 아직 미사용)', '설명 (메모 — 로직 미사용)'])
ws.append(['C&S', 'a', 'a', 'a', 'a', 'a', 'a', 'a', 'a'])
ws.append(['Type', 'int', 'string', 'eGlobalRarity', 'eEquipKind', 'eIndustryType', 'int', 'int', 'string'])
ws.append(['Min', 1, None, None, None, None, -999, 0, None])
ws.append(['Max', None, None, None, None, None, None, None, None])
ws.append(['Default(Null)', None, None, None, None, None, None, None, '""'])
ws.append(['Ref', None, None, None, None, None, None, None, None])
ws.append([None, 'EquipTID', 'Name', 'GlobalRarity', 'EquipKind', 'Industry', 'SpeedAddPermille', 'BasePrice', 'Description'])
rows = [
    (1001, '목검',         'Common',   'Weapon',    'None',    100, 50,  '전 산업 +10% · 테스트값'),
    (1002, '낚싯대',       'Uncommon', 'Weapon',    'Fishing', 300, 120, '낚시 +30% · 테스트값'),
    (2001, '구리 반지',    'Common',   'Accessory', 'None',    50,  40,  '전 산업 +5% · 테스트값'),
    (2002, '어부의 목걸이', 'Rare',     'Accessory', 'Fishing', 200, 80,  '낚시 +20% · 테스트값'),
    (3001, '원석',         'Common',   'Gem',       'None',    30,  20,  '전 산업 +3% · 테스트값'),
]
for r in rows:
    ws.append([None, *r])
wb2.save(ROOT + 'Equip.xlsx')
print('ok')
```

Run: `PYTHONIOENCODING=utf-8 python "<scratchpad>/add_equip_excel.py"`
Expected: `ok`

- [ ] **Step 2: 파이프라인 실행**

Run: `powershell -File GameDesign/generate-tables.ps1`
Expected: `[완료] 코드(GameData) + 바이너리(Shared/Data) + 로그(DataLog) 생성 완료`, Unity 미러 복사 로그. 실패하면 오류 메시지(`Industry` 컬럼에 `None`이 거부되면 셀을 비우고 `Default(Null)`에 `None`을 넣는 대안을 시도)를 보고 스크립트를 고쳐 다시 돌린다.

- [ ] **Step 3: 생성물 확인**

Run: `grep -n "EquipKind\|EquipSlot" Server/GameData/Enum.cs; sed -n 1,30p Server/GameData/Tables/EquipTable.cs; head -c 600 GameDesign/DataLog/EquipTable.json`
Expected: 두 enum이 값 그대로, `EquipTableRow`에 8개 프로퍼티(`IndustryType Industry`), JSON에 5행이고 `Industry`가 `"None"`/`"Fishing"`.

- [ ] **Step 4: 서버 빌드로 GameData가 컴파일되는지 확인**

Run: `dotnet build Server/WSGameServer/WSGameServer.csproj 2>&1 | tail -3`
Expected: 오류 0.

- [ ] **Step 5: Commit** (엑셀 + 생성물 + DataLog + Unity 미러를 한 커밋에)

```bash
git add GameDesign/Excel/Enum.xlsx GameDesign/Excel/Equip.xlsx Server/GameData Server/Shared/Data GameDesign/DataLog Assets/Scripts_Server/GameData Assets/StreamingAssets/Data
git commit -m "feat: 장비 정의 테이블과 EquipKind·EquipSlot enum 추가

- Equip.xlsx/EquipTable — 종류·대상 산업·속도 가산(천분율) · 테스트 행 5개
- Enum.xlsx — EquipKind(무기·장신구·보석) · EquipSlot(무기·장신구1·2·보석)"
```

---

### Task 2: DB — `t_user_equip`·`t_character_equip`

**Files:**
- Modify: `Server/Shared/game.sqlite3`
- Modify: `Server/WSGameServer.Tests/Repository/SqliteFixture.cs`

**Interfaces:**
- Produces: `SqliteFixture.CreateEquipTables()` — 테스트 `:memory:` DB에 아래 두 테이블 + `t_character` 생성.

- [ ] **Step 1: 운영 DB에 DDL 적용**

```python
# scratchpad/add_equip_tables.py
import sqlite3
c = sqlite3.connect('C:/Users/wlsdn/workspace/Windows_simulator/Server/Shared/game.sqlite3')
c.executescript("""
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
    PRIMARY KEY (character_id, slot)        -- (캐릭터, 칸) 유일 = 한 칸에 하나. 로그인 조회는 t_user_equip과 JOIN
) STRICT;
""")
c.commit()
for n, s in c.execute("select name, sql from sqlite_master where name like '%equip%'"):
    print(s)
```

Run: `PYTHONIOENCODING=utf-8 python "<scratchpad>/add_equip_tables.py"`
Expected: 두 `CREATE TABLE`이 출력된다. 인덱스는 두지 않는다(로그인 조회 1회, 유저당 수십 행).

- [ ] **Step 2: 테스트 픽스처에 같은 DDL 추가**

`SqliteFixture.cs`의 `CreateUserTable()` 아래에 추가:

```csharp
    /// <summary>장비 개체·착용 매핑 + 매핑이 가리키는 캐릭터 테이블. 운영 DDL과 같아야 한다.</summary>
    public void CreateEquipTables()
    {
        Execute(@"
            CREATE TABLE t_character (
                character_id  INTEGER PRIMARY KEY,
                user_id       INTEGER NOT NULL,
                character_tid INTEGER NOT NULL,
                level         INTEGER NOT NULL DEFAULT 1,
                exp           INTEGER NOT NULL DEFAULT 0,
                created_at    TEXT    NOT NULL DEFAULT (datetime('now'))
            ) STRICT;
            CREATE TABLE t_user_equip (
                equip_id      INTEGER PRIMARY KEY,
                user_id       INTEGER NOT NULL,
                equip_tid     INTEGER NOT NULL,
                slot_position INTEGER NOT NULL DEFAULT 0,
                created_at    TEXT    NOT NULL DEFAULT (datetime('now'))
            ) STRICT;
            CREATE TABLE t_character_equip (
                character_id INTEGER NOT NULL,
                slot         INTEGER NOT NULL,
                equip_id     INTEGER NOT NULL UNIQUE,
                PRIMARY KEY (character_id, slot)
            ) STRICT;");
    }
```

- [ ] **Step 3: 빌드 확인 후 Commit**

Run: `dotnet build Server/WSGameServer.Tests/WSGameServer.Tests.csproj 2>&1 | tail -3` → 오류 0.

```bash
git add Server/Shared/game.sqlite3 Server/WSGameServer.Tests/Repository/SqliteFixture.cs
git commit -m "feat: 장비 개체·착용 매핑 테이블 DDL

- t_user_equip — 유저 소유 개체 (equip_tid · 창고 칸 번호)
- t_character_equip — (캐릭터, 칸) PK · equip_id UNIQUE로 한 칸 하나·한 장비 한 곳"
```

---

### Task 3: `EquipCatalog` — TID 인덱스와 종류↔칸 규칙

**Files:**
- Create: `Server/WSGameServer/Common/EquipCatalog.cs`
- Test: `Server/WSGameServer.Tests/Common/EquipCatalogTest.cs`
- Modify: `Server/WSGameServer/GameServer.cs:43` (LoadAll 배선)

**Interfaces:**
- Produces: `EquipCatalog : Singleton<EquipCatalog>` — `int Count` · `void LoadAll()` · `void Load(IEnumerable<EquipTableRow>)` · `bool TryGet(int equipTid, out EquipTableRow row)` · `static bool IsValidSlot(EquipSlot slot)` · `static bool CanEquip(EquipKind kind, EquipSlot slot)`

- [ ] **Step 1: 실패하는 테스트**

```csharp
// Server/WSGameServer.Tests/Common/EquipCatalogTest.cs
using GameData;

namespace WSGameServer;

/// <summary>종류↔칸 허용표와 TID 인덱스. 장신구가 두 칸 어디든 들어가는지가 핵심이다.</summary>
public class EquipCatalogTest
{
    private static EquipTableRow Row(int tid, EquipKind kind) => new() { EquipTID = tid, Name = $"장비{tid}", EquipKind = kind };

    [Theory]
    [InlineData(EquipKind.Weapon,    EquipSlot.Weapon,     true)]
    [InlineData(EquipKind.Weapon,    EquipSlot.Accessory1, false)]
    [InlineData(EquipKind.Accessory, EquipSlot.Accessory1, true)]
    [InlineData(EquipKind.Accessory, EquipSlot.Accessory2, true)]
    [InlineData(EquipKind.Accessory, EquipSlot.Gem,        false)]
    [InlineData(EquipKind.Gem,       EquipSlot.Gem,        true)]
    [InlineData(EquipKind.Gem,       EquipSlot.Weapon,     false)]
    public void 종류가_칸에_맞는지(EquipKind kind, EquipSlot slot, bool expected)
    {
        EquipCatalog.CanEquip(kind, slot).ShouldBe(expected);
    }

    [Fact]
    public void None과_Max는_유효한_칸이_아니다()
    {
        EquipCatalog.IsValidSlot(EquipSlot.None).ShouldBeFalse();
        EquipCatalog.IsValidSlot(EquipSlot.Max).ShouldBeFalse();
        EquipCatalog.IsValidSlot(EquipSlot.Accessory2).ShouldBeTrue();
    }

    [Fact]
    public void TID로_행을_찾는다()
    {
        var catalog = new EquipCatalog();
        catalog.Load(new[] { Row(1001, EquipKind.Weapon), Row(2001, EquipKind.Accessory) });

        catalog.Count.ShouldBe(2);
        catalog.TryGet(2001, out var row).ShouldBeTrue();
        row.EquipKind.ShouldBe(EquipKind.Accessory);
        catalog.TryGet(9999, out _).ShouldBeFalse();
    }

    [Fact]
    public void TID가_중복되면_예외다()
    {
        var catalog = new EquipCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Row(1001, EquipKind.Weapon), Row(1001, EquipKind.Gem) }));
    }

    [Fact]
    public void 실제_엑셀_데이터가_적재된다()
    {
        GameTableFixture.EnsureLoaded();
        var catalog = new EquipCatalog();

        catalog.LoadAll();

        catalog.Count.ShouldBeGreaterThan(0);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj --filter "FullyQualifiedName~EquipCatalogTest" 2>&1 | grep -E "error|통과|실패" | head`
Expected: 컴파일 오류 (`EquipCatalog` 없음).

- [ ] **Step 3: 구현**

```csharp
// Server/WSGameServer/Common/EquipCatalog.cs
using GameData;
using MikaUtils;

namespace WSGameServer;

// EquipTable 보관소. TID → 행과 "이 종류가 이 칸에 들어가는가"를 답한다 — 장신구만 두 칸(Accessory1·2) 어디든.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md
public sealed class EquipCatalog : Singleton<EquipCatalog>
{
    private readonly Dictionary<int, EquipTableRow> _byTid = new();

    /// <summary>등록된 장비 종류 수.</summary>
    public int Count => _byTid.Count;

    /// <summary>모든 행을 GameTable에서 읽어 등록한다. GameTable.LoadAll 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.EquipTable.All);
    }

    /// <summary>행 목록으로 인덱스를 만든다. 같은 TID가 두 번 나오면 예외.</summary>
    public void Load(IEnumerable<EquipTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byTid.Clear();

        foreach (var row in rows)
        {
            if (!_byTid.TryAdd(row.EquipTID, row))
            {
                throw new InvalidOperationException($"EquipTable에 EquipTID가 중복됐습니다: {row.EquipTID}");
            }
        }
    }

    public bool TryGet(int equipTid, out EquipTableRow row)
        => _byTid.TryGetValue(equipTid, out row!);

    /// <summary>None·Max를 뺀 실제 칸인가. 클라가 보낸 값이라 범위부터 거른다.</summary>
    public static bool IsValidSlot(EquipSlot slot)
        => slot > EquipSlot.None && slot < EquipSlot.Max;

    /// <summary>종류가 칸에 맞는가. 장신구는 두 칸 어디든 — 칸을 나눈 이유는 매핑 유일키 때문이지 종류가 달라서가 아니다.</summary>
    public static bool CanEquip(EquipKind kind, EquipSlot slot)
    {
        return kind switch
        {
            EquipKind.Weapon    => slot == EquipSlot.Weapon,
            EquipKind.Accessory => slot == EquipSlot.Accessory1 || slot == EquipSlot.Accessory2,
            EquipKind.Gem       => slot == EquipSlot.Gem,
            _                   => false,
        };
    }
}
```

`GameServer.cs`의 `UnlockCatalog.Instance.LoadAll();` 줄 다음에:

```csharp
            EquipCatalog.Instance.LoadAll();
```

- [ ] **Step 4: 통과 확인**

Run: 위 필터로 다시 실행 → `통과! - 실패: 0, 통과: 11`.

- [ ] **Step 5: Commit**

```bash
git add Server/WSGameServer/Common/EquipCatalog.cs Server/WSGameServer.Tests/Common/EquipCatalogTest.cs Server/WSGameServer/GameServer.cs
git commit -m "feat: EquipCatalog — 장비 TID 인덱스와 종류↔칸 규칙"
```

---

### Task 4: 프로토콜 — enum · `EquipInfo` · 패킷 5개 · Unity 수신 핸들러

**Files:**
- Modify: `Server/MikaProtocol/PacketEnum.cs` (`EResultCode` 600대 · `ECheatCommand.GiveEquip` · `EEquipSlot`)
- Modify: `Server/MikaProtocol/PacketInfo.cs` (`EquipInfo`)
- Modify: `Server/MikaProtocol/MikaPacket.cs` (`PacketId` 27~31 · 클래스 5개)
- Modify: `Server/WSGameServer.Tests/Protocol/PacketEnumTest.cs`
- Modify: `Assets/Scripts_Server/Network/MikaNetwork.Unity/ServerPacketHandler.cs` (미러가 복사된 뒤)

**Interfaces:**
- Produces: `EEquipSlot { None=0, Weapon=1, Accessory1=2, Accessory2=3, Gem=4 }` · `EquipInfo { long EquipId; int EquipTid; long EquippedCharacterId; EEquipSlot EquippedSlot; int SlotPosition }` · `S_EquipListResponse { List<EquipInfo> Equips }` · `S_EquipSyncResponse { List<EquipInfo> Equips }` · `C_EquipRequest { long CharacterId; long EquipId; EEquipSlot Slot }` · `C_UnequipRequest { long CharacterId; EEquipSlot Slot }` · `S_EquipResponse { EResultCode Result; long CharacterId; EEquipSlot Slot }`

- [ ] **Step 1: 실패하는 테스트 — `PacketEnumTest`에 추가**

```csharp
    [Fact]
    public void 프로토콜_장비칸은_GameData_장비칸과_이름_값이_1대1이다()
    {
        // 장착 요청의 Slot을 서버가 byte 캐스팅으로 옮기고 DB(t_character_equip.slot)에 그 정수를 저장한다.
        var protocol = Enum.GetValues<EEquipSlot>()
            .Select(v => $"{v}={(byte)v}");

        var gameData = Enum.GetValues<EquipSlot>()
            .Where(v => v != EquipSlot.Max)
            .Select(v => $"{v}={(byte)v}");

        protocol.ShouldBe(gameData);
    }
```

Run: `dotnet test ... --filter "FullyQualifiedName~PacketEnumTest"` → 컴파일 오류(`EEquipSlot` 없음).

- [ ] **Step 2: `PacketEnum.cs`**

`EResultCode`의 해금 블록 뒤에:

```csharp
        // ── 600~: 장비 ──
        EquipNotOwned      = 600, // 미보유 장비 개체
        InvalidEquipSlot   = 601, // None·범위 밖 칸
        EquipKindMismatch  = 602, // 종류가 칸에 맞지 않음 (무기를 보석 칸에 등)
        EquipSlotEmpty     = 603, // 해제할 장비가 없는 칸
```

`ECheatCommand`의 `Unlock = 7` 뒤에:

```csharp
        GiveEquip        = 8,  // Arg1 = EquipTID — 개체 1개 지급, 창고 첫 빈 칸
```

`ECurrencyType` 정의 아래에:

```csharp
    // 장비 칸. GameData.EquipSlot(Enum.xlsx)과 이름·값이 1:1이어야 한다 — 서버가 byte 캐스팅으로 옮기고 DB에 저장한다.
    public enum EEquipSlot : byte
    {
        None       = 0,

        Weapon     = 1,
        Accessory1 = 2,
        Accessory2 = 3,
        Gem        = 4,
    }
```

- [ ] **Step 3: `PacketInfo.cs` — `WorkStationSlotInfo` 뒤에**

```csharp
    // 장비 개체 하나. 유저 소유이며 EquippedCharacterId가 0이면 창고에 있다. SlotPosition은 창고 장비 탭의 칸 번호(0부터).
    // 스탯(종류·산업·가산)은 EquipTid로 EquipTable에서 읽는다 — 개체는 위치만 나른다.
    [MemoryPackable]
    public partial class EquipInfo
    {
        public long       EquipId             { get; set; }
        public int        EquipTid            { get; set; }
        public long       EquippedCharacterId { get; set; }  // 0=창고
        public EEquipSlot EquippedSlot        { get; set; }  // 창고면 None
        public int        SlotPosition        { get; set; }
    }
```

- [ ] **Step 4: `MikaPacket.cs`**

`PacketId`에 `S_UnlockListResponse = 26,` 뒤:

```csharp
        S_EquipListResponse = 27,
        S_EquipSyncResponse = 28,
        C_EquipRequest = 29,
        C_UnequipRequest = 30,
        S_EquipResponse = 31,
```

상점 블록 앞(해금 블록 뒤)에:

```csharp
    // ───────────────────────── 장비 (Equip) ─────────────────────────

    /// <summary>보유 장비 전체(로그인 시). 캐릭터 목록 뒤 · 작업슬롯 스냅샷 앞에 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipListResponse)]
    public partial class S_EquipListResponse : IPacket
    {
        public List<EquipInfo> Equips { get; set; } = new();
    }

    /// <summary>바뀐 장비 개체들. 지급·장착·해제·자동 이동(최대 3개)에서 온다. EquipId로 찾아 덮어쓴다 — 확정값이다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipSyncResponse)]
    public partial class S_EquipSyncResponse : IPacket
    {
        public List<EquipInfo> Equips { get; set; } = new();
    }

    /// <summary>장착 요청. 칸은 클라가 고른다(장신구는 Accessory1·2 어디든). 이미 다른 캐릭터가 착용 중이면 서버가 옮긴다.</summary>
    [MemoryPackable, Packet(PacketId.C_EquipRequest)]
    public partial class C_EquipRequest : IPacket
    {
        public long       CharacterId { get; set; }  // 개체 PK
        public long       EquipId     { get; set; }  // 개체 PK
        public EEquipSlot Slot        { get; set; }
    }

    /// <summary>해제 요청. 그 칸이 비어 있으면 EquipSlotEmpty.</summary>
    [MemoryPackable, Packet(PacketId.C_UnequipRequest)]
    public partial class C_UnequipRequest : IPacket
    {
        public long       CharacterId { get; set; }
        public EEquipSlot Slot        { get; set; }
    }

    /// <summary>장착·해제 결과. 바뀐 개체는 S_EquipSyncResponse가, 속도 변화는 S_WorkStationSlotSyncResponse가 따로 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipResponse)]
    public partial class S_EquipResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public long        CharacterId { get; set; }
        public EEquipSlot  Slot        { get; set; }
    }
```

- [ ] **Step 5: 테스트 통과 + 미러 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~PacketEnumTest"` → 통과. 빌드 로그에 `[sync-protocol] ... copied=3`.
Run: `diff <(tr -d '\r' < Server/MikaProtocol/MikaPacket.cs) <(tr -d '\r' < Assets/Scripts_Server/Protocol/MikaPacket.cs) && echo 미러동일`

- [ ] **Step 6: Unity 수신 핸들러** — `ServerPacketHandler.cs`의 `#region 해금` 블록 `#endregion` 뒤에 추가. 상단 summary 주석의 로그인 패킷 순서 목록에도 `S_EquipListResponse         보유 장비 (캐릭터 뒤 · 슬롯 앞)` 한 줄을 `S_CharacterListResponse` 다음에 넣는다.

```csharp
        #region 장비

        // 보유 장비 전체 도착 — 로그인 직후, 캐릭터 목록 뒤·작업슬롯 스냅샷 앞 (Handle_S_EquipListResponse에서 발행)
        public static event Action<S_EquipListResponse>? EquipListReceived;

        // 바뀐 장비 개체 도착 — 지급·장착·해제·자동 이동 (Handle_S_EquipSyncResponse에서 발행)
        public static event Action<S_EquipSyncResponse>? EquipSynced;

        // 장착·해제 결과 도착 (Handle_S_EquipResponse에서 발행)
        public static event Action<S_EquipResponse>? EquipResponded;

        // 보유 장비 전체 (S_EquipListResponse 수신 시 자동 호출)
        // ★ 로그인 시 자동으로 1회 온다. EquippedCharacterId=0이면 창고, SlotPosition이 창고 장비 탭의 칸이다.
        [PacketHandler]
        public static void Handle_S_EquipListResponse(ISession session, S_EquipListResponse res)
        {
            ClientLogger.Info(ClientLogger.Recv, $"보유 장비 — {res.Equips.Count}개");
            EquipListReceived?.Invoke(res);
        }

        // 바뀐 장비 개체들 (S_EquipSyncResponse 수신 시 자동 호출)
        // ※ 스냅샷과 같은 EquipInfo다. EquipId로 찾아 덮어쓴다 — 확정값이다. 없던 Id면 새로 지급된 것이다.
        [PacketHandler]
        public static void Handle_S_EquipSyncResponse(ISession session, S_EquipSyncResponse res)
        {
            ClientLogger.Info(ClientLogger.Recv,
                $"장비 동기화 — {string.Join(", ", res.Equips.Select(e => $"#{e.EquipId}(TID {e.EquipTid})→캐릭터 {e.EquippedCharacterId} {e.EquippedSlot} 칸 {e.SlotPosition}"))}");
            EquipSynced?.Invoke(res);
        }

        // 장착·해제 결과 (S_EquipResponse 수신 시 자동 호출)
        // ※ 성공이면 S_EquipSyncResponse(바뀐 개체)와 S_WorkStationSlotSyncResponse(속도)가 따로 온다.
        [PacketHandler]
        public static void Handle_S_EquipResponse(ISession session, S_EquipResponse res)
        {
            ClientLogger.Info(ClientLogger.Recv, $"장비 {res.Slot} @캐릭터 {res.CharacterId} → {res.Result}");
            EquipResponded?.Invoke(res);
        }

        #endregion
```

파일 상단에 `using System.Linq;`가 없으면 추가한다.

- [ ] **Step 7: Commit** (원본 + 미러 함께)

```bash
git add Server/MikaProtocol Assets/Scripts_Server/Protocol Assets/Scripts_Server/Network/MikaNetwork.Unity/ServerPacketHandler.cs Server/WSGameServer.Tests/Protocol/PacketEnumTest.cs
git commit -m "feat: 장비 패킷 — 스냅샷·싱크·장착·해제·응답

- EquipInfo(개체·착용 위치·창고 칸) · PacketId 27~31
- EResultCode 600대 · ECheatCommand.GiveEquip · EEquipSlot(GameData.EquipSlot과 1:1 테스트)
- Unity ServerPacketHandler 수신 핸들러 3개"
```

---

### Task 5: 저장소 — 조회 Row · 로그인 조회 · 지급 · 매핑 저장(트랜잭션)

**Files:**
- Modify: `Server/WSGameServer/Repository/RepositoryContracts.cs`
- Modify: `Server/WSGameServer/Repository/LoginRepository.cs`
- Create: `Server/WSGameServer/Repository/EquipRepository.cs`
- Test: `Server/WSGameServer.Tests/Repository/EquipRepositoryTest.cs`
- Modify: `PlayerLoginData`를 위치 인자로 만드는 테스트 전부 (`grep -rn "new PlayerLoginData\|LoginData(" Server/WSGameServer.Tests`)

**Interfaces:**
- Produces:
  - `UserEquipRow { long equip_id; int equip_tid; int slot_position }` · `CharacterEquipRow { long character_id; int slot; long equip_id }`
  - `PlayerLoginData(..., List<UserUnlockRow> UnlockRows, List<UserEquipRow> EquipRows, List<CharacterEquipRow> CharacterEquipRows)` — 뒤에 두 목록 추가
  - `EquipMappingChange(long CharacterId, EquipSlot Slot, long EquipId)` — `EquipId == 0`이면 그 칸을 비운다
  - `GrantEquipRepository(User user, int equipTid, int slotPosition)` — INSERT 후 `User.OnEquipGranted(long equipId, int equipTid, int slotPosition)` 호출
  - `SaveCharacterEquipRepository(User user, IReadOnlyList<EquipMappingChange> changes)` — 트랜잭션. `Apply()`는 비어 있다
- Consumes: `User.OnEquipGranted` (Task 6에서 정의 — 이 Task에서는 컴파일을 위해 Task 6의 `User.Equip.cs`를 먼저 만들어도 되지만, 계획 순서상 여기서는 **`User.Equip.cs`에 `OnEquipGranted`만 있는 최소 파일**을 만든다. Task 6이 그 파일을 채운다.)

- [ ] **Step 1: 실패하는 테스트**

```csharp
// Server/WSGameServer.Tests/Repository/EquipRepositoryTest.cs
using GameData;

namespace WSGameServer;

/// <summary>
/// 장비 매핑 SQL을 실제 SQLite에 대고 검증한다. 자동 이동은 매핑 행 최대 3개를 건드리므로
/// <b>한 트랜잭션</b>이어야 하고, UNIQUE(equip_id)에 걸리면 전부 되돌아가야 한다.
/// </summary>
public class EquipRepositoryTest : IDisposable
{
    private readonly SqliteFixture _db = new();

    public EquipRepositoryTest()
    {
        _db.CreateEquipTables();
    }

    public void Dispose() => _db.Dispose();

    private static User NewUser() => new TestUserBuilder().Build(uid: 7);

    private long Count(string sql) => (long)_db.Query(sql)!;

    [Fact]
    public async Task 지급은_개체_PK를_발급하고_창고_칸을_저장한다()
    {
        var user = NewUser();
        var repo = new GrantEquipRepository(user, equipTid: 1001, slotPosition: 3);

        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_user_equip WHERE user_id = 7 AND equip_tid = 1001 AND slot_position = 3").ShouldBe(1);
        repo.EquipId.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task 자동_이동은_이전_칸을_비우고_새_칸에_넣는다()
    {
        var user = NewUser();
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001), (11, 7, 1002)");
        // 장비 10은 캐릭터 500 무기 칸, 장비 11은 캐릭터 501 무기 칸
        _db.Execute("INSERT INTO t_character_equip VALUES (500, 1, 10), (501, 1, 11)");

        // 장비 10을 캐릭터 501 무기 칸으로 → 500 무기 칸 비움, 501 무기 칸의 11은 창고로, 10이 501 무기 칸에
        var repo = new SaveCharacterEquipRepository(user, new[]
        {
            new EquipMappingChange(500, EquipSlot.Weapon, 0),
            new EquipMappingChange(501, EquipSlot.Weapon, 10),
        });
        await repo.ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character_equip").ShouldBe(1);
        Count("SELECT equip_id FROM t_character_equip WHERE character_id = 501 AND slot = 1").ShouldBe(10);
    }

    [Fact]
    public async Task 해제는_행을_지운다()
    {
        var user = NewUser();
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001)");
        _db.Execute("INSERT INTO t_character_equip VALUES (500, 4, 10)");

        await new SaveCharacterEquipRepository(user, new[] { new EquipMappingChange(500, EquipSlot.Gem, 0) })
            .ExecuteAsync(new DbConnection(_db.Connection));

        Count("SELECT COUNT(*) FROM t_character_equip").ShouldBe(0);
    }

    [Fact]
    public async Task 실패하면_전부_되돌아간다()
    {
        var user = NewUser();
        _db.Execute("INSERT INTO t_user_equip (equip_id, user_id, equip_tid) VALUES (10, 7, 1001)");
        _db.Execute("INSERT INTO t_character_equip VALUES (500, 1, 10)");

        // 두 번째 변경이 UNIQUE(equip_id)를 어긴다 — 같은 장비 10을 다른 캐릭터 칸에도 넣으려 한다(앞에서 500 무기 칸을 비우지 않았다).
        var repo = new SaveCharacterEquipRepository(user, new[]
        {
            new EquipMappingChange(600, EquipSlot.Gem, 10),
        });

        await Should.ThrowAsync<Exception>(() => repo.ExecuteAsync(new DbConnection(_db.Connection)));

        Count("SELECT COUNT(*) FROM t_character_equip").ShouldBe(1);
        Count("SELECT character_id FROM t_character_equip").ShouldBe(500);
    }
}
```

`SqliteFixture`에 단일 값 조회 헬퍼 추가:

```csharp
    /// <summary>단일 값 조회. 검증용.</summary>
    public object? Query(string sql)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
```

> ⚠️ 마지막 테스트는 `SaveCharacterEquipRepository`가 "INSERT 전에 `equip_id`로 기존 행을 지운다"면 실패하지 않는다. 사양은 **자동 이동 시 호출자가 이전 (캐릭터, 칸) 변경을 함께 넘긴다**이고, 저장소는 넘겨받은 변경만 반영한다(`equip_id` 기준 DELETE를 하지 않는다). 그래야 메모리와 DB가 같은 규칙으로 움직인다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~EquipRepositoryTest"` → 컴파일 오류.

- [ ] **Step 3: `RepositoryContracts.cs`**

`UserUnlockRow` 뒤에:

```csharp
// t_user_equip 조회 전용 Row. equip_id는 개체 PK(long), equip_tid는 테이블 정의(int).
public sealed record UserEquipRow
{
    public long equip_id      { get; init; }
    public int  equip_tid     { get; init; }
    public int  slot_position { get; init; }
}

// t_character_equip 조회 전용 Row. slot은 GameData.EquipSlot 정수값.
public sealed record CharacterEquipRow
{
    public long character_id { get; init; }
    public int  slot         { get; init; }
    public long equip_id     { get; init; }
}
```

`PlayerLoginData`를:

```csharp
public sealed record PlayerLoginData(
    List<InventoryRow> InventoryRows,
    CurrencyRow? Currency,
    List<CharacterRow> CharacterRows,
    List<WorkStationSlotRow> WorkStationSlotRows,
    List<UserIndustryLevelRow> IndustryLevelRows,
    List<UserUnlockRow> UnlockRows,
    List<UserEquipRow> EquipRows,
    List<CharacterEquipRow> CharacterEquipRows);
```

- [ ] **Step 4: `LoginRepository.cs`** — 필드 두 개, 조회 두 개, Apply 인자 추가

```csharp
    private List<UserEquipRow>         _equipRows          = new();
    private List<CharacterEquipRow>    _characterEquipRows = new();
```

`ExecuteAsync`의 6) 뒤:

```csharp
        // 7) 장비 개체(유저 소유 전부)와 착용 매핑. 매핑은 유저 컬럼이 없어 개체 테이블과 JOIN으로 유저를 가른다.
        _equipRows = await connection.QueryAsync<UserEquipRow>(
            "SELECT equip_id, equip_tid, slot_position FROM t_user_equip WHERE user_id = @userId",
            new { userId = User.Uid });

        _characterEquipRows = await connection.QueryAsync<CharacterEquipRow>(
            @"SELECT ce.character_id, ce.slot, ce.equip_id
              FROM t_character_equip ce
              JOIN t_user_equip ue ON ue.equip_id = ce.equip_id
              WHERE ue.user_id = @userId",
            new { userId = User.Uid });
```

`Apply`:

```csharp
            new PlayerLoginData(_inventoryRows, _currency, _characterRows,
                                _workStationSlotRows, _industryLevelRows, _unlockRows,
                                _equipRows, _characterEquipRows),
```

- [ ] **Step 5: `EquipRepository.cs`**

```csharp
// Server/WSGameServer/Repository/EquipRepository.cs
using GameData;

namespace WSGameServer;

/// <summary>착용 매핑 변경 하나. EquipId가 0이면 그 (캐릭터, 칸)을 비운다.</summary>
public readonly record struct EquipMappingChange(long CharacterId, EquipSlot Slot, long EquipId);

/// <summary>장비 개체 1개를 지급(INSERT)하고 발급된 PK를 로직 스레드로 돌려준다. 창고 칸은 호출자가 정해 넘긴다.</summary>
public sealed class GrantEquipRepository : IRepository
{
    private readonly int _equipTid;
    private readonly int _slotPosition;

    public GrantEquipRepository(User user, int equipTid, int slotPosition)
    {
        User          = user;
        _equipTid     = equipTid;
        _slotPosition = slotPosition;
    }

    public long Key => User.DbKey;

    public User User { get; }

    /// <summary>발급된 개체 PK. ExecuteAsync 뒤에만 유효하다.</summary>
    public long EquipId { get; private set; }

    // === DB 스레드에서 실행 ===
    public async Task ExecuteAsync(DbConnection connection)
    {
        EquipId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO t_user_equip (user_id, equip_tid, slot_position)
              VALUES (@userId, @tid, @position) RETURNING equip_id;",
            new { userId = User.Uid, tid = _equipTid, position = _slotPosition });
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.OnEquipGranted(EquipId, _equipTid, _slotPosition);
    }
}

/// <summary>
/// 착용 매핑 변경을 한 트랜잭션으로 쓴다. 자동 이동은 행 최대 3개(이전 칸 비움·밀려난 장비·새 매핑)를 건드리므로
/// 나누면 중간 실패 시 장비가 두 곳에 있거나 어디에도 없게 된다. 비우기를 전부 먼저 하고 넣는다 — UNIQUE(equip_id) 순서 문제.
/// </summary>
public sealed class SaveCharacterEquipRepository : IRepository
{
    private readonly IReadOnlyList<EquipMappingChange> _changes;

    public SaveCharacterEquipRepository(User user, IReadOnlyList<EquipMappingChange> changes)
    {
        User     = user;
        _changes = changes;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public IReadOnlyList<EquipMappingChange> Changes => _changes;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var c in _changes)
            {
                await tx.ExecuteAsync(
                    "DELETE FROM t_character_equip WHERE character_id = @characterId AND slot = @slot",
                    new { characterId = c.CharacterId, slot = (int)c.Slot });
            }

            foreach (var c in _changes)
            {
                if (c.EquipId == 0)
                {
                    continue;
                }

                await tx.ExecuteAsync(
                    "INSERT INTO t_character_equip (character_id, slot, equip_id) VALUES (@characterId, @slot, @equipId)",
                    new { characterId = c.CharacterId, slot = (int)c.Slot, equipId = c.EquipId });
            }
        });
    }

    public void Apply()
    {
    }
}
```

- [ ] **Step 6: `User.Equip.cs` 최소 파일** (Task 6이 채운다)

```csharp
// Server/WSGameServer/User/User.Equip.cs
using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>지급이 끝나면 불린다(로직 스레드). Task 6에서 메모리 적재·싱크로 채운다.</summary>
    public void OnEquipGranted(long equipId, int equipTid, int slotPosition)
    {
    }
}
```

- [ ] **Step 7: `PlayerLoginData`를 만드는 기존 테스트 수정**

Run: `grep -rn "new PlayerLoginData\|LoginData(" Server/WSGameServer.Tests --include=*.cs`
각 호출에 `new List<UserEquipRow>(), new List<CharacterEquipRow>()`를 마지막 두 인자로 추가한다 (예: `UserUnlockTest.LoginData`의 `new(...)` 끝).

- [ ] **Step 8: 전체 테스트 통과**

Run: `dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj 2>&1 | grep -E "error|통과!|실패"` → 실패 0.

- [ ] **Step 9: Commit**

```bash
git add Server/WSGameServer/Repository Server/WSGameServer/User/User.Equip.cs Server/WSGameServer.Tests
git commit -m "feat: 장비 저장소 — 로그인 조회·지급·착용 매핑 트랜잭션

- LoginRepository가 t_user_equip·t_character_equip(JOIN)을 읽어 PlayerLoginData에 싣는다
- SaveCharacterEquipRepository — 비우기 전부 먼저, 넣기 뒤에 (UNIQUE equip_id)"
```

---

### Task 6: `User.Equip.cs` — 도메인 · 적재 · 장착/해제 · 지급 · 속도 반영

**Files:**
- Create: `Server/WSGameServer/User/Equip/Equip.cs`
- Modify: `Server/WSGameServer/User/User.Equip.cs` (Task 5의 최소 파일을 채운다)
- Modify: `Server/WSGameServer/User/User.cs` (생성자 `EquipCatalog? equips = null` · `Login()` 전송 순서)
- Modify: `Server/WSGameServer/User/User.DB.cs` (`LoadPlayerData`에 `LoadEquips`)
- Modify: `Server/WSGameServer/User/User.WorkStation.cs` (`ResolveSlotSpeed` 가산)
- Modify: `Server/WSGameServer.Tests/User/TestUserBuilder.cs` (`Equips` 카탈로그)
- Test: `Server/WSGameServer.Tests/User/UserEquipTest.cs`

**Interfaces:**
- Produces:
  - `Equip(long id, EquipTableRow row, int slotPosition)` — `Id` · `Row` · `Tid` · `Kind` · `Industry` · `SpeedAddPermille` · `EquippedCharacterId` · `EquippedSlot` · `SlotPosition` · `IsEquipped` · `Wear(long, EquipSlot)` · `TakeOff()` · `AppliesTo(IndustryType)`
  - `User`: `IReadOnlyCollection<Equip> Equips` · `LoadEquips(IReadOnlyList<UserEquipRow>, IReadOnlyList<CharacterEquipRow>)` · `SendEquipList()` · `TryEquip(long characterId, long equipId, EquipSlot slot, DateTime now)` · `TryUnequip(long characterId, EquipSlot slot, DateTime now)` · `GrantEquip(int equipTid)` → `GrantEquipRepository` · `OnEquipGranted(long, int, int)` · `int GetEquipSpeedAdd(long characterId, IndustryType industry)` · `bool TryGetEquip(long equipId, out Equip equip)`
- Consumes: Task 3 `EquipCatalog`, Task 4 패킷, Task 5 저장소.

- [ ] **Step 1: `TestUserBuilder`에 카탈로그 추가**

`Unlocks` 프로퍼티 아래:

```csharp
    /// <summary>장비 표. 비워 둔 채 <see cref="Build"/>하면 실제 엑셀 데이터가 들어간다. 기대값을 고정하려면 먼저 <c>Load</c>한다.</summary>
    public EquipCatalog Equips { get; } = new();
```

`Build`:

```csharp
        if (Unlocks.Count == 0 || Equips.Count == 0)
        {
            GameTableFixture.EnsureLoaded();
        }
        if (Unlocks.Count == 0)
        {
            Unlocks.LoadAll();
        }
        if (Equips.Count == 0)
        {
            Equips.LoadAll();
        }

        var user = new User(Channel, DB, Executor,
                            pid: _pid, nickname: "테스터", loggedInAt: Base, Drops, Levels, Growth, Unlocks, Equips);
```

- [ ] **Step 2: 실패하는 테스트**

```csharp
// Server/WSGameServer.Tests/User/UserEquipTest.cs
using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>의 장비 경로 — 거절 순서 · 자동 이동 · 정산→장착→속도 재확정 순서 · 저장 요청 · 싱크.
/// 속도가 재화 생성량에 곱해지므로, 정산보다 먼저 속도가 바뀌면 소급 지급이 된다.
/// </summary>
public class UserEquipTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;   // 전 산업 적성 ≥ 1
    private const long CharA = 500;
    private const long CharB = 501;

    // 테스트 전용 장비 표 — 엑셀 값에 기대지 않는다.
    private const int SwordTid   = 1001;  // 무기 · 전 산업 +10%
    private const int RodTid     = 1002;  // 무기 · 낚시 +30%
    private const int RingTid    = 2001;  // 장신구 · 전 산업 +5%
    private const int GemTid     = 3001;  // 보석 · 농사 +50% (낚시 슬롯엔 0)

    private const long Sword = 10, Rod = 11, Ring = 12, Gem = 13;

    public UserEquipTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWithEquips(params CharacterEquipRow[] worn)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.Equips.Load(new[]
        {
            new EquipTableRow { EquipTID = SwordTid, Name = "검",  EquipKind = EquipKind.Weapon,    Industry = IndustryType.None,    SpeedAddPermille = 100 },
            new EquipTableRow { EquipTID = RodTid,   Name = "대",  EquipKind = EquipKind.Weapon,    Industry = IndustryType.Fishing, SpeedAddPermille = 300 },
            new EquipTableRow { EquipTID = RingTid,  Name = "링",  EquipKind = EquipKind.Accessory, Industry = IndustryType.None,    SpeedAddPermille = 50 },
            new EquipTableRow { EquipTID = GemTid,   Name = "석",  EquipKind = EquipKind.Gem,       Industry = IndustryType.Farming, SpeedAddPermille = 500 },
        });
        var user = b.Build();

        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharA, character_tid = AllRounderTid, level = 1, exp = 0 },
            new CharacterRow { character_id = CharB, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        user.LoadEquips(
            new[]
            {
                new UserEquipRow { equip_id = Sword, equip_tid = SwordTid, slot_position = 0 },
                new UserEquipRow { equip_id = Rod,   equip_tid = RodTid,   slot_position = 1 },
                new UserEquipRow { equip_id = Ring,  equip_tid = RingTid,  slot_position = 2 },
                new UserEquipRow { equip_id = Gem,   equip_tid = GemTid,   slot_position = 3 },
            },
            worn);

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static void GiveFishingSlot(User user, long characterId)
    {
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, characterId, Base) });
    }

    private static int ExpectedSpeed(User user, long characterId, int addPermille)
    {
        user.TryGetCharacter(characterId, out var c).ShouldBeTrue();
        return WorkSpeed.From(c.GetBaseWorkSpeed(IndustryType.Fishing))
            .Add(addPermille)
            .Multiply(GatherSpeedMultiplier)
            .Resolve();
    }

    // ─────────────────────── 적재 ───────────────────────

    [Fact]
    public void 로그인_매핑이_착용_상태로_복원된다()
    {
        var (user, _) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryGetEquip(Rod, out var rod).ShouldBeTrue();
        rod.EquippedCharacterId.ShouldBe(CharA);
        rod.EquippedSlot.ShouldBe(EquipSlot.Weapon);
        user.GetEquipSpeedAdd(CharA, IndustryType.Fishing).ShouldBe(300);
    }

    [Fact]
    public void 없는_캐릭터를_가리키는_매핑은_건너뛴다()
    {
        var (user, _) = UserWithEquips(new CharacterEquipRow { character_id = 999, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryGetEquip(Rod, out var rod).ShouldBeTrue();
        rod.IsEquipped.ShouldBeFalse();
    }

    // ─────────────────────── 거절 ───────────────────────

    [Fact]
    public void 미보유_캐릭터면_CharacterNotOwned다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(999, Sword, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.CharacterNotOwned);
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 미보유_장비면_EquipNotOwned다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, 999, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.EquipNotOwned);
    }

    [Fact]
    public void None_칸이면_InvalidEquipSlot이다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Sword, EquipSlot.None, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidEquipSlot);
    }

    [Fact]
    public void 무기를_보석_칸에_끼우면_EquipKindMismatch다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Sword, EquipSlot.Gem, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.EquipKindMismatch);
        user.TryGetEquip(Sword, out var sword).ShouldBeTrue();
        sword.IsEquipped.ShouldBeFalse();
    }

    [Fact]
    public void 빈_칸을_해제하면_EquipSlotEmpty다()
    {
        var (user, b) = UserWithEquips();

        user.TryUnequip(CharA, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.EquipSlotEmpty);
    }

    // ─────────────────────── 장착 ───────────────────────

    [Fact]
    public void 장착하면_매핑_저장과_싱크가_나간다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Ring, EquipSlot.Accessory2, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        var sync = b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem();
        var info = sync.Equips.ShouldHaveSingleItem();
        info.EquipId.ShouldBe(Ring);
        info.EquippedCharacterId.ShouldBe(CharA);
        info.EquippedSlot.ShouldBe(EEquipSlot.Accessory2);

        var save = b.DB.PostedOf<SaveCharacterEquipRepository>().ShouldHaveSingleItem();
        save.Changes.ShouldBe(new[] { new EquipMappingChange(CharA, EquipSlot.Accessory2, Ring) });
    }

    [Fact]
    public void 장신구는_두_칸_어디든_들어간다()
    {
        var (user, b) = UserWithEquips();

        user.TryEquip(CharA, Ring, EquipSlot.Accessory1, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 같은_자리에_다시_장착하면_Ok지만_아무것도_안_한다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Sword });

        user.TryEquip(CharA, Sword, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.Channel.SentOf<S_EquipSyncResponse>().ShouldBeEmpty();
        b.DB.Posted.ShouldBeEmpty();
    }

    [Fact]
    public void 같은_칸에_있던_장비는_창고로_돌아간다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon, equip_id = Sword });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base);

        user.TryGetEquip(Sword, out var sword).ShouldBeTrue();
        sword.IsEquipped.ShouldBeFalse();
        var sync = b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem();
        sync.Equips.Select(e => e.EquipId).OrderBy(x => x).ShouldBe(new[] { Sword, Rod });
        sync.Equips.Single(e => e.EquipId == Sword).EquippedCharacterId.ShouldBe(0);
    }

    [Fact]
    public void 다른_캐릭터가_착용_중이면_옮겨_온다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharB, slot = (int)EquipSlot.Weapon, equip_id = Rod });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base);

        user.GetEquipSpeedAdd(CharB, IndustryType.Fishing).ShouldBe(0);
        user.GetEquipSpeedAdd(CharA, IndustryType.Fishing).ShouldBe(300);
        var save = b.DB.PostedOf<SaveCharacterEquipRepository>().ShouldHaveSingleItem();
        save.Changes.ShouldBe(new[]
        {
            new EquipMappingChange(CharB, EquipSlot.Weapon, 0),
            new EquipMappingChange(CharA, EquipSlot.Weapon, Rod),
        });
    }

    [Fact]
    public void 해제하면_창고로_가고_저장_요청이_나간다()
    {
        var (user, b) = UserWithEquips(new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Gem, equip_id = Gem });

        user.TryUnequip(CharA, EquipSlot.Gem, Base);

        b.Channel.SentOf<S_EquipResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem().Equips.ShouldHaveSingleItem().EquippedCharacterId.ShouldBe(0);
        b.DB.PostedOf<SaveCharacterEquipRepository>().ShouldHaveSingleItem()
            .Changes.ShouldBe(new[] { new EquipMappingChange(CharA, EquipSlot.Gem, 0) });
    }

    // ─────────────────────── 속도 ───────────────────────

    [Fact]
    public void 가산은_전_산업_장비와_슬롯_산업_장비만_합한다()
    {
        var (user, _) = UserWithEquips(
            new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Weapon,     equip_id = Sword },  // 전 산업 +100
            new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Accessory1, equip_id = Ring },   // 전 산업 +50
            new CharacterEquipRow { character_id = CharA, slot = (int)EquipSlot.Gem,        equip_id = Gem });   // 농사 +500 — 낚시엔 0

        user.GetEquipSpeedAdd(CharA, IndustryType.Fishing).ShouldBe(150);
        user.GetEquipSpeedAdd(CharA, IndustryType.Farming).ShouldBe(650);
        user.GetEquipSpeedAdd(0,     IndustryType.Fishing).ShouldBe(0);
    }

    [Fact]
    public void 배치_중인_캐릭터에_장착하면_슬롯_속도가_바뀌어_밀려_온다()
    {
        var (user, b) = UserWithEquips();
        GiveFishingSlot(user, CharA);
        user.RefreshWorkStationSpeed(Base, notify: false);
        b.Channel.Sent.Clear();

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base);

        b.Channel.SentOf<S_WorkStationSlotSyncResponse>().ShouldHaveSingleItem()
            .Slot.CurrentWorkSpeed.ShouldBe(ExpectedSpeed(user, CharA, 300));
    }

    [Fact]
    public void 장착_전에_먼저_정산한다()
    {
        // 기본 속도 1.0배 = 30초에 1판정. 10분 뒤 장착이면 정확히 20판정이 이전 속도로 정산돼야 한다.
        var (user, b) = UserWithEquips();
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharA, Base, WorkStationSlot.DefaultWorkSpeed) });

        user.TryEquip(CharA, Rod, EquipSlot.Weapon, Base.AddMinutes(10));

        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem().JudgeCount.ShouldBe(20);
        var kinds = b.Channel.Sent.Select(p => p.GetType()).ToList();
        kinds.IndexOf(typeof(S_GatherResultResponse)).ShouldBeLessThan(kinds.IndexOf(typeof(S_EquipResponse)));
    }

    // ─────────────────────── 지급 ───────────────────────

    [Fact]
    public void 지급은_첫_빈_창고_칸을_골라_저장을_요청한다()
    {
        var (user, b) = UserWithEquips();   // 칸 0~3이 차 있다

        user.GrantEquip(SwordTid);

        b.DB.PostedOf<GrantEquipRepository>().ShouldHaveSingleItem();
        // 지급 완료 콜백을 흉내 낸다 — 개체 20이 칸 4에 들어온다.
        user.OnEquipGranted(20, SwordTid, 4);

        user.TryGetEquip(20, out var granted).ShouldBeTrue();
        granted.SlotPosition.ShouldBe(4);
        b.Channel.SentOf<S_EquipSyncResponse>().ShouldHaveSingleItem().Equips.ShouldHaveSingleItem().EquipId.ShouldBe(20);
    }

    [Fact]
    public void 빈_칸이_중간에_있으면_그_칸을_쓴다()
    {
        var b = new TestUserBuilder();
        var user = b.Build();
        user.LoadEquips(new[]
        {
            new UserEquipRow { equip_id = 1, equip_tid = 1001, slot_position = 0 },
            new UserEquipRow { equip_id = 2, equip_tid = 1001, slot_position = 2 },
        }, Array.Empty<CharacterEquipRow>());

        user.NextFreeEquipPosition().ShouldBe(1);
    }

    [Fact]
    public void 테이블에_없는_TID는_지급하지_않는다()
    {
        var (user, b) = UserWithEquips();

        user.GrantEquip(9999).ShouldBeFalse();

        b.DB.Posted.ShouldBeEmpty();
    }
}
```

`GatherSpeedMultiplier`는 `GlobalUsing.cs`가 `Global`을 static using으로 열어 두었는지 확인한다(`User.WorkStation.cs`가 `GatherSpeedMultiplier`를 그대로 쓴다). 테스트 프로젝트에서 안 보이면 `Global.GatherSpeedMultiplier`로 쓴다.

- [ ] **Step 3: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~UserEquipTest"` → 컴파일 오류.

- [ ] **Step 4: `Equip.cs`**

```csharp
// Server/WSGameServer/User/Equip/Equip.cs
using GameData;

namespace WSGameServer;

// 유저가 소유한 장비 개체. Id(DB PK)와 Tid(테이블 정의)를 구분한다. 스탯은 Row에서 읽고 개체는 위치(착용·창고 칸)만 든다.
public sealed class Equip
{
    public Equip(long id, EquipTableRow row, int slotPosition)
    {
        Id           = id;
        Row          = row;
        SlotPosition = slotPosition;
    }

    /// <summary>장비 개체 PK (<c>t_user_equip.equip_id</c>). DB가 발급한다.</summary>
    public long Id { get; }

    public EquipTableRow Row { get; }

    public int          Tid              => Row.EquipTID;
    public EquipKind    Kind             => Row.EquipKind;
    public IndustryType Industry         => Row.Industry;
    public int          SpeedAddPermille => Row.SpeedAddPermille;

    /// <summary>착용 캐릭터 개체. 0이면 창고에 있다.</summary>
    public long EquippedCharacterId { get; private set; }

    /// <summary>착용 칸. 창고면 None.</summary>
    public EquipSlot EquippedSlot { get; private set; }

    /// <summary>창고 장비 탭의 칸 번호(0부터). 착용 중이어도 유지된다 — 해제하면 그 자리로 돌아간다.</summary>
    public int SlotPosition { get; set; }

    public bool IsEquipped => EquippedCharacterId != 0;

    public void Wear(long characterId, EquipSlot slot)
    {
        EquippedCharacterId = characterId;
        EquippedSlot        = slot;
    }

    public void TakeOff()
    {
        EquippedCharacterId = 0;
        EquippedSlot        = EquipSlot.None;
    }

    /// <summary>이 슬롯 산업에 가산이 붙는가. None(전 산업) 장비는 어디든 붙는다.</summary>
    public bool AppliesTo(IndustryType industry)
        => Industry == IndustryType.None || Industry == industry;
}
```

- [ ] **Step 5: `User.Equip.cs`** (Task 5 최소 파일을 통째로 교체)

```csharp
// Server/WSGameServer/User/User.Equip.cs
using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>보유 장비 개체. 키는 개체 PK(<c>t_user_equip.equip_id</c>)다.</summary>
    private readonly Dictionary<long, Equip> _equips = new();

    /// <summary>착용 매핑 (캐릭터, 칸) → 장비. <c>t_character_equip</c>의 메모리 사본이며 한 칸에 하나다.</summary>
    private readonly Dictionary<(long CharacterId, EquipSlot Slot), Equip> _worn = new();

    /// <summary>지급을 요청했지만 아직 PK가 돌아오지 않은 창고 칸. 연속 지급이 같은 칸을 고르지 않게 한다.</summary>
    private readonly HashSet<int> _pendingEquipPositions = new();

    public IReadOnlyCollection<Equip> Equips => _equips.Values;

    public bool TryGetEquip(long equipId, out Equip equip)
        => _equips.TryGetValue(equipId, out equip!);

    /// <summary>DB에서 읽은 개체·매핑을 적재한다(로그인 시 1회). 캐릭터 적재 뒤, 작업슬롯 적재 앞이다 — 슬롯 속도의 근거다.</summary>
    public void LoadEquips(IReadOnlyList<UserEquipRow> equipRows, IReadOnlyList<CharacterEquipRow> wornRows)
    {
        _equips.Clear();
        _worn.Clear();

        foreach (var r in equipRows)
        {
            // 테이블에 없는 TID는 건너뛴다 — 데이터 한 줄 때문에 로그인이 막히면 안 된다.
            if (!_equipCatalog.TryGet(r.equip_tid, out var row))
            {
                ServerLog.Warn("로그인", $"EquipTable에 없는 TID, 건너뜀: {r.equip_tid} (개체 {r.equip_id})");
                continue;
            }

            _equips[r.equip_id] = new Equip(r.equip_id, row, r.slot_position);
        }

        foreach (var r in wornRows)
        {
            var slot = (EquipSlot)r.slot;
            if (!_equips.TryGetValue(r.equip_id, out var equip) || !_characters.ContainsKey(r.character_id) || !EquipCatalog.IsValidSlot(slot))
            {
                ServerLog.Warn("로그인", $"착용 매핑이 가리키는 대상이 없어 건너뜀: 캐릭터 {r.character_id} 칸 {r.slot} 장비 {r.equip_id}");
                continue;
            }

            equip.Wear(r.character_id, slot);
            _worn[(r.character_id, slot)] = equip;
        }
    }

    /// <summary>보유 장비 전체 스냅샷(로그인 직후). 캐릭터 목록 뒤·슬롯 스냅샷 앞.</summary>
    public void SendEquipList()
    {
        Send(new S_EquipListResponse { Equips = _equips.Values.Select(ToEquipInfo).ToList() });
    }

    /// <summary>
    /// 장착. 검증 → <b>정산</b> → 매핑 갱신(이전 착용자 해제 · 같은 칸 장비 창고로) → 속도 재확정 → 저장(트랜잭션) → 응답·싱크.
    /// 정산이 매핑보다 먼저여야 새 속도가 이전 구간에 소급되지 않는다.
    /// </summary>
    public void TryEquip(long characterId, long equipId, EquipSlot slot, DateTime now)
    {
        if (!TryGetCharacter(characterId, out _))
        {
            Reject(EResultCode.CharacterNotOwned, "미보유 캐릭터");
            return;
        }

        if (!TryGetEquip(equipId, out var equip))
        {
            Reject(EResultCode.EquipNotOwned, $"미보유 장비 {equipId}");
            return;
        }

        if (!EquipCatalog.IsValidSlot(slot))
        {
            Reject(EResultCode.InvalidEquipSlot, "칸 범위 밖");
            return;
        }

        if (!EquipCatalog.CanEquip(equip.Kind, slot))
        {
            Reject(EResultCode.EquipKindMismatch, $"{equip.Kind}는 {slot}에 못 낀다");
            return;
        }

        if (equip.EquippedCharacterId == characterId && equip.EquippedSlot == slot)
        {
            Send(new S_EquipResponse { Result = EResultCode.Ok, CharacterId = characterId, Slot = (EEquipSlot)slot });
            return;
        }

        SettleWorkStation(now);

        var changed = new List<Equip>();
        var changes = new List<EquipMappingChange>();

        // 1) 이 장비가 다른 곳에 있었으면 그 칸을 비운다.
        if (equip.IsEquipped)
        {
            _worn.Remove((equip.EquippedCharacterId, equip.EquippedSlot));
            changes.Add(new EquipMappingChange(equip.EquippedCharacterId, equip.EquippedSlot, 0));
        }

        // 2) 대상 칸에 있던 장비는 창고로. 그 칸의 DB 행은 3)의 변경이 덮는다.
        if (_worn.TryGetValue((characterId, slot), out var displaced))
        {
            displaced.TakeOff();
            changed.Add(displaced);
        }

        // 3) 새 매핑
        equip.Wear(characterId, slot);
        _worn[(characterId, slot)] = equip;
        changed.Add(equip);
        changes.Add(new EquipMappingChange(characterId, slot, equipId));

        RefreshWorkStationSpeed(now);
        PostDBTask(new SaveCharacterEquipRepository(this, changes));

        ServerLog.Info("장비", $"장착 Uid={Uid} 캐릭터 {characterId} {slot} ← 장비 {equipId}(TID {equip.Tid})");
        Send(new S_EquipResponse { Result = EResultCode.Ok, CharacterId = characterId, Slot = (EEquipSlot)slot });
        Send(new S_EquipSyncResponse { Equips = changed.Select(ToEquipInfo).ToList() });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("장비", $"장착 거절 — {reason}. Uid={Uid} 캐릭터 {characterId} 장비 {equipId} 칸 {slot}");
            Send(new S_EquipResponse { Result = code, CharacterId = characterId, Slot = (EEquipSlot)slot });
        }
    }

    /// <summary>해제. 정산 → 매핑 제거 → 속도 재확정 → 저장 → 응답·싱크. 순서 이유는 <see cref="TryEquip"/>과 같다.</summary>
    public void TryUnequip(long characterId, EquipSlot slot, DateTime now)
    {
        if (!TryGetCharacter(characterId, out _))
        {
            Reject(EResultCode.CharacterNotOwned, "미보유 캐릭터");
            return;
        }

        if (!EquipCatalog.IsValidSlot(slot))
        {
            Reject(EResultCode.InvalidEquipSlot, "칸 범위 밖");
            return;
        }

        if (!_worn.TryGetValue((characterId, slot), out var equip))
        {
            Reject(EResultCode.EquipSlotEmpty, "빈 칸");
            return;
        }

        SettleWorkStation(now);

        equip.TakeOff();
        _worn.Remove((characterId, slot));

        RefreshWorkStationSpeed(now);
        PostDBTask(new SaveCharacterEquipRepository(this, new[] { new EquipMappingChange(characterId, slot, 0) }));

        ServerLog.Info("장비", $"해제 Uid={Uid} 캐릭터 {characterId} {slot} → 장비 {equip.Id}");
        Send(new S_EquipResponse { Result = EResultCode.Ok, CharacterId = characterId, Slot = (EEquipSlot)slot });
        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("장비", $"해제 거절 — {reason}. Uid={Uid} 캐릭터 {characterId} 칸 {slot}");
            Send(new S_EquipResponse { Result = code, CharacterId = characterId, Slot = (EEquipSlot)slot });
        }
    }

    /// <summary>이 캐릭터가 착용한 장비 중 이 산업에 붙는 가산의 합(천분율). 미배치(0)면 0.</summary>
    public int GetEquipSpeedAdd(long characterId, IndustryType industry)
    {
        if (characterId == 0)
        {
            return 0;
        }

        var sum = 0;
        foreach (var ((wornBy, _), equip) in _worn)
        {
            if (wornBy == characterId && equip.AppliesTo(industry))
            {
                sum += equip.SpeedAddPermille;
            }
        }

        return sum;
    }

    /// <summary>창고 장비 탭의 첫 빈 칸(0부터). 지급 대기 중인 칸도 찬 것으로 본다.</summary>
    public int NextFreeEquipPosition()
    {
        var used = new HashSet<int>(_pendingEquipPositions);
        foreach (var equip in _equips.Values)
        {
            used.Add(equip.SlotPosition);
        }

        var position = 0;
        while (used.Contains(position))
        {
            position++;
        }

        return position;
    }

    /// <summary>장비 개체 1개를 지급한다(치트·앞으로의 획득 경로가 같은 길을 쓴다). 개체 PK는 <see cref="OnEquipGranted"/>로 돌아온다.</summary>
    /// <returns>지급을 요청했으면 true. 테이블에 없는 TID면 false.</returns>
    public bool GrantEquip(int equipTid)
    {
        if (!_equipCatalog.TryGet(equipTid, out _))
        {
            ServerLog.Warn("장비", $"지급 거절 — EquipTable에 없는 TID {equipTid}. Uid={Uid}");
            return false;
        }

        var position = NextFreeEquipPosition();
        _pendingEquipPositions.Add(position);
        PostDBTask(new GrantEquipRepository(this, equipTid, position));
        return true;
    }

    /// <summary>지급이 끝나면 불린다(로직 스레드). 메모리에 올리고 그 개체만 밀어 준다.</summary>
    public void OnEquipGranted(long equipId, int equipTid, int slotPosition)
    {
        _pendingEquipPositions.Remove(slotPosition);

        if (!_equipCatalog.TryGet(equipTid, out var row))
        {
            ServerLog.Warn("장비", $"EquipTable에 없는 TID, 적재 건너뜀: {equipTid} (개체 {equipId})");
            return;
        }

        var equip = new Equip(equipId, row, slotPosition);
        _equips[equipId] = equip;

        ServerLog.Info("장비", $"지급 Uid={Uid} 장비 {equipId}(TID {equipTid}) 칸 {slotPosition}");
        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
    }

    private static EquipInfo ToEquipInfo(Equip e)
    {
        return new EquipInfo
        {
            EquipId             = e.Id,
            EquipTid            = e.Tid,
            EquippedCharacterId = e.EquippedCharacterId,
            EquippedSlot        = (EEquipSlot)e.EquippedSlot,
            SlotPosition        = e.SlotPosition,
        };
    }
}
```

- [ ] **Step 6: `User.cs`** — 필드·생성자·로그인 전송 순서

`_unlockCatalog` 필드 아래:

```csharp
    /// <summary>장비 정의 인덱스. 적재·지급·장착 검증에 쓴다. 규약은 위와 같다.</summary>
    private readonly EquipCatalog _equipCatalog;
```

생성자 시그니처 끝에 `EquipCatalog? equips = null` 추가, 본문에 `_equipCatalog = equips ?? EquipCatalog.Instance;`.

`Login()`의 `SendCharacters();` 다음 줄:

```csharp
        // 장비를 캐릭터 뒤·슬롯 앞에 보낸다 — EquippedCharacterId가 캐릭터를, 슬롯 속도가 장비를 전제한다.
        SendEquipList();   // S_EquipListResponse
```

- [ ] **Step 7: `User.DB.cs`** — `LoadPlayerData`에서 `LoadIndustryLevels(...)` 다음:

```csharp
        // 착용 장비가 슬롯 속도의 근거라 캐릭터 뒤·슬롯 앞에 적재한다.
        LoadEquips(data.EquipRows, data.CharacterEquipRows);
```

- [ ] **Step 8: `User.WorkStation.cs`** — `ResolveSlotSpeed`

```csharp
        return WorkSpeed.From(baseSpeed)
            // 착용 장비 가산(전 산업 + 슬롯 산업). 특성·부스트는 여기에 .Add(천분율)로 더 붙는다 — 개수가 늘어도 각 보정의 몫은 그대로다.
            .Add(GetEquipSpeedAdd(slot.CharacterId, slot.Industry))
            .Multiply(GatherSpeedMultiplier)
            .Resolve();
```

- [ ] **Step 9: 전체 테스트 통과**

Run: `dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj 2>&1 | grep -E "error|통과!|실패"` → 실패 0. `UserEquipTest` 19개 포함.

- [ ] **Step 10: Commit**

```bash
git add Server/WSGameServer/User Server/WSGameServer.Tests/User
git commit -m "feat: 장비 장착·해제와 채취 속도 가산

- User.Equip — 적재·장착(자동 이동)·해제·지급·창고 첫 빈 칸
- 정산 → 매핑 → 속도 재확정 순서로 소급을 막는다
- ResolveSlotSpeed에 착용 장비 가산(전 산업 + 슬롯 산업) 합산"
```

---

### Task 7: 핸들러 · 치트 · 더미 클라 메뉴

**Files:**
- Modify: `Server/WSGameServer/Network/ClientPacketHandler.cs`
- Modify: `Server/WSGameServer/User/User.Cheat.cs`
- Modify: `Server/MikaDummyClient/Menu/PacketMenu.cs`
- Modify: `Server/docs/치트.md` (2장 표 · 3장)
- Test: `Server/WSGameServer.Tests/User/UserCheatTest.cs` (추가)

**Interfaces:**
- Consumes: `User.TryEquip` · `User.TryUnequip` · `User.GrantEquip` (Task 6)

- [ ] **Step 1: 실패하는 치트 테스트 — `UserCheatTest.cs`에 추가** (기존 테스트의 유저 조립 헬퍼를 그대로 쓴다. 파일을 열어 admin 유저를 만드는 헬퍼 이름을 확인하고 맞춘다.)

```csharp
    [Fact]
    public void GiveEquip은_지급을_요청한다()
    {
        var (user, b) = AdminUser();   // 기존 헬퍼 — admin_level ≥ 1 유저

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.GiveEquip, Arg1 = 1001 }, Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        b.DB.PostedOf<GrantEquipRepository>().ShouldHaveSingleItem();
    }

    [Fact]
    public void GiveEquip은_없는_TID를_거절한다()
    {
        var (user, b) = AdminUser();

        user.ExecuteCheat(new C_CheatRequest { Command = ECheatCommand.GiveEquip, Arg1 = 9999 }, Base);

        b.Channel.SentOf<S_CheatResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.InvalidCheatArgs);
        b.DB.Posted.ShouldBeEmpty();
    }
```

Run: `dotnet test ... --filter "FullyQualifiedName~UserCheatTest"` → `GiveEquip` 테스트 2개 실패(`InvalidCheatCommand`).

- [ ] **Step 2: `User.Cheat.cs`**

switch에 `ECheatCommand.GiveEquip => CheatGiveEquip(req.Arg1),` 추가. `CheatUnlock` 아래:

```csharp
    private (EResultCode, string) CheatGiveEquip(long equipTid)
    {
        if (equipTid <= 0 || equipTid > int.MaxValue || !_equipCatalog.TryGet((int)equipTid, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"EquipTable에 없는 TID {equipTid}");
        }

        // 앞으로 생길 획득 경로와 같은 지급 함수 — PK 발급 후 S_EquipSyncResponse까지 동일하다.
        GrantEquip((int)equipTid);
        return (EResultCode.Ok, $"장비 {equipTid} 지급 요청");
    }
```

- [ ] **Step 3: `ClientPacketHandler.cs`** — `Handle_C_UnlockRequest` 뒤

```csharp
    /// <summary>장비 장착. 보유·칸·종류 검증과 정산 순서는 User가 맡는다.</summary>
    [PacketHandler]
    public static void Handle_C_EquipRequest(ISession session, C_EquipRequest req)
    {
        ServerLog.Debug("장비", $"장착 요청 캐릭터={req.CharacterId} 장비={req.EquipId} 칸={req.Slot} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_EquipResponse { Result = EResultCode.NotLoggedIn, CharacterId = req.CharacterId, Slot = req.Slot });
            return;
        }

        user.TryEquip(req.CharacterId, req.EquipId, (GameData.EquipSlot)req.Slot, DateTime.UtcNow);
    }

    /// <summary>장비 해제.</summary>
    [PacketHandler]
    public static void Handle_C_UnequipRequest(ISession session, C_UnequipRequest req)
    {
        ServerLog.Debug("장비", $"해제 요청 캐릭터={req.CharacterId} 칸={req.Slot} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_EquipResponse { Result = EResultCode.NotLoggedIn, CharacterId = req.CharacterId, Slot = req.Slot });
            return;
        }

        user.TryUnequip(req.CharacterId, (GameData.EquipSlot)req.Slot, DateTime.UtcNow);
    }
```

- [ ] **Step 4: 더미 클라 메뉴** — `PacketMenu.cs`의 `SendUnlock` 항목·메서드를 본떠 두 항목 추가

목록에:

```csharp
                new ClientAction("Equip (장착 — 캐릭터ID 장비ID 칸1~4)", SendEquip),
                new ClientAction("Unequip (해제 — 캐릭터ID 칸1~4)", SendUnequip),
```

메서드 (`SendUnlock` 아래):

```csharp
        private void SendEquip()
        {
            Console.Write("CharacterId EquipId Slot(1무기 2·3장신구 4보석) > ");
            var parts = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !long.TryParse(parts[0], out var characterId) || !long.TryParse(parts[1], out var equipId) || !byte.TryParse(parts[2], out var slot))
            {
                Console.WriteLine("[Client] 숫자 세 개를 띄어 적습니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_EquipRequest { CharacterId = characterId, EquipId = equipId, Slot = (EEquipSlot)slot });
        }

        private void SendUnequip()
        {
            Console.Write("CharacterId Slot(1~4) > ");
            var parts = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var characterId) || !byte.TryParse(parts[1], out var slot))
            {
                Console.WriteLine("[Client] 숫자 두 개를 띄어 적습니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_UnequipRequest { CharacterId = characterId, Slot = (EEquipSlot)slot });
        }
```

더미 클라에 서버 응답 핸들러가 별도로 있으면(`grep -rn "Handle_S_UnlockResponse" Server/MikaDummyClient`) 같은 꼴로 `S_EquipListResponse`·`S_EquipSyncResponse`·`S_EquipResponse` 로그 핸들러를 추가한다.

- [ ] **Step 5: `Server/docs/치트.md`** — 2장 표 끝에 행 추가, 헤더 `최종 업데이트` 오늘 날짜

```
| `GiveEquip = 8` | `EquipTID` | — | `GrantEquip` — 개체 1개 INSERT 후 창고 첫 빈 칸 · `S_EquipSyncResponse`까지 같다 | 테이블에 없는 TID → `InvalidCheatArgs` |
```

- [ ] **Step 6: 전체 빌드·테스트**

Run: `dotnet build Server/Server.sln 2>&1 | grep -E " error |오류" ; dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj 2>&1 | grep -E "통과!|실패"` (솔루션 파일명은 `ls Server/*.sln`으로 확인) → 오류 0 · 실패 0.

- [ ] **Step 7: Commit**

```bash
git add Server/WSGameServer/Network/ClientPacketHandler.cs Server/WSGameServer/User/User.Cheat.cs Server/MikaDummyClient Server/docs/치트.md Server/WSGameServer.Tests/User/UserCheatTest.cs
git commit -m "feat: 장비 장착·해제 핸들러와 GiveEquip 치트"
```

---

### Task 8: 실서버 검증 · 문서 반영 · 일감 갱신

**Files:**
- Modify: `tasks/T-002-장비슬롯.md` · `tasks/README.md`(INDEX 행)
- Modify: `GameDesign/design/character/README.md` · `GameDesign/design/workslot/README.md` · `GameDesign/design/게임기획코어.md`
- Modify: `Server/docs/채취-정산.md`(5장) · `Server/docs/데이터-카탈로그.md`(EquipCatalog 절)
- Create: `.claude/Agent/2026-09-17-equip-server.md` + `README.md` INDEX 한 줄

- [ ] **Step 1: 실서버 왕복** (메모리 `unity-cli-live-test` 절차)

1. 실행 중인 서버가 없는지 확인 후 서버 빌드·기동: `cd Server/WSGameServer && sleep 7200 | dotnet run -c Debug` 를 백그라운드로. 기동 로그에 `EquipCatalog` 예외가 없어야 한다.
2. 테스트 계정: `python`으로 `t_user`에 `provider_id='equip-test', admin_level=1` 삽입.
3. `unity status --json` → ready. `unity command editor_play`. `eval_file`로 로그인(`C_LoginRequest`, pid `equip-test`) → 콘솔에서 `S_CharacterListResponse`의 CharacterId와 `S_EquipListResponse — 0개` 확인.
4. `eval_file`: `C_CheatRequest { Command = GiveEquip, Arg1 = 1002 }` → 콘솔 `장비 동기화 — #N(TID 1002)→캐릭터 0 None 칸 0`.
5. `eval_file`: 캐릭터를 낚시 슬롯 0에 배치(`C_WorkStationAssignRequest`) → `S_WorkStationAssignResponse`의 `CurrentWorkSpeed` 기록.
6. `eval_file`: `C_EquipRequest { CharacterId, EquipId = N, Slot = Weapon }` → `장비 Weapon @캐릭터 → Ok`, `장비 동기화 … →캐릭터 <id> Weapon`, `S_WorkStationSlotSyncResponse`의 `CurrentWorkSpeed`가 5번 값의 1.3배(정수 절삭)인지 확인.
7. `eval_file`: `C_EquipRequest { …, Slot = Gem }` → `EquipKindMismatch`. `C_UnequipRequest { …, Slot = Weapon }` → Ok + 속도 원복.
8. 재로그인(에디터 정지 → 플레이) 전에 다시 장착해 두고, 재로그인 후 `S_EquipListResponse`에 `→캐릭터 <id> Weapon`으로 복원되는지 확인.
9. 서버 종료. `t_user`·`t_user_equip`·`t_character`·`t_character_equip`·`t_user_workstation_slot`에서 테스트 계정 행을 지운다. `git diff --stat Server/Shared/game.sqlite3`가 DDL 커밋 이후 변화 없음(또는 바이너리 동일)인지 확인.

기대와 다르면 여기서 멈추고 원인을 고친 뒤(테스트 추가) 다시 돈다.

- [ ] **Step 2: 기획 문서 반영** (각 문서의 `> **바뀌면 갱신:**` 블록을 따라 전파)

- `character/README.md` 1장 #6 행: "효과는 속도식의 **가산** 항으로 붙는다 (일감 T-002)" → "효과는 속도식의 **가산** 항. **대상 산업 지정(None=전 산업)** · 서버 구현 완료(2026-09-17). 획득 경로는 미정(치트만)". 2.3의 "장비가 적성을 올리는가는 아직 확정이 아니다" 인용 블록을 "**장비는 적성을 바꾸지 않는다** (2026-09-17 확정 — 속도 가산만). 적성 푸시(T-022)는 이 결정으로 필요 없어졌다"로 교체. 6장 표에 `Equip.xlsx` · `t_user_equip`·`t_character_equip` · 패킷 5개 · `User.Equip.cs` 행 추가.
- `workslot/README.md` 3.4 가산 표의 "**장비**" 항에 "대상 산업 일치 또는 전 산업인 장비만 합산 — 서버 `GetEquipSpeedAdd`" 추가.
- `게임기획코어.md` 5장 "캐릭터 장비 부위" 행에 "2026-09-17 서버 구현 — 종류(EquipKind)·칸(EquipSlot) 분리, 산업 지정 가산" 덧붙임. `최종 업데이트` 갱신.
- `powershell -File GameDesign/check-doc-graph.ps1`로 링크·갱신일 검사 통과.

- [ ] **Step 3: 서버 문서**

- `Server/docs/채취-정산.md` 5장 가산 행: "특성 패시브 · 액티브 부스트 · 장비 (아직 미작성)" → "**장비(구현)** · 특성·부스트(미작성). 장비는 `User.GetEquipSpeedAdd` — 착용 장비 중 `Industry == 슬롯 산업 || None`의 합".
- `Server/docs/데이터-카탈로그.md`에 `## 5. EquipCatalog` 절: TID 인덱스 · `CanEquip` 종류↔칸 표 · 칸을 나눈 이유(매핑 유일키).
- `Server/CLAUDE.md` 문서 표는 그대로(문서 신설 없음).

- [ ] **Step 4: 일감** (`task-writer` 스킬)

`tasks/T-002-장비슬롯.md`: 상태 `완료`, 할 일 체크, "남은 질문"을 결정 표로 교체(효과 모양·정의 테이블·칸 선택·자동 이동·강화 보류·획득 치트뿐), 관련 커밋 해시. `tasks/README.md` INDEX의 T-002 행을 완료로, T-043·T-022 행의 선행 설명 갱신(T-022는 "장비가 적성을 안 바꾸므로 사유 소멸 — 보류/폐기 검토"). 완료된 일감 파일은 `tasks/archive/`로 옮기는 규칙이면 옮긴다(`tasks/README.md` 상단 규칙 확인).

- [ ] **Step 5: 작업 로그** (`agent-log-writer`) — `.claude/Agent/2026-09-17-equip-server.md`: 결정·근거(종류/칸 분리, 자동 이동 트랜잭션, 저장소가 equip_id DELETE를 하지 않는 이유, T-022 사유 소멸), 실서버 검증 결과, 후속(T-043 클라 탭·T-058 장비 탭 정렬·획득 경로 일감). `README.md` INDEX 최신순 한 줄.

- [ ] **Step 6: Commit**

```bash
git add tasks GameDesign/design Server/docs .claude/Agent
git commit -m "docs: T-002 장비 시스템 완료 — 기획·서버 문서·일감 반영"
```
