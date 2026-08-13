# CLAUDE.md — 게임 기획 · 데이터

> 최종 업데이트: 2026-08-13 (루트 `CLAUDE.md`에서 기획 파트를 분리)

게임 시스템·콘텐츠·데이터에 닿는 작업에서 참고하는 문서다.
공통 규칙(환경·협업·스킬)은 저장소 루트의 [`CLAUDE.md`](../CLAUDE.md)를 함께 본다.

대상 작업: 채취·퀘스트·특성·아이템·거래·성장곡선·위젯 UI 로직, `GameDesign/Excel` 데이터 변경,
게임 데이터 테이블/패킷/DB 스키마 설계, 기획 문서 수정.
(게임 규칙과 무관한 순수 인프라 작업은 제외)

---

## ⚠️ 착수 전 필독 — 게임기획코어.md

**게임 시스템·콘텐츠에 닿는 작업은 착수 전에
[`design/게임기획코어.md`](design/게임기획코어.md)를 반드시 먼저 읽는다.**

- 이 문서가 게임의 **단일 진입점**이다. 정체성·설계 원칙(P1~P4)·코어 루프·시스템 지도·
  **확정/미확정 현황**을 담는다.
- 게임기획코어를 읽은 뒤, 해당 영역의 **상세 기획안**(`design/<시스템>/README.md`)을 이어서 읽는다.
- **게임기획코어 5장의 "미확정" 항목은 임의로 결정하지 않는다.** 필요하면 사용자에게 먼저 확인한다.
- 절차·예외·문서 갱신 규칙은 [`game-design-reference`](../.claude/skills/common/game-design-reference/SKILL.md) 스킬 참조.

---

## 폴더

| 경로 | 내용 |
|------|------|
| `design/게임기획코어.md` | **게임 기획 최상위 문서** — 게임 작업 착수 전 필독. 정체성·설계 원칙·코어 루프·시스템 지도·확정/미확정 현황 |
| `design/문서관계도.md` | **기획 문서 의존 그래프** — 무엇을 함께 읽고 함께 고치는가. 전파 규칙 + 엑셀·코드 대응표 |
| `design/` | **게임 기획 단일 진실** — 게임기획코어 + 시스템별 상세 기획안(`<시스템>/README.md`) + 1차 산업(`gathering/<산업>/README.md`) + 설계 평가(`기획평가.md`) |
| `Excel/` | **게임 데이터 단일 진실** — 기획 데이터 엑셀(`Enum.xlsx`·`Item.xlsx` …). 서버/클라 어느 쪽 폴더에도 속하지 않는 공용 입력 |
| `DataLog/` | (생성) 생성된 `.bytes`를 되읽어 덤프한 JSON. 엑셀 대조·diff 리뷰용 — **직접 수정 금지** |
| `check-doc-graph.ps1` | **문서 그래프 검사기.** 깨진 링크·헤더 블록 불일치·**갱신일 역전**(전파 누락)을 잡는다 |
| `generate-tables.ps1` | **데이터 파이프라인 실행 스크립트.** 입력(엑셀) 옆에 두어 기획자가 그 자리에서 돌린다 |
| `web/` | **문서 사이트(Astro).** `tasks/`·`GameDesign/design/`을 그대로 읽어 정적 사이트로 만든다 — 문서를 복사하지 않는다. 사용법은 [`사이트.md`](web/사이트.md) |

---

## 기획 문서는 "지금 상태"만 담는다 — 이력을 남기지 않는다

**기획이 바뀌면 이전 서술을 지우고 새 내용으로 대체한다.**
취소선(`~~…~~`)·"폐지"·"재정의됨" 같은 변경 이력을 문서에 쌓지 않는다 —
**변경 이력은 git이 갖는다.** 문서가 이력을 겸하면 지금 무엇이 유효한지 읽기 어려워진다.

- 폐지된 시스템은 **항목째 삭제한다.** 지금도 유효한 규칙이 남으면
  (예: "요일 보너스 없음") 긍정문 한 줄로 적는다.
- `게임기획코어.md` 5장 확정/미확정 표에 `❌ 폐지` 행을 쌓지 않는다.

## 기획 문서를 고쳤으면 재귀적으로 전파한다

**기획 문서는 서로 촘촘히 물려 있다**(17개 문서 · 참조 113개).
한 문서만 고치고 끝내면 상위·형제 문서가 낡은 서술로 남는다 —
실제로 `Hunting` enum·아이템 종수·`ItemRarity` 개명이 이렇게 새어 나갔다(2026-08-02 확인).

