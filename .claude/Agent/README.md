# Agent 작업 로그 (.claude/Agent)

이 폴더는 **코드 작업의 결정·맥락을 다음 작업(사람/Agent)이 참고**하도록 남기는 로그 저장소다.

- 새 코드 작업을 **시작하기 전** → `agent-log-reader` 스킬에 따라 관련 로그를 먼저 읽는다.
- 코드 작업을 **마친 뒤** → `agent-log-writer` 스킬에 따라 이 폴더에 로그(.md)를 남긴다.

## 파일 규칙

- 파일명: `YYYY-MM-DD-<kebab-slug>.md` (예: `2026-07-25-excelgenerator-bytes.md`)
- 하나의 작업 단위 = 하나의 로그 파일
- 아래 `INDEX`에 한 줄씩 최신순으로 추가한다.

## INDEX

<!-- 최신 작업이 위로. 형식: - [YYYY-MM-DD 제목](파일명.md) — 한 줄 요약 -->

- [2026-07-26 GameDesignCore 도입 및 시스템별 상세 기획안 구축](2026-07-26-game-design-core.md) — 게임 기획 단일 진입점 + 상세안 7종 + 평가 문서 + 강제 참조 스킬
