---
date: 2026-08-12
title: nullable 참조 형식 규칙을 Arca Unity Toolkit 표준으로 승격
tags: [client, toolkit, nullable, convention]
---

# nullable 참조 형식 규칙을 Arca Unity Toolkit 표준으로 승격

## 목적 / 배경

`?`·`!` 문법 질문에서 시작해 클라 40개 파일을 점검했다. 규칙(`= null!` + `RequireRef`)은 거의
완벽히 지켜지고 있었지만 **두 곳에 구멍**이 있었다.

1. 프로토콜 이탈 2건 — `InventorySlotView`·`WorkStationSlotView`가 `= null!`만 있고 `RequireRef`가 없다
2. **툴킷 마스터에 규칙이 없다** — `RequireRef` *코드*만 있고 "언제 쓰는지"가 어디에도 없었다

## 변경 내용

**프로젝트**
- `UI/Storage/.../InventorySlotView.cs` · `UI/Main/.../WorkStationSlotView.cs` — `Awake()` 신설 + `RequireRef`
- `UI/UI 규칙.md` — 종속 View 규약 예시에 `Awake` + `RequireRef` 추가, 규칙 표에 2행 추가

**툴킷 마스터** (`~/.claude/skills`, 커밋 `5cc0414`)
- `clean-code-style/SKILL.md` — **9장 「nullable 참조 형식」 신설** + 참조 타입 예시를 `null!`/`?` 기준으로 갱신
- `unity-project-setup/SKILL.md` — 3단계에 `Assets/csc.rsp`(`-nullable:enable`) 생성 추가
- `optimization/SKILL.md` · `templates/code/README.md` · `CLAUDE.md.template` 반영

## 주요 결정 / 근거

### `?`를 기본값으로 삼지 않는다 — 미연결이 조용해진다

"전부 `?`로 두면 안전하지 않나"가 자연스러운 오해다. **`?`는 안전장치가 아니라
"비어도 정상"이라는 선언**이고 런타임 효과가 0이다. 필수 참조를 `?`로 두면 컴파일러가
`if (x != null)`을 요구하고, 그 결과 **미연결이 아무 경고 없이 조용히 무시된다.**
실제 안전은 `RequireRef`의 예외가 만든다.

### 종속 View는 `Awake`, Presenter는 `Start`

View는 상위 Presenter의 `Start`가 `Bind`를 부르기 전에 검증이 끝나 있어야 한다.
서비스를 조회하지 않으므로 `Awake`로 충분하다. (`CharacterStateRowView`가 이미 그렇게 하고 있었고,
빠진 두 파일만 맞췄다)

### 🔴 툴킷에 `csc.rsp` 생성 단계가 없던 것이 실제 결함이었다

툴킷 전체에 `nullable` 언급이 **0건**이었는데, `MonoBehaviourExtensions.cs`는 이미 `Object?`를 쓴다.
→ 새 프로젝트에 심으면 **CS8632 경고**가 뜬다. 규칙 문서만 추가하면 이 구멍이 남으므로
세팅 절차에 `csc.rsp` 생성을 넣었다.

## 후속 작업 / 주의사항

- ⚠️ **Unity 객체에 `?.`·`??`를 쓰지 않는다** — 파괴된 오브젝트의 "가짜 null"을 통과시킨다.
  현재 클라 사용처 14건은 전부 순수 C#(이벤트·컬렉션·DTO)이라 안전하다. 새로 추가할 때 주의
- `WidgetPositionLayout`은 `null!` 7개에 `RequireRef`가 없지만 **의도적 예외**다 —
  `HasAllReferences()`로 자체 검사하고, `[ExecuteAlways]`라 배선 전 예외를 던지면 에디터가 시끄러워진다
- `_network = NetworkManager.Instance`는 `Services.Get`이 아니지만 문제없다 —
  `SingletonMonoBehaviour.Instance`는 없으면 오브젝트를 만들어 반환하므로 null이 될 수 없다
- 규칙 원문 → `.claude/skills/client/clean-code-style/SKILL.md` 9장
