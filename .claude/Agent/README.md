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

- [2026-08-01 게임 UI 캔버스 골격 확정 — 16:9 배율 창 · 3열 정렬 · 렌더 모드](2026-08-01-ui-canvas-skeleton.md) — 캔버스 4→3개(위젯은 패널) · **LayoutGroup은 root Canvas를 못 움직인다 → nested Canvas** · Overlay→Screen Space-Camera · `WindowManager` 1:2 세로 → 16:9 절대 배율
- [2026-08-01 mattpocock-skills 각색 — 프로젝트 스킬 갱신 2건 + server-tdd 신설](2026-08-01-skill-updates-from-mattpocock.md) — task-writer(일감 쪼개기 절차) · agent-log-writer(압축 기준) · **server-tdd 신설** · CLAUDE.md 서버 스킬 표 정정
- [2026-08-01 산업 레벨 시스템 기획 — 배치가 (산업, 레벨, 캐릭터) 세 칸이 된다](2026-08-01-industry-level.md) — 경험치 없이 조건 해금 · 효과는 드롭 테이블 확장 · **낚시터·사냥터·수종이 `IndustryLevel`로 통합** · 해금 조건은 미정(T-016)
- [2026-07-30 보상 구조에서 특별보상 층(9:1) 폐지](2026-07-30-special-reward-removal.md) — 판정 1회 = 희귀도 롤 한 번 · 문서 9개 갱신 · **보스 티켓 경로와 낚시 차별화가 미정으로 남음**
- [2026-07-30 작업속도 계산을 가산/승산 분류 합성으로 재구성](2026-07-30-workspeed-add-mul.md) — `적성 × (1 + Σ가산) × Π승산` · `WorkSpeed` 누산기 신설 · **EquipSlot 부위 확정(무기1·장신구2·보석1)**
- [2026-07-29 작업슬롯 패킷 4종 Unity 클라 연동](2026-07-29-workstation-packet-client.md) — 배치/해제 송신 + 30초 채취 푸시 수신 확인 · `MonoService` null 등록 방지(CRTP 대신 런타임 검사)
- [2026-07-29 MIKA001 경고가 Unity 콘솔에 뜨지 않던 문제 수정](2026-07-29-mika001-unity-visibility.md) — 진단의 `Location.None`이 원인(Unity는 위치 없는 경고를 파싱 못 함) · 분석기 DLL 자동 동기화 추가
- [2026-07-29 작업슬롯 서버 구현 — 시각 기반 채취 정산과 30초 푸시](2026-07-29-workstation-slot-impl.md) — `t_workstation_slot` · 패킷 4종 · `LastTickAt` 정산 · 스케줄러 · 테스트 18건
- [2026-07-29 작업슬롯 구조 전환 — 산업 택1·요일 로테이션 폐지](2026-07-29-workstation-slot-design.md) — 슬롯당 캐릭터 1명 · 서버 권위 + 30초 푸시 · **30fps 루프 반대 근거** · P3 근거 상실
- [2026-07-29 가중치 추첨기 WeightedPicker 도입](2026-07-29-weighted-picker.md) — 드롭·희귀도·가챠 공용 추첨기(누적합 + 이진 탐색) · 그룹 인덱스 헬퍼 · 경계 검증 테스트 19건
- [2026-07-29 서버 프레임워크 프로젝트를 MikaNetwork.Lib로 묶어 폴더 구조 정리](2026-07-29-server-folder-restructure.md) — 프레임워크 5개 이동 + MikaSourceGen 이중 폴더 해소 · 게임 코드는 위치 유지(경로 하드코딩)
- [2026-07-29 데이터 파이프라인 스크립트를 GameDesign으로 이전하고 서버 테스트 프로젝트 신설](2026-07-29-pipeline-move-and-tests.md) — `generate-tables.ps1` 이동 + 절대경로 제거 · `WSGameServer.Tests`(xUnit·Shouldly·Moq) 신설
- [2026-07-28 낚시 드롭 테이블 시트 생성 및 ItemTID 참조 무결성 검사 도입](2026-07-28-drop-table-ref-check.md) — Drop.xlsx(낚시 Basic/Special) + `Ref` 마커 기반 참조 검사 · 드롭 시트는 `DropTID(ID)` 선두 규칙
- [2026-07-27 ExcelGenerator 생성 코드를 C# 9로 수정](2026-07-27-csharp9-codegen-fix.md) — 이슈 #6 대응 · 블록 네임스페이스 + 파이프라인 C# 9 규약 검사 추가
- [2026-07-27 낚시 기획 확정 및 요일 로테이션 구조 전환](2026-07-27-fishing-design-lock.md) — 낚시 논점 확정 + 요일 효율 보너스 전환 + **채취주기 30초 통일·개체 변량 폐기**(2차 업데이트)
- [2026-07-26 GameDesignCore 도입 및 시스템별 상세 기획안 구축](2026-07-26-game-design-core.md) — 게임 기획 단일 진입점 + 상세안 7종 + 평가 문서 + 강제 참조 스킬
