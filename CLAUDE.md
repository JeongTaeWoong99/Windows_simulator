# CLAUDE.md

이 문서는 Claude Code로 작업할 때 공통으로 유의·협의해야 할 내용을 정리한 가이드다.
데스크톱 위에서 동작하는 투명 창(데스크톱 윈도우 제어)과 네트워크 기능을 결합하는 프로젝트로,
클라이언트와 서버를 한 저장소에서 영역을 나눠 협업한다.

---

## 환경

| 항목 | 내용 |
|------|------|
| Unity 버전 | 6000.3.10f1 |
| 렌더 파이프라인 | Built-in |

---

## 폴더 구조

| 경로 | 담당 | 내용 |
|------|------|------|
| `Assets/Scripts_Client/` | 클라이언트 | 클라이언트 코드 |
| `Assets/Scripts/` | 서버 | 서버 / 네트워크 코드 |
| `Assets/Scenes/` | 공용 | 씬 파일 |

---

## 협업 규칙

- 각자 자기 담당 폴더(`Scripts_Client` / `Scripts`)만 수정한다. 상대 폴더 변경은 합의 후.
- 커밋은 `commit-convention` 규칙을 따른다.
- `.claude/settings.local.json`은 개인 설정이라 커밋하지 않는다(`.gitignore` 처리됨).
- 코드는 한글 주석을 사용한다.

---

## Skills 참조

스킬은 항상 적용하는 게 아니라, **작업 내용에 따라 필요한 경우에만** 참고한다.
작업하는 폴더에 맞춰 해당 그룹과 **공용** 스킬을 함께 본다.
(클라이언트 작업 = 공용 + 클라이언트 / 서버 작업 = 공용 + 서버)

### 공용 (`common/`) — 모든 작업

| 스킬 | 경로 | 내용 |
|------|------|------|
| `commit-convention` | [`.claude/skills/common/commit-convention/SKILL.md`](.claude/skills/common/commit-convention/SKILL.md) | Git 커밋 메시지 규칙 |

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
