---
date: 2026-09-26
title: 상수 8개 Constants.xlsx 이관 · 채취 창고 한도 (T-085 · #34)
tags: [server, data, test, docs]
---

# 상수 8개 Constants.xlsx 이관 · 채취 창고 한도 (T-085 · #34)

## 목적 / 배경
- #34 서버 몫 마무리 — 클라 추가 요청(A·C) 값과 단위 상수를 시트로, 채취 정산에 한도 검사

## 변경 내용
- `Constants.xlsx` 8행 추가 → `WorkStationSlot`·`EnchantCatalog`·`User.Market`·`User.Character`의 `const`를 `Constants` 조회 속성으로
- `User.SettleWorkStation` — 창고가 가득 차면 새 종류 산출을 버린다 (→ `Server/docs/채취-정산.md` 3장)
- 결정 경위·분류 → `tasks/T-085` 7장

## 주요 결정 / 근거
- **`WorkSpeedScale`·`BaseCycleSeconds`를 시트로 둔 건 사용자 결정**이다(나는 코드 공용 상수를 권했다). `WorkSpeedScale`은 모든 ‰ 데이터의 분모라 바꾸면 데이터 재작성이 필요 — `GameDesign/CLAUDE.md`와 시트 Description에 경고를 남겼다
- 채취 가득 참은 (가) 버림 — 슬롯을 멈추면 방치형 전제가 깨지고, 우편 넘침은 판정마다 우편이 쌓여 제외된 상태였다
- `YieldPerJudge`는 T-009 이후 산업 시트로 갈 값이라 제외, 우편 템플릿 TID는 코드가 구조적으로 지목하므로 코드에 둔다

## 후속 작업 / 주의사항
- ⚠️ **`const` → 속성이 되어 기본 매개변수·`const` 식에 못 쓴다.** `WorkStationSlot` 생성자·`Assign`은 `int?`/`long?` 기본값 `null`로, `AssignWorkStation`은 오버로드로 바꿨다. 새 코드도 같은 방식으로
- 순수 단위 테스트(`WorkSpeedTest`)는 테이블을 안 읽으므로 `DefaultWorkSpeed` 대신 리터럴 1000을 쓴다
- 남은 것: 산업·희귀도 표시 이름(옛 T-047) · 기획 범위 결정 · 클라의 `/1000f`·`DefaultIndustryLevel` 교체
