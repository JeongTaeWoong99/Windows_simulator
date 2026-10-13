---
date: 2026-10-13
title: 클라 화면 글자·문서의 낚시 → 발굴 (T-148) · art 폴더 정리
tags: [client, docs, editor]
---

# 클라 화면 글자·문서의 낚시 → 발굴 (T-148) · art 폴더 정리

## 목적 / 배경
- T-142에서 서버·데이터가 발굴로 바뀌고 클라는 컴파일 참조만 바뀐 상태 → 화면 글자·문서를 맞춘다.
- 사용자 지시: 아트만 남으면 `tasks/art/`로, 끝나면 `archive/`로. T-031은 아트까지 끝났다고 판단 → 보관.

## 변경 내용
- 씬(원본 · Test Copy) TMP 글자 `\uB09A\uC2DC` → `\uBC1C\uAD74` 2곳씩 · 오브젝트 이름 `Fishing Cell`·`Fishing Button`·`Filter Fishing Button` → `Excavation …`.
- 클라 규칙 문서 6개 + `Art 규칙.md` — 머리줄의 옛 이력 속 "낚시"는 기록이라 그대로 둠.
- T-148 → `tasks/art/` (🎨만 남음) · T-031 → `archive/` (희귀도 스프라이트는 색 구분으로 대신하고 닫음).

## 주요 결정 / 근거
- `Assets/Art/targets/fishing/` 폴더는 이름을 안 바꿨다 — 발굴 그림을 새로 구울 때 키 `excavation`으로 교체(T-148 🎨). 지금은 산업 값 2의 임시 그림.
- 씬 YAML은 텍스트로 직접 고쳤다 — 코드가 오브젝트 이름으로 찾는 곳이 없음을 grep으로 확인.

## 후속 작업 / 주의사항
- 씬 YAML 치환 함정: TMP 글자는 `\uXXXX` 이스케이프라 `낚시`로 grep하면 안 잡힌다. perl 치환 때 `\u`는 대소문자 변환자라 `\x5c`로 써야 맞는다. Test Copy 씬은 gitignore라 diff에 안 보인다.
- `RarityPalette`의 `TODO: 등급 스프라이트` 주석은 남아 있다 — T-031에서 닫은 항목.
- 발굴 실플레이(배치 → 채취 → 획득) 확인은 #66을 닫을 때.
