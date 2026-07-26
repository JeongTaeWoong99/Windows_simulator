# CLAUDE.md

> 최종 업데이트: 2026-07-27

이 문서는 Claude Code로 작업할 때 공통으로 유의·협의해야 할 내용을 정리한 가이드다.
데스크톱 위에서 동작하는 투명 창(데스크톱 윈도우 제어)과 네트워크 기능을 결합하는 프로젝트로,
클라이언트와 서버를 한 저장소에서 영역을 나눠 협업한다.

서버는 저장소 루트의 독립 .NET 솔루션(`Server/`)으로 존재하며,
패킷 정의(`Server/MikaProtocol`)만 Unity(`Assets/Scripts/Protocol`)로 단방향 미러링된다.

---

## ⚠️ 게임 작업 전 필독 — 게임기획코어.md

**게임 시스템·콘텐츠에 닿는 작업은 착수 전에
[`GameDesign/기획/게임기획코어.md`](GameDesign/기획/게임기획코어.md)를 반드시 먼저 읽는다.**

- 이 문서가 게임의 **단일 진입점**이다. 정체성·설계 원칙(P1~P4)·코어 루프·시스템 지도·
  **확정/미확정 현황**을 담는다.
- 게임기획코어를 읽은 뒤, 해당 영역의 **상세 기획안**(`GameDesign/기획/<시스템>/README.md`)을 이어서 읽는다.
- **게임기획코어 5장의 "미확정" 항목은 임의로 결정하지 않는다.** 필요하면 사용자에게 먼저 확인한다.
- 절차·예외·문서 갱신 규칙은 [`game-design-reference`](.claude/skills/common/game-design-reference/SKILL.md) 스킬 참조.

대상 작업: 채취·퀘스트·특성·아이템·거래·성장곡선·위젯 UI 로직, `GameDesign/Excel` 데이터 변경,
게임 데이터 테이블/패킷/DB 스키마 설계, 기획 문서 수정.
(게임 규칙과 무관한 순수 인프라 작업은 제외)

---

## 환경

| 항목 | 내용 |
|------|------|
| Unity 버전 | 6000.3.10f1 |
| 렌더 파이프라인 | Built-in |
| 서버 런타임 | .NET 10 (WSGameServer) / MikaProtocol 멀티타깃 `net9.0`·`netstandard2.1` |
| 직렬화 | MemoryPack |
| 패킷 핸들러 생성 | Roslyn Source Generator (빌드 타임) |
| DB | SQLite (`Server/Shared/game.sqlite3`) |

---

## 폴더 구조

| 경로 | 담당 | 내용 |
|------|------|------|
| `GameDesign/기획/게임기획코어.md` | 공용 | **게임 기획 최상위 문서** — 게임 작업 착수 전 필독. 정체성·설계 원칙·코어 루프·시스템 지도·확정/미확정 현황 |
| `GameDesign/기획/` | 공용 | **게임 기획 단일 진실** — 게임기획코어 + 시스템별 상세 기획안(`<시스템>/README.md`) + 1차 산업(`자원채취/<산업>/README.md`) + 설계 평가(`기획평가.md`) |
| `GameDesign/Excel/` | 공용 | **게임 데이터 단일 진실** — 기획 데이터 엑셀(`Enum.xlsx`·`Item.xlsx` …). 서버/클라 어느 쪽 폴더에도 속하지 않는 공용 입력 |
| `GameDesign/DataLog/` | 공용(생성) | 생성된 `.bytes`를 되읽어 덤프한 JSON. 엑셀 대조·diff 리뷰용 — **직접 수정 금지** |
| `Server/` | 서버 | **서버 단일 진실** — .NET 솔루션(MikaNetwork 모듈 + WSGameServer). 패킷 정의 원본 = `Server/MikaProtocol` |
| `Server/GameData/` | 서버(생성) | 엑셀에서 생성된 테이블 정의(Row/Enum/GameTable/TableSet) — **직접 수정 금지** |
| `Server/Shared/Data/` | 서버(생성) | MemoryPack 바이너리 `*.bytes` |
| `Assets/Scripts/Protocol/` | 서버(미러) | `Server/MikaProtocol`에서 자동 복사되는 사본 — **직접 수정 금지** |
| `Assets/Scripts_Server/GameData/` | 서버(미러) | `Server/GameData`에서 자동 복사되는 사본 — **직접 수정 금지** |
| `Assets/StreamingAssets/Data/` | 서버(미러) | `Server/Shared/Data`에서 복사되는 `*.bytes` |
| `Assets/Scripts/` (`Network`·`Test`·`Utils`) | 서버 | Unity 측 네트워크/서버 연동 코드 |
| `Assets/Scripts_Client/` | 클라이언트 | 클라이언트 코드 |
| `Assets/Scenes/` | 공용 | 씬 파일 |

