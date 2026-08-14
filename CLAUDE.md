# CLAUDE.md

> 최종 업데이트: 2026-08-14 (문서·스킬 트리거를 "무조건 읽기"에서 "필요할 때 읽기"로 개정)

이 문서는 Claude Code로 작업할 때 공통으로 유의·협의해야 할 내용을 정리한 가이드다.
데스크톱 위에서 동작하는 투명 창(데스크톱 윈도우 제어)과 네트워크 기능을 결합하는 프로젝트로,
클라이언트와 서버를 한 저장소에서 영역을 나눠 협업한다.

## 영역별 진입점

**이 표는 지도다** — 전부 읽는 곳이 아니라, 지금 만지는 영역의 문을 찾는 곳이다.
해당 영역에 손댈 때 그 문서를 연다.

| 작업 영역 | 진입점 |
|-----------|--------|
| **게임 시스템·콘텐츠·데이터** (채취·퀘스트·특성·아이템·거래·성장곡선 로직, 엑셀 변경, 테이블/패킷/DB 스키마 설계, 기획 문서 수정) | [`GameDesign/CLAUDE.md`](GameDesign/CLAUDE.md) — 게임 **규칙·수치가 바뀔 때** [`게임기획코어.md`](GameDesign/design/게임기획코어.md)를 먼저 읽는다 |
| **서버** (`Server/`, `Assets/Scripts_Server/`) | [`Server/CLAUDE.md`](Server/CLAUDE.md) |
| **클라이언트** (`Assets/Scripts_Client/`) | [`Assets/Scripts_Client/폴더 구조.md`](Assets/Scripts_Client/폴더%20구조.md) — 폴더마다 `<폴더명> 규칙.md`가 있다 |

---

## 환경

| 항목 | 내용 |
|------|------|
| Unity 버전 | 6000.3.10f1 |
| 렌더 파이프라인 | Built-in |
| 서버 런타임 | .NET 10 (WSGameServer) — 상세는 [`Server/CLAUDE.md`](Server/CLAUDE.md) |
| DB | SQLite (`Server/Shared/game.sqlite3`) |

---

## 폴더 구조

| 경로 | 담당 | 내용 |
|------|------|------|
| `tasks/` | 공용 | **할 일의 단일 목록** — 일감 1개 = 파일 1개, `README.md`가 현황 표. 담당(클라/서버/공용)·상태·우선순위·마감·관련 커밋 |
| `tasks/archive/` | 공용 | **끝난 일감** — 완료되면 여기로 내려간다. INDEX에는 굴러가는 것만 남는다. 평소 탐색하지 않는다 |
| `docs/` | 공용 | **개발 인프라 문서** — 서버·클라 양쪽에 걸치는 것. 현재 [`CI.md`](docs/CI.md) |
| `.github/workflows/` | 공용 | GitHub Actions — 서버 CI · 클라 CI · Unity 라이선스 활성화 |
| `GameDesign/` | 공용 | **게임 기획·데이터 단일 진실** — 기획 문서(`design/`)·엑셀(`Excel/`)·파이프라인 스크립트·문서 사이트(`web/`). 상세는 [`GameDesign/CLAUDE.md`](GameDesign/CLAUDE.md) |
| `Server/` | 서버 | **서버 단일 진실** — .NET 솔루션(MikaNetwork 모듈 + WSGameServer). 상세는 [`Server/CLAUDE.md`](Server/CLAUDE.md) |
| `Assets/Scripts_Server/` | 서버 | Unity 측 네트워크/서버 연동 코드 + `Server/`에서 자동 복사되는 미러(`Protocol`·`GameData`) |
| `Assets/Scripts_Client/` | 클라이언트 | 클라이언트 코드. **폴더 구성·클라 코딩 규약은 [`폴더 구조.md`](Assets/Scripts_Client/폴더%20구조.md)** |
| `Assets/Scripts_Client/Common/` | 클라이언트 | **Arca Unity Toolkit의 사본** — 게임을 모르는 범용 코드. 마스터는 `~/.claude/skills`(저장소)이고, 여기서 고쳤으면 `/unity-skill-sync`로 되돌린다. 특정 프로젝트 이름을 주석에 남기지 않는다 |
| `Assets/Scenes/` | 공용 | 씬 파일 |

> 서버 → Unity 미러링(패킷 정의·GameData·`.bytes`·Roslyn 분석기)과
> 엑셀 → 서버/Unity 데이터 파이프라인은 각각 [`Server/CLAUDE.md`](Server/CLAUDE.md)·[`GameDesign/CLAUDE.md`](GameDesign/CLAUDE.md)에 있다.

---

## 협업 규칙

- 각자 자기 담당 폴더(`Scripts_Client` / `Scripts_Server`·`Server`)만 수정한다. 상대 폴더 변경은 합의 후.
- **미러·생성물은 직접 수정하지 않는다** — `Assets/Scripts_Server/Protocol`·`GameData`,
  `Server/GameData`, `Server/Shared/Data`, `Assets/StreamingAssets/Data`, `GameDesign/DataLog`.
  원본(`Server/MikaProtocol`, `GameDesign/Excel`)에서 고치고 파이프라인을 돌린다.
