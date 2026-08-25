---
date: 2026-08-25
title: Scripts_Client 전량 스타일 정리 — XML 주석 제거 · 중괄호 강제 · 수직 간격
tags: [client, docs, style]
---

# Scripts_Client 전량 스타일 정리

## 목적 / 배경
- 사용자가 `///` XML 문서 주석의 값을 물었다. **Rider는 이 태그를 호버·Ctrl+Q에 실제로 렌더링한다**
  — "IDE에서 적용 안 된다"는 전제는 사실이 아니다. 그럼에도 제거를 택한 이유는 비용 대비 이득이다:
  `Assets/csc.rsp`에 `GenerateDocumentationFile`이 없어 문서 XML을 만들지 않고, 외부 배포 라이브러리가
  아니라 태그가 본문을 밀어내는 비용만 남는다.
- 같은 김에 미뤄둔 포맷 두 가지(중괄호 · 수직 간격)를 확정해 58개 파일 전량에 적용했다.

## 변경 내용
- 커밋 `8b3fa91`·`bfaf557`·`4ed742a`·`21395a7`·`2af2d86` (폴더 단위) + `62eebb8` (문서·스킬).
  파일별 diff는 커밋이 말한다 — 여기 다시 적지 않는다.
- 규칙 원문은 `.claude/skills/client/clean-code-style/SKILL.md` 3장·6장.

## 주요 결정 / 근거
- **`<param>`은 `//   이름 : 설명` 블록으로 옮겼다** (10건, 수작업). 자동 변환하면 이름 열 정렬이 깨진다.
- **가드절은 그대로 뒀다.** 종료문 앞 빈 줄은 블록의 **첫 문장·유일한 문장이 아닐 때만** 넣는다.
  `if (x) { return; }`에 빈 줄을 넣으면 3줄짜리 가드가 5줄이 되어 규칙의 목적(구분)과 반대로 간다.
- **`else if` 체인은 유지**한다. `else { if ... }`로 중첩 전개하지 않는다.
- 툴킷 마스터(`~/.claude/skills`)의 `templates/code/Common/` 7개와 클라 스킬 SKILL.md도 함께 맞췄다.
  사본만 고치면 다음 `/unity-skill-sync`에서 `///`가 되살아난다.
- 마스터에 들어가는 예시에서 **프로젝트 클래스 이름을 지웠다**(`ClientLogger` → `Debug.LogWarning` 등).
  전역 지침상 범용 코드·문서에 프로젝트 이름을 남기지 않는다. 덕분에 프로젝트 사본과 마스터가 바이트 동일이다.

## 지뢰 / 함정
- **이 환경의 `python`은 WindowsApps 스토어 스텁이라 동작하지 않는다.** `python -c "print('ok')"`가
  "Python"만 출력하고 끝난다. 일괄 변환은 sed/awk/perl로 해야 한다.
- **perl 치환에서 `$"`가 보간된다.** C# 보간 문자열(`$"..."`)을 다루면 조용히 망가진다.
  awk 범위 치환 + heredoc으로 우회했다.
- **sed/awk 파이프는 CRLF를 LF로 떨어뜨린다.** 이 저장소는 `.gitattributes`의 `* text=auto`라
  저장소 내용은 무사하지만 작업 트리가 섞인다. 변환 스크립트 끝에 `sed -E 's/\r?$/\r/'`를 붙였다.
- 검증은 `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj`로 했다
  (유니티를 열지 않아도 구문 확인이 된다). 의미 불변은
  `git diff --ignore-all-space`에서 주석·중괄호·빈 줄 외 코드 줄이 0인 것으로 확인했다.

## 후속 작업 / 주의사항
- **툴킷 저장소(`~/.claude/skills`)에 8개 파일이 커밋되지 않은 채 남아 있다** — 커밋은 사용자 몫이다.
- 유니티 에디터 재컴파일 · 런타임 한 바퀴(로그인 → 가챠 → 작업슬롯) 확인은 아직 안 했다.
- `Assets/Scripts_Server/`·`Server/`에는 XML 주석 433건이 그대로다. 서버 담당 영역이라 손대지 않았고,
  이번 변경으로 클라/서버 주석 스타일이 갈렸다. 적용 여부는 별건.
