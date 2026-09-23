---
date: 2026-09-23
title: 인챈트 데이터 테이블 — EquipEnchant.xlsx · Enum · 아이템 · 상자 풀
tags: [data, design, server]
---

# 인챈트 데이터 테이블

## 목적 / 배경

- 장비 인챈트 구현(T-085)의 1단계. **데이터만** 넣고 서버 코드는 뒤 작업으로 미룬다.
- 요구값은 `.superpowers/sdd/2026-09-23-equip-enchant/task-1-brief.md`에 있다.
  파일명·시트명·아이템 이름/TID는 2026-09-23에 사용자 확인을 받은 값이다.

## 변경 내용

- 커밋 `3f4dce1` 참조. 엑셀 4개(`Enum`·`EquipEnchant`(신규)·`Item`·`Gacha`) + 파이프라인 생성물.

## 주요 결정 / 근거

- **`EnchantGradeTable`의 키는 `Grade`(eGlobalRarity)다.** 별도 TID를 두지 않았다 —
  등급이 곧 정체성이라 1:1 컬럼이 둘이 되면 어긋날 여지만 생긴다(`WorkSpeedTable`과 같은 판단).
  생성 결과는 `TableSet<GlobalRarity, EnchantGradeTableRow>`.
- **`EnchantOptionTable`의 키는 `EnchantOptionTID`다.** 처음엔 브리프대로 `OptionTID`로 넣었다가
  개명했다(`2202b39`) — 저장소의 모든 테이블이 **시트 접두 전체**를 쓰고(`GachaItemTID`·`MailTemplateTID` …)
  하드 규칙 1도 그렇게 못박는다. `Option`은 `EnchantOption`의 접두가 아니다.
  서버 코드가 Row 타입을 읽기 전에 고쳐야 값이 싸다.
- `GachaItemTable` 행은 **풀 대역 안에 끼워 넣었다**(6007~/7007~/8007~). 맨 뒤에 붙이면
  엑셀에서 풀별 묶음이 끊긴다. DataLog JSON은 어느 쪽이든 삽입 diff라 리뷰 비용은 같다.
- `EnchantItemTable.SuccessPermille`은 `GradeUp`(인챈트 큐브) 행에서 **읽히지 않는다.**
  등급 상승 확률은 `EnchantGradeTable.UpPermille`이 갖는다 — 값을 비울 수 없어 1000을 넣고
  `Description`에 명시했다. 서버 구현 시 이 컬럼을 큐브에 적용하지 않도록 주의.

## 후속 작업 / 주의사항

- ⚠️ **`format-excel.py`를 인자 없이 돌리면 엑셀 17개가 전부 재저장되어** 손대지 않은 파일까지
  변경으로 잡힌다. 고친 파일만 인자로 넘긴다(`python GameDesign/format-excel.py Item.xlsx`).
  이번엔 전체를 돌린 뒤 무관한 13개를 `git restore`로 되돌렸다.
- Unity를 열지 못해 새 미러 자산 6개의 `.meta`를 **손으로 만들었다**(GUID는 무작위, 저장소의
  기존 미러 `.meta`와 같은 최소 형식). 다음에 Unity를 열면 임포터 블록이 붙어 파일이 한 번 바뀐다.
- 서버 코드·패킷·DB는 아직 없다 → `tasks/T-085`.