1. 고친 문서 헤더의 **`> **바뀌면 갱신:**`** 블록에 적힌 문서를 **전부 열어 본다.**
2. 고친 문서가 생기면 **그 문서의 블록을 따라 또 퍼진다** (순환은 방문 표시로 멈춘다).
3. 전체 그래프·전파 규칙·엑셀/코드 대응표는
   [`design/문서관계도.md`](design/문서관계도.md)에 있다.
4. 커밋 전에 검사한다:

```powershell
powershell -File GameDesign/check-doc-graph.ps1 -Changed
```

> **엑셀만 앞서 나가면 "미구현"이 아니라 "오동작"이 된다.** 엑셀 구조를 바꿀 때는
> 그 시트를 읽는 서버 코드를 **같은 작업 단위로** 본다. 코드가 못 따라가면
> `tasks/`에 등록하고 문서에 `❌ 미구현 (일감 T-0XX)`를 적는다.

---

## 게임 데이터 파이프라인

엑셀 하나를 고치고 `GameDesign/generate-tables.ps1`을 돌리면 서버·Unity 양쪽 산출물이 한 번에 갱신된다.

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
- **경로는 전부 저장소 루트에서 유도한다.** 어디에 체크아웃하든 동작하도록 절대경로를 박지 않는다.
  ps1은 `$RepoRoot`/`$ServerRoot`/`$UnityRoot`에서, `ExcelGenerator`는 `Program.cs` 상단의
  루트 상대 상수(`ExcelDirRel` 등)에서 조합한다. **프로젝트 폴더 상대(`../GameData`)로 두지 않는다** —
  프로젝트를 옮기면 컴파일은 통과하면서 엉뚱한 위치에 파일을 쓴다.
- **시트 이름 `<테이블명>.<접미사>`는 병합 규약이다** — 베이스 이름이 같은 시트들이
  한 테이블로 합쳐진다(예: `FishingBasicTable.Lv1`~`.Lv5` → `FishingBasicTable`).
  상세는 [`excel-table-creator`](../.claude/skills/common/excel-table-creator/SKILL.md) 스킬 참조.
- **시트를 지우면 그 테이블의 `.bytes`·`.json`도 자동 삭제된다**(Unity 미러까지 전파).
  생성물을 손으로 지울 필요가 없다.

### 엑셀 마커 행 (A열)

| 마커 | 의미 |
|------|------|
| `Type` | `int`·`long`·`float`·`string`·`bool`·`ID`·`eEnum` (+ `[]` 배열은 `,` 구분) |
| `Min`·`Max` | 값 범위 |
| `Default(Null)` | 빈 셀일 때 쓸 값. **비워 두면 "빈 셀 = 오류"**(fail-fast) |
| `Ref` | `대상시트.대상컬럼` — 값이 실재하는지 검사. `?`를 붙이면 빈 셀·`0` 허용 |

> `Default(Null)`에 **`""`(따옴표 두 개)** 를 적으면 빈 문자열이 기본값이 된다.
> 설명·비고처럼 비워 두는 게 정상인 string 컬럼에 쓴다. 이 표기가 없으면 빈 셀은 오류다.

### 예약 컬럼 — `Description`

**`Description` 컬럼은 게임 로직에 아무 영향을 주지 않는다.** 기획자가 시트에 남기는 메모다.

- 값을 바꿔도, 통째로 비워도 **동작이 달라지지 않는다.** 서버·클라 어느 쪽도 읽지 않는다.
- 그래서 `Default(Null)`에 `""`를 지정해 **빈 셀을 정상으로 둔다.**
- 생성된 Row 클래스에 `[기획 메모 — 로직에서 읽지 않는다]` 주석이 자동으로 붙는다.

> ⚠️ **로직에 쓰는 수치를 `Description`에 적지 않는다.** 확률·배수 같은 값을 메모로 적어 두면
> 실제 컬럼(`Weight` 등)과 따로 놀다가 조용히 어긋난다. 계산에 쓸 값은 반드시 자기 컬럼을 갖는다.

---

## 작업 규칙

- 게임 시스템·콘텐츠 작업은 `design/게임기획코어.md` → 해당 상세 기획안 순으로 먼저 읽는다.
  기획이 확정·변경되면 상세 기획안과 게임기획코어의 확정/미확정 현황을 함께 갱신하고,
  **`문서관계도.md`의 역참조를 따라 재귀적으로 전파한 뒤 `check-doc-graph.ps1 -Changed`로 검사한다.**
- 게임 데이터는 `GameDesign/Excel`의 엑셀에서만 수정한다. 여긴 **공용**이라 서버·클라 모두 편집해도 된다.
  생성물(`Server/GameData`, `Server/Shared/Data`, `GameDesign/DataLog`, Unity 미러)은 직접 고치지 않는다.
