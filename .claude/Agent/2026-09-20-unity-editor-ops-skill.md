---
date: 2026-09-20
title: unity-handoff → unity-editor-ops 개편 · 에디터 조작 경로 확정
tags: [docs, infra, editor]
---

# unity-editor-ops — 에디터 조작을 직접 하는 전제로 갈아엎음

## 목적 / 배경

에디터 도구를 매번 껐다 켰다 하지 않고 **항상 켜둔 채로** 터미널에서 구상 →
계획 → 에디터 작업까지 이어서 하되, **미리 정해 둔 지점에서만** 확인을 받는
워크플로우로 바꾸고 싶다는 요청. 기존 `unity-handoff`(프롬프트를 뽑아 사람이
유니티 어시스턴트에 붙여넣는 방식)는 이 전제와 맞지 않아 다시 썼다.

→ 변경 내역은 커밋 `14526d6`(프로젝트) · 툴킷 `47d9824`

## 주요 결정 / 근거

**Unity CLI는 이미 붙어 있었다.** `com.unity.pipeline`이 에디터 안에서 로컬
HTTP 서버를 띄우고 `unity` 바이너리가 붙는 구조로, 설치할 것이 없었다.
도구가 "꺼져 있던" 것도 아니었다 — `deny` 목록 자체가 없었고 단지
allow에 없었을 뿐이다.

**⚠️ auto 모드에서 사용자에게 확인을 받는 수단은 `deny` 하나뿐이다** (실측,
Claude Code 2.1.278). 아래 셋은 **전부 도달하지 않는다** — 규칙은 적용되지만
프롬프트가 뜨지 않고 그대로 실행된다.

| 수단 | 결과 |
|------|------|
| `permissions.ask` | 도달 안 함 (user/project local 둘 다) |
| PreToolUse 훅의 `permissionDecision: "ask"` | 훅은 발화하고 JSON도 맞는데 도달 안 함 (센티널 로그로 확인) |
| `autoMode.soft_deny` | 도달 안 함 (재시작 후에도) |
| `permissions.deny` | **즉시 작동** — 규칙을 쓰는 순간 도구 목록에서 사라진다 |

처음에는 **B-1안**으로 AI 에셋 생성만 `deny`에 넣었다가, **뺐다** —
`deny`는 "물어보고 하기"가 아니라 "아예 못 하기"라서 사용자가 맡기고 싶어 한
작업을 통째로 막았다. 지금은 `deny`가 비어 있고, **전부 스킬 문서의 행동
규칙으로 지킨다**(git이 안전망). 스킬 2절에 "설정으로 강제되지 않는다"를
명시한 이유다.

## 후속 작업 / 주의사항

- **모델은 자기 권한 규칙을 지울 수 없다** — `deny`·`allow` 줄을 빼려 하면
  분류기가 `[Self-Modification]`으로 막는다. **사용자가 직접 설정 파일을
  고쳐야 한다.** 스킬에 그렇게 적어 뒀다.
- **`Bash(unity *)`를 allow에 두면 auto 모드 분류기를 통째로 우회한다.**
  `unity cmd delete_asset` 같은 것도 무검사로 지나간다. 지금은 뺐다 —
  다시 넣지 말 것.
- **AI 에셋 생성이 안 되는 진짜 이유는 `NoSubscription`이다** — Unity AI
  크레딧이 계정에 없다. 모델 목록이 비면 **패키지 탓으로 넘겨짚지 말고
  콘솔을 먼저 읽는다.** 이 프로젝트는 AI 패키지가 다 깔려 있었는데도
  내가 "패키지가 없다"고 단정해서 오진했다 (→ 스킬 3절에 주의로 박아 뒀다).
- ⛔ **`com.unity.ai.generators`를 다시 깔지 않는다.** deprecated이고 기능은
  `com.unity.ai.assistant`에 전부 통합됐다. 둘이 함께 있으면 어셈블리
  (`Unity.AI.Generators.IO.Srp`)와 GUID가 충돌해 **프로젝트 전체 컴파일이
  깨진다** — 실제로 콘솔 에러 1,538건에 `compilationFailed: true`였다.
  `package_remove`로 빼자 **에러 0건**이 됐다.
- Unity 공식 `unity-cli` 스킬은 `unity skill install claude-code`로 깔린다.
  툴킷 저장소(`~/.claude/skills`)에는 `.gitignore`로 제외했다 — Unity 소유에
  CLI 버전에 딸린 것이라 마스터에 섞지 않는다.
- 마스터 판은 Unity CLI가 없는 프로젝트도 쓰도록 1·2절에 분기를 뒀다.
  프로젝트 사본의 `deny` 대상 도구 이름·패키지 사정은 **의도적으로 중립화**했으니
  다음 sync에서 차이로 잡혀도 push하지 않는다.