- 커밋은 `commit-convention` 규칙을 따른다.
- Unity에서 새 스크립트·에셋을 만들면 에디터를 갱신해 `.meta`를 생성한 뒤 원본과 함께 커밋한다.
  `.meta` 누락 시 GUID·참조 충돌이 발생할 수 있다.
- `.claude/settings.local.json`은 개인 설정이라 커밋하지 않는다(`.gitignore` 처리됨).
- 코드는 한글 주석을 사용한다.
- **폴더는 영문 소문자 kebab-case, 문서(`.md`) 파일명은 한글이다.**
  상세 규칙·개명 대응표는 [`GameDesign/CLAUDE.md`](GameDesign/CLAUDE.md#이름-규칙-문서폴더).
- CLAUDE.md·스킬 문서를 수정하면 문서 상단의 `최종 업데이트` 날짜를 그날 날짜로 갱신한다.
- **문서·스킬에 "필독·반드시·항상·모든"을 쓸 때는 그것이 정말 예외 없는지 확인한다.**
  조건이 있으면 조건을 적고, **읽지 않아도 되는 경우를 함께 명시한다.**
  강한 단어 한 줄이 "필요할 때만"이라는 완화 문장을 덮어써서, 매 작업이 문서를 통째로
  읽는 비용을 물게 된다. 특히 스킬 `description`은 발동 조건 자체이므로 더 엄격히 본다.
- **사용자가 특별한 요구를 하지 않는 한, 간단하고 명료하게 설명한다.**
  물은 것에 답하고 끝낸다. 배경·대안·파생 논점을 묻지 않았는데 늘어놓지 않는다.
  중요한 위험이나 결정 사항이 있으면 **한두 줄로 짚고** 넘어간다.

---

## Skills 참조

**스킬은 항상 적용하는 게 아니라, 아래 "언제" 칸에 해당할 때만 연다.**
이 표는 무엇을 담았는지가 아니라 **언제 발동하는지**의 명세다.
작업하는 폴더에 맞춰 해당 그룹과 **공용** 스킬을 함께 본다.
(클라이언트 작업 = 공용 + 클라이언트 / 서버 작업 = 공용 + 서버)

### 공용 (`common/`)

| 스킬 | 언제 | 무엇을 |
|------|------|--------|
| [`agent-log-writer`](.claude/skills/common/agent-log-writer/SKILL.md) | **코드 작업 종료 후 — 조건 없이 항상** | `.claude/Agent/`에 로그 기록 |
| [`agent-log-reader`](.claude/skills/common/agent-log-reader/SKILL.md) | 기존 결정을 뒤집을 때 · 원인 불명 버그 · "예전에 왜?" 의문이 생겼을 때 · 미완 작업을 이어받을 때 — **일반 작업엔 열지 않는다** | `.claude/Agent/`에서 과거 경위 확인 |
| [`game-design-reference`](.claude/skills/common/game-design-reference/SKILL.md) | 게임 **규칙·수치가 바뀔 때** — 화면 배치·배선만 바꾸면 해당 없음 | `게임기획코어.md` + 상세 기획안 |
| [`excel-table-creator`](.claude/skills/common/excel-table-creator/SKILL.md) | `GameDesign/Excel/*.xlsx` 시트·컬럼을 만들거나 고칠 때 | TID 필수·마커 행·`Ref` 규칙 |
| [`commit-convention`](.claude/skills/common/commit-convention/SKILL.md) | 커밋할 때 | Git 커밋 메시지 규칙 |
| [`task-reader`](.claude/skills/common/task-reader/SKILL.md) | 사용자가 할 일을 묻거나 `T-0XX`를 언급할 때 · 일감 상태 갱신 전 | `tasks/` 현황 확인 |
| [`task-writer`](.claude/skills/common/task-writer/SKILL.md) | 할 일이 새로 생겼을 때 · 작업을 시작/완료했을 때 | `tasks/` 등록·갱신 |

### 클라이언트 (`client/`) — `Assets/Scripts_Client` 작업 시

| 스킬 | 언제 | 무엇을 |
|------|------|--------|
| [`clean-code-style`](.claude/skills/client/clean-code-style/SKILL.md) | C# 코드를 쓰거나 리뷰할 때 | 명명·포맷·주석 스타일 규칙 |
| [`feature-design`](.claude/skills/client/feature-design/SKILL.md) | 새 기능·클래스·시스템을 **설계**할 때 | OOP·SOLID·디자인 패턴 |
| [`optimization`](.claude/skills/client/optimization/SKILL.md) | 성능 문제가 **실제로 관측됐을 때** | 최적화 판단·적용 가이드 |
| [`unity-handoff`](.claude/skills/client/unity-handoff/SKILL.md) | 답변 후 에디터 수작업이 남을 때 | 유니티 어시스턴트용 프롬프트 생성 |

### 서버 — `Server/.claude/skills/`

`packet-creator`·`sqlite-sql-creator`·`server-tdd`·`server-code-style`.
디렉터리 스코프라 `Server/` 아래 파일을 다룰 때 적용된다. 상세는 [`Server/CLAUDE.md`](Server/CLAUDE.md#서버-스킬).