> 패킷 정의는 `Server/MikaProtocol`에서 빌드되면 post-build로 `sync-protocol-to-unity.ps1`이
> 실행되어 `Assets/Scripts/Protocol`로 단방향 미러링된다(소스 `MikaProtocol` → 대상 `Protocol`).

---

## 게임 데이터 파이프라인

엑셀 하나를 고치고 `Server/generate-tables.ps1`을 돌리면 서버·Unity 양쪽 산출물이 한 번에 갱신된다.

```
GameDesign/Excel/*.xlsx            ← 사람이 편집하는 유일한 원본
        │  [1/2] ExcelGenerator (코드 생성 + 런타임 인메모리 컴파일로 .bytes까지)
        ├─ 정의(.cs)   → Server/GameData/        ─[2/2]→ Assets/Scripts_Server/GameData/
        ├─ 데이터(.bytes) → Server/Shared/Data/   ─[2/2]→ Assets/StreamingAssets/Data/
        └─ 리뷰(.json) → GameDesign/DataLog/
```

- 서버는 `GameData` 프로젝트를 참조하고 `.bytes`를 Content로 bin에 복사받는다.
- Unity는 미러된 `.cs` + StreamingAssets의 `.bytes`를 읽는다. 양쪽 MemoryPack 와이어 포맷이 동일하다.
- 엑셀을 Excel에서 열어 둔 채로 실행하면 파일 잠금으로 즉시 실패한다. 닫고 다시 실행한다.

---

## 협업 규칙

- 각자 자기 담당 폴더(`Scripts_Client` / `Scripts`·`Server`)만 수정한다. 상대 폴더 변경은 합의 후.
- 패킷 정의는 `Server/MikaProtocol`에서만 수정한다. `Assets/Scripts/Protocol`은
  `sync-protocol-to-unity.ps1`이 덮어쓰는 사본이므로 직접 수정하지 않는다.
- 게임 시스템·콘텐츠 작업은 `GameDesign/기획/게임기획코어.md` → 해당 상세 기획안 순으로 먼저 읽는다.
  기획이 확정·변경되면 상세 기획안과 게임기획코어의 확정/미확정 현황을 함께 갱신한다.
- 게임 데이터는 `GameDesign/Excel`의 엑셀에서만 수정한다. 여긴 **공용**이라 서버·클라 모두 편집해도 된다.
  생성물(`Server/GameData`, `Server/Shared/Data`, `GameDesign/DataLog`, Unity 미러)은 직접 고치지 않는다.
- 엑셀을 수정했으면 `Server/generate-tables.ps1`을 돌려 **엑셀과 생성물을 같은 커밋에** 담는다.
  `GameDesign/DataLog/*.json`의 diff가 데이터 변경 내역 리뷰 수단이므로 함께 커밋한다.
- 커밋은 `commit-convention` 규칙을 따른다.
- Unity에서 새 스크립트·에셋을 만들면 에디터를 갱신해 `.meta`를 생성한 뒤 원본과 함께 커밋한다.
  `.meta` 누락 시 GUID·참조 충돌이 발생할 수 있다.
- `.claude/settings.local.json`은 개인 설정이라 커밋하지 않는다(`.gitignore` 처리됨).
- 코드는 한글 주석을 사용한다.
- **문서(`.md`)와 문서 폴더는 항상 한글 이름으로 만든다.** 상세는 아래 "이름 규칙" 참조.
- CLAUDE.md·스킬 문서를 수정하면 문서 상단의 `최종 업데이트` 날짜를 그날 날짜로 갱신한다.

---

## 이름 규칙 (문서·폴더)

**새 `.md` 문서나 폴더를 만들 때는 한글 이름을 쓴다.** 영문으로 만들지 않는다.

