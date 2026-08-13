# CLAUDE.md — 서버

> 최종 업데이트: 2026-08-13 (루트 `CLAUDE.md`에서 서버 파트를 분리)

`Server/` · `Assets/Scripts_Server/` 작업 시 참고하는 문서다.
공통 규칙(환경·협업·이름 규칙)은 저장소 루트의 [`CLAUDE.md`](../CLAUDE.md)를 함께 본다.
게임 시스템·데이터에 닿는 작업이면 [`GameDesign/CLAUDE.md`](../GameDesign/CLAUDE.md)도 읽는다.

---

## 구성

서버는 저장소 루트의 독립 .NET 솔루션(`Server/`)이다.
패킷 정의(`Server/MikaProtocol`)만 Unity(`Assets/Scripts_Server/Protocol`)로 단방향 미러링된다.

| 항목 | 내용 |
|------|------|
| 서버 런타임 | .NET 10 (WSGameServer) / MikaProtocol 멀티타깃 `net9.0`·`netstandard2.1` |
| 직렬화 | MemoryPack |
| 패킷 핸들러 생성 | Roslyn Source Generator (빌드 타임) |
| DB | SQLite (`Server/Shared/game.sqlite3`) |

### 폴더

| 경로 | 내용 |
|------|------|
| `Server/MikaNetwork.Lib/` | **게임과 무관한 재사용 네트워크 프레임워크** — `MikaNetwork.Core`·`.Client`·`.Server`·`MikaUtils`·`MikaSourceGen` |
| `Server/MikaProtocol/` | **패킷 정의 원본.** 여기서만 수정한다 |
| `Server/GameData/` | (생성) 엑셀에서 생성된 테이블 정의(Row/Enum/GameTable/TableSet) — **직접 수정 금지** |
| `Server/Shared/Data/` | (생성) MemoryPack 바이너리 `*.bytes` |
| `Server/WSGameServer.Tests/` | 서버 유닛 테스트 — **xUnit + Shouldly + Moq** |
| `Server/docs/` | 서버 전용 문서 — [`테스트커버리지.md`](docs/테스트커버리지.md) |
| `Assets/Scripts_Server/Protocol/` | (미러) `Server/MikaProtocol` 사본 — **직접 수정 금지** |
| `Assets/Scripts_Server/GameData/` | (미러) `Server/GameData` 사본 — **직접 수정 금지** |
| `Assets/StreamingAssets/Data/` | (미러) `Server/Shared/Data`의 `*.bytes` |
| `Assets/Scripts_Server/` (`Network`·`Test`·`Utils`) | Unity 측 네트워크/서버 연동 코드 |

### 미러링

- 패킷 정의는 `Server/MikaProtocol` 빌드 시 post-build로 `sync-protocol-to-unity.ps1`이 실행되어
  `Assets/Scripts_Server/Protocol`로 단방향 복사된다(소스 `MikaProtocol` → 대상 `Protocol`).
- **Roslyn 분석기(`MikaSourceGen`)도 빌드 시 `Assets/Plugins/Analyzers/`로 자동 복사된다.**
  Unity는 이 DLL을 `RoslynAnalyzer` 라벨로 로드해 핸들러 누락 경고(MIKA001)를 낸다.
  Unity 에디터가 켜져 있으면 파일이 잠겨 복사가 실패할 수 있다(빌드는 통과) — Unity를 닫고 다시 빌드한다.

### 경계 — "게임을 아는가"

**`MikaNetwork.Lib` 안팎의 경계는 "게임을 아는가"다.** 프레임워크는 게임 타입을 모른다 —
`MikaProtocol`(게임 패킷)·`GameData`(게임 테이블)를 Lib 안으로 넣지 않는다.

`MikaProtocol`·`GameData`·`ExcelGenerator`·`Shared`는 **미러링·파이프라인 경로가 위치에 묶여 있어**
옮기면 `ExcelGenerator/Program.cs`의 소스 상대경로가 조용히 어긋난다. 위치를 유지한다.

---

## 테스트

`Server/WSGameServer.Tests`(솔루션 포함). **xUnit** + **Shouldly**(단언) + **Moq**(목).
세 네임스페이스는 csproj의 `<Using>`으로 전역 등록돼 있어 테스트 파일에 `using`을 적지 않는다.

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

