# Game research Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** wsdocs에 게임별 재미 연구를 제공하고 매일 KST 06·12·18시 자동 게시한다.
**Architecture:** Astro의 기존 목록을 확장한다. Node 게시기가 읽기 전용 Codex 웹 조사 결과를 검증하고 독립 worktree에서 문서만 게시한다. Windows 작업 스케줄러는 Node 게시기만 실행한다.
**Tech Stack:** Astro 5, Node 22, Codex CLI, Git, PowerShell ScheduledTasks.
**Spec:** docs/superpowers/specs/2026-11-17-game-research.md

## Global Constraints
- 사용자 미추적 .agents/·AGENTS.md와 개발 DB를 보존한다.
- 기존 /proposals URL·지난 레퍼런스를 유지한다.
- KST 06:00, 12:00, 18:00. 예약은 실행 환경이 갖춰진 이 PC에 등록한다.

## Review Focus
- 게임별 연구와 기존 제안을 혼동하지 않는다.
- 공식 기능과 플레이어 증언·작성자 해석을 구분한다.
- 예약 중복·PC 꺼짐·CLI 실패를 성공으로 표시하지 않는다.
- 잘못된 slug·빈 본문·출처 없는 결과를 게시하지 않는다.
- 게시 시 기존 staged 변경과 현재 브랜치를 보존한다.

### Task 1: 연구 목록과 초기 문서
- [x] 연구 3편 수집·작성, 학습용 구조와 출처 확인
- [x] Layout·목록·상세 이름을 기획 (AI)로 변경하고 두 문서 종류 표시
- [x] Astro check/build와 생성 HTML 확인

### Task 2: 안전한 정기 조사·게시
- [x] 시간대·출처·문서 검증·중복의 실패 테스트 작성
- [x] Node 실행기, JSON 출력 규격, 연구 프롬프트 구현
- [x] 임시 Git 저장소로 문서 단독 커밋·게시와 원래 작업 보존 검증
- [x] 운영 절차와 장애 복구 문서 작성

### Task 3: 예약·배포·인계
- [x] 테스트 통과 후 커밋·main 반영·push 및 wsdocs 확인
- [x] Windows 매일 06·12·18시 작업 등록, 설정과 다음 실행 확인
- [x] 최초 예약 실행의 실제 경로 검증, 일감·로그·공돌리기 기록