| 대상 | 규칙 | 예 |
|------|------|-----|
| 기획·설계 문서 폴더 | **한글** | `GameDesign/기획/자원채취/농사/` |
| 기획·설계 `.md` 파일 | **한글** | `요일로테이션.md`, `밸런스표.md` |
| 문서 안의 링크·경로 | 실제 한글 경로 그대로 | `[자원채취](자원채취/README.md)` 형태 |

### 예외 — 영문을 유지하는 것

이름을 바꾸면 **동작이 깨지거나 관례를 벗어나는** 대상은 영문 그대로 둔다.

| 대상 | 이유 |
|------|------|
| `GameDesign/Excel/` · `GameDesign/DataLog/` | `Server/ExcelGenerator/Program.cs`가 경로를 문자열로 직접 참조 |
| 모든 코드 폴더·소스 파일 (`Assets/`, `Server/`, `.cs` 등) | 빌드·네임스페이스·Unity 규약 |
| `.claude/` 하위 (스킬명·`SKILL.md`·`Agent` 로그) | 스킬 이름은 kebab-case 영문, 로그 파일명은 `YYYY-MM-DD-<kebab-slug>.md` |
| 관례적 파일명 (`README.md`, `CLAUDE.md`) | 표준 관례 |
| 이미 영문으로 자리 잡은 기존 문서 | 임의로 바꾸지 않는다. 바꿀 땐 참조 경로를 전부 함께 고친다 |

> 폴더명을 바꾸면 **참조하는 모든 문서의 상대 링크가 깨진다.**
> 이름을 변경했으면 링크를 전수 확인하고, `CLAUDE.md`와 관련 스킬 문서의 경로도 함께 고친다.

---

## Skills 참조

스킬은 항상 적용하는 게 아니라, **작업 내용에 따라 필요한 경우에만** 참고한다.
작업하는 폴더에 맞춰 해당 그룹과 **공용** 스킬을 함께 본다.
(클라이언트 작업 = 공용 + 클라이언트 / 서버 작업 = 공용 + 서버)

### 공용 (`common/`) — 모든 작업

| 스킬 | 경로 | 내용 |
|------|------|------|
| `game-design-reference` | [`.claude/skills/common/game-design-reference/SKILL.md`](.claude/skills/common/game-design-reference/SKILL.md) | 게임 작업 **착수 전** `게임기획코어.md` + 상세 기획안 필독 |
| `commit-convention` | [`.claude/skills/common/commit-convention/SKILL.md`](.claude/skills/common/commit-convention/SKILL.md) | Git 커밋 메시지 규칙 |
| `agent-log-reader` | [`.claude/skills/common/agent-log-reader/SKILL.md`](.claude/skills/common/agent-log-reader/SKILL.md) | 코드 작업 **착수 전** `.claude/Agent/` 로그 필독 |
| `agent-log-writer` | [`.claude/skills/common/agent-log-writer/SKILL.md`](.claude/skills/common/agent-log-writer/SKILL.md) | 코드 작업 **종료 후** `.claude/Agent/`에 로그 기록 |

### 클라이언트 (`client/`) — `Assets/Scripts_Client` 작업 시

| 스킬 | 경로 | 내용 |
|------|------|------|
| `clean-code-style` | [`.claude/skills/client/clean-code-style/SKILL.md`](.claude/skills/client/clean-code-style/SKILL.md) | Unity/C# 클린 코드 스타일 규칙 |
| `feature-design` | [`.claude/skills/client/feature-design/SKILL.md`](.claude/skills/client/feature-design/SKILL.md) | OOP·SOLID·디자인 패턴 기반 기능 설계 |
| `optimization` | [`.claude/skills/client/optimization/SKILL.md`](.claude/skills/client/optimization/SKILL.md) | 성능 최적화 판단 및 적용 가이드 |
| `unity-handoff` | [`.claude/skills/client/unity-handoff/SKILL.md`](.claude/skills/client/unity-handoff/SKILL.md) | 유니티 에디터 작업 핸드오프 프롬프트 생성 |

### 서버 (`server/`) — `Assets/Scripts` 작업 시

| 스킬 | 경로 | 내용 |
|------|------|------|
| (없음) | — | 필요 시 서버 담당이 추가 |
