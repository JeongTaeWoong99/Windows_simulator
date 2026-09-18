---
date: 2026-09-19
title: 엑셀 서식 기준 · excel-table-creator 클라·기획용 재정리 (T-065)
tags: [gamedesign, excel, skill, pipeline]
---

# 엑셀 서식 기준 · excel-table-creator 클라·기획용 재정리 (T-065)

## 목적 / 배경
- 데이터 추가는 앞으로 클라·기획이 한다. 새 스킬(Excel-Writer)을 만들지 않고 기존 `excel-table-creator`를 갱신하기로 했다.
- 사용자가 서식 기준을 확정했다 — 테이블 시트는 `Character.xlsx`, Enum 시트는 `Enum.xlsx`. 어긋난 칸은 전부 맞춘다.

## 변경 내용
- `GameDesign/format-excel.py` (신규) — openpyxl로 전 워크북 서식 적용. 값은 불변. 마커 행·컬럼명 행은 A열로 찾고, 숫자/문자 정렬은 `Type` 행으로 가른다. 열 너비 = 최장 표시폭(한글 2칸)+2, 6~60.
- `GameDesign/Excel/*.xlsx` 12개 — 서식 적용 + 설명 행 `//`의 긴 한글을 짧게(원문은 셀 메모).
- `excel-table-creator/SKILL.md` — description 재작성, 행 추가 절차, 서식 절, 실행 절차에 `format-excel.py`, 체크리스트.
- `컬럼 레퍼런스.md`, 루트 `CLAUDE.md` 스킬 표, `GameDesign/CLAUDE.md` 폴더 표.

## 주요 결정 / 근거
- `//` 행은 생성기(`TableCodeGenerator.BuildColumnComment`)가 읽지 않는다 — 스킬의 "코드에 주석으로 들어간다"는 틀린 서술이었다. 그래서 줄여도 생성물이 안 바뀐다.
- 파이프라인 재실행 후 `.cs`·`.bytes`·`DataLog` diff 없음을 확인.
- Enum 1~3행 색은 기존 테마 색(dk1 / dk1 tint 0.35 / lt2 tint −0.1)을 그대로 썼다. 테이블 주황은 테마 accent6.
- 스킬 수정은 프로젝트 고유 내용이라 Arca 마스터로 올리지 않는다.

## 후속
- 커밋 후 T-065를 완료 처리·보관한다.
- Description 열(데이터 메모)은 길어서 열 너비 상한 60에 걸린다 — 의도된 동작.