- 엑셀을 수정했으면 `GameDesign/generate-tables.ps1`을 돌려 **엑셀과 생성물을 같은 커밋에** 담는다.
  `GameDesign/DataLog/*.json`의 diff가 데이터 변경 내역 리뷰 수단이므로 함께 커밋한다.

---

## 이름 규칙 (문서·폴더)

**폴더는 영문, 문서(`.md`) 파일명은 한글이다.** (2026-08-08 변경 — 이전 규칙은 폴더도 한글이었다)

| 대상 | 규칙 | 예 |
|------|------|-----|
| **모든 폴더** | **영문 소문자 · kebab-case** | `GameDesign/design/gathering/farming/`, `tasks/archive/` |
| 기획·설계 `.md` 파일 | **한글** | `게임기획코어.md`, `산업레벨.md`, `요일로테이션.md` |
| 일감 파일 | `T-<3자리>-<한글슬러그>.md` | `T-005-드롭테이블.md` |
| **엑셀 파일 (`.xlsx`)** | **영문 PascalCase** (2026-08-09 변경 — `Drop낚시` 등 한글 5개를 개명) | `Item.xlsx`, `DropFishing.xlsx` |
| 문서 안의 링크·경로 | 실제 경로 그대로 (영문 폴더 + 한글 파일) | `[자원채취](gathering/README.md)` |

**폴더를 영문으로 되돌린 이유:** 문서를 웹으로 배포할 때 한글 폴더가
percent-encoding(`진행%20및%20성장`)으로 깨지고, 공백이 섞이면 URL·스크립트 양쪽에서 사고가 난다.
파일명은 사람이 목록에서 찾는 이름이라 한글을 유지한다.

### 폴더 이름 대응 (2026-08-08 개명)

| 옛 이름 | 새 이름 | | 옛 이름 | 새 이름 |
|---------|---------|---|---------|---------|
| `일감/` | `tasks/` | | `자원채취/` | `gathering/` |
| `일감/보관/` | `tasks/archive/` | | `낚시/` `농사/` `벌목/` | `fishing/` `farming/` `logging/` |
| `문서/` | `docs/` | | `사냥/` `채굴/` | `hunting/` `mining/` |
| `Server/문서/` | `Server/docs/` | | `작업슬롯/` | `workslot/` |
| `GameDesign/기획/` | `GameDesign/design/` | | `진행 및 성장/` | `progression/` |
| `아키텍처리뷰/` | `architecture-review/` | | `캐릭터/` `퀘스트/` `특성/` | `character/` `quest/` `trait/` |
| `거래/` `아이템/` | `trade/` `item/` | | `게임UI/` (+`참고 사진/`) | `ui/` (+`references/`) |
| `리서치/` `방향제안/` | `research/` `proposals/` | | | |

### 그대로 두는 것

| 대상 | 이유 |
|------|------|
| `GameDesign/Excel/` · `GameDesign/DataLog/` | `Server/ExcelGenerator/Program.cs`가 경로를 문자열로 직접 참조 |
| 모든 코드 폴더·소스 파일 (`Assets/`, `Server/`, `.cs` 등) | 빌드·네임스페이스·Unity 규약 |
| `.claude/` 하위 (스킬명·`SKILL.md`·`Agent` 로그) | 스킬 이름은 kebab-case 영문, 로그 파일명은 `YYYY-MM-DD-<kebab-slug>.md` |
| 관례적 파일명 (`README.md`, `CLAUDE.md`) | 표준 관례 |
| **한글 `.md` 파일명** | 개명 대상이 아니다. 문서는 계속 한글로 만든다 |

> 폴더명을 바꾸면 **참조하는 모든 문서의 상대 링크가 깨진다.**
> 이름을 변경했으면 링크를 전수 확인하고(`check-doc-graph.ps1`), `CLAUDE.md`와 관련 스킬 문서의
> 경로도 함께 고친다. 검사기의 `$DesignRoot`·`$ExcludeDirs`처럼 **폴더 이름이 코드에 박힌 곳**도 본다.

---

## 관련 스킬

| 스킬 | 경로 | 내용 |
|------|------|------|
| `game-design-reference` | [`.claude/skills/common/game-design-reference/SKILL.md`](../.claude/skills/common/game-design-reference/SKILL.md) | 게임 작업 **착수 전** `게임기획코어.md` + 상세 기획안 필독 |
| `excel-table-creator` | [`.claude/skills/common/excel-table-creator/SKILL.md`](../.claude/skills/common/excel-table-creator/SKILL.md) | 게임 데이터 엑셀 시트·컬럼 작성 규칙 (TID 필수·마커 행·`Ref`) |