- 테스트 이름은 한글로 **동작을 서술**한다 (예: `만료된_티켓은_소모되지_않는다`).
- `SmokeTest.cs`는 프레임워크 연결 확인용이다. 실제 테스트는 새 파일로 나눈다.
- 작성 규칙·red-green 절차는 [`server-tdd`](.claude/skills/server-tdd/SKILL.md) 스킬 참조.
- **실행 중인 `WSGameServer.exe`가 있으면 DLL 잠금(MSB3021)으로 빌드가 실패한다.** 종료하고 돌린다.
  (Unity 에디터는 분석기 DLL 복사만 막으므로 테스트에는 영향이 없다)

### 커버리지

```powershell
powershell -File Server/run-coverage.ps1          # 낮은 순 25개 + 전체 수치
powershell -File Server/run-coverage.ps1 -Top 0   # 전부
powershell -File Server/run-coverage.ps1 -Html    # HTML 리포트(reportgenerator 필요)
```

**필터 없이 `--collect`만 쓰면 숫자가 쓸모없다.** MemoryPack 생성물(`*.g.cs`)이 전체 라인의
절반을 넘어 손으로 쓴 코드가 그 안에 묻힌다(필터 전 17.5% → 후 45.4%).
제외 규칙은 `Server/coverlet.runsettings`에 있고, 측정 대상은 **`WSGameServer`뿐**이다.

⚠️ **커버리지를 목표로 삼지 않는다.** 단언 없는 테스트로도 숫자는 올라간다 —
빨강만 믿을 만하고 초록은 못 믿는다. 상세는
[`Server/docs/테스트커버리지.md`](docs/테스트커버리지.md) 참조.

---

## 서버 작업 규칙

- 패킷 정의는 `Server/MikaProtocol`에서만 수정한다. `Assets/Scripts_Server/Protocol`은
  `sync-protocol-to-unity.ps1`이 덮어쓰는 사본이므로 직접 수정하지 않는다.
- **서버 로그는 `Console.WriteLine`이 아니라 `ServerLog`(`Server/WSGameServer/Common/ServerLog.cs`)를 쓴다.**
  시각·레벨·스레드·분류가 함께 남아야 로그를 읽을 수 있다.
  `MikaNetwork.Lib`은 로그 정책을 갖지 않는다 — 훅(`MikaPacketManager.Dispatching`,
  `MikaSessionPacketExtensions.Sent`, `MikaServer.Connected`)만 뚫고 호스트가 채운다.
- 생성물(`Server/GameData`, `Server/Shared/Data`)은 직접 고치지 않는다.
  엑셀에서 고치고 파이프라인을 돌린다 — [`GameDesign/CLAUDE.md`](../GameDesign/CLAUDE.md) 참조.
- 코드는 한글 주석을 사용한다.

---

## 서버 스킬

서버 스킬은 `.claude/skills/server/`가 아니라 **`Server/.claude/skills/`** 에 있다
(디렉터리 스코프 — `Server/` 아래 파일을 다룰 때 적용된다).

| 스킬 | 경로 | 내용 |
|------|------|------|
| `packet-creator` | [`Server/.claude/skills/packet-creator/SKILL.md`](.claude/skills/packet-creator/SKILL.md) | MikaProtocol 패킷 추가 절차 (PacketId·MemoryPackable·핸들러) |
| `sqlite-sql-creator` | [`Server/.claude/skills/sqlite-sql-creator/SKILL.md`](.claude/skills/sqlite-sql-creator/SKILL.md) | SQLite DDL·쿼리 규칙 (STRICT·주석 필수·인덱스 근거) |
| `server-tdd` | [`Server/.claude/skills/server-tdd/SKILL.md`](.claude/skills/server-tdd/SKILL.md) | 서버 테스트 작성 규칙 + red-green 루프 (한글 테스트명·기대값 리터럴·목은 경계에서만) |
| `server-code-style` | [`Server/.claude/skills/server-code-style/SKILL.md`](.claude/skills/server-code-style/SKILL.md) | 서버 코드 작성 스타일 (주석은 꼭 필요한 곳에만·한글 주석) |

공용 스킬(`commit-convention`·`agent-log-reader` 등)은 루트 [`CLAUDE.md`](../CLAUDE.md) 참조.
