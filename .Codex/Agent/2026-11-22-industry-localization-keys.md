---
date: 2026-11-22
title: 산업 번역 키에 enum 이름 적용
tags: [data, localization]
---

# 산업 번역 키에 enum 이름 적용

- PR #145의 산업명·툴팁 키는 생성기가 첫 열의 값을 사용한다. `IndustryTID`의 열 이름을 유지하고 타입을 기존 `eIndustryType`으로 바꿔 `Industry.Farming.Tooltip` 형식을 생성한다.
- 기존 한국어·영어 문구를 새 키로 옮겼다. 클라이언트는 `GameTable.IndustryTable[IndustryType.Farming].TooltipLocKey`로 같은 키를 가져온다.
- 새 테이블을 읽는 코드가 아직 없어 enum으로 바로 조회하도록 형식을 확정했다. 다른 테이블의 숫자 기반 번역 키는 기존 규칙을 따른다.
