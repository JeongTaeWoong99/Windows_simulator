---
date: 2026-10-08
title: 문서 사이트 게임 데이터 뷰어 /data (T-132)
tags: [docs, web, data]
---

# 문서 사이트 게임 데이터 뷰어 /data (T-132)

## 목적 / 배경
- 레벨별 드롭 테이블 등 엑셀 수치를 wsdocs에서 바로 보고 싶다는 요청 → `tasks/T-132`

## 변경 내용
- `GameDesign/web/src/lib/datalog.ts` — JSON 로드 · 이름표 · 거르기 열 · 단위 환산
- `GameDesign/web/src/pages/data/` — 목록(`index.astro`) · 테이블 페이지(`[table].astro`)
- 내비 `데이터` 탭 · `docs-deploy.yml` 트리거 · 버전 0.5.0 · `사이트.md` `/data` 절

## 주요 결정 / 근거
- 엑셀이 아니라 `DataLog/*.json`을 읽는다 — `.bytes`를 되읽은 값이라 서버·클라가 받는 것과 같고, CI에 xlsx 파서가 필요 없다.
- 테이블별 코드를 두지 않았다. 이름 규칙으로만 꾸민다: 첫 열 `<키>TID` + `Name` → 이름표, `Permille`/`Permyriad`/`PerMillion` → %, `Weight` → 묶음 비율.
- 거르기 상자는 글자 열 + `…Level`/`…Id` 숫자 열만. 처음엔 값 12종 이하면 다 달았더니 Weight·가격에도 붙어 상자가 넘쳤다.
- 비율 묶음 열은 `WEIGHT_GROUP_KEYS`(`GachaId` → `IndustryLevel` → `Grade`) 고정 목록이다 — 롤 단위를 데이터에서 추론할 수 없어서.

## 후속 작업 / 주의사항
- 새 가중치 테이블의 롤 단위가 위 셋이 아니면 비율이 틀린다 → `WEIGHT_GROUP_KEYS`에 그 열을 추가.
- 브라우저 실측은 못 했다(로컬 서버를 내장 브라우저로 못 연다). 빌드 HTML만 확인.
