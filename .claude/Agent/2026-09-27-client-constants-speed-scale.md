---
date: 2026-09-27
title: 클라 속도 스케일·기본 산업 레벨을 Constants 조회로 교체 (T-085 클라 몫 종료)
tags: [client, data]
---

# 클라 속도 스케일·기본 산업 레벨을 Constants 조회로 교체

## 목적 / 배경
- T-085 클라 파트의 마지막 항목. 시트(`Constants.xlsx`)에 `WorkSpeedScale`·`DefaultIndustryLevel`이 들어와 클라 사본을 걷어냈다 → `tasks/T-085`
- 이슈 #34(클라 몫 종료)·#39(서버 수정 확인)를 코멘트와 함께 닫았다

## 주요 결정 / 근거
- **`WorkStationProgress`의 1000은 속도 스케일이 아니다.** 작업량 단위가 "ms × 천분율 속도"라서 `작업량 / CurrentWorkSpeed`는 이미 ms이고, 스케일은 여기서 약분된다. 남는 1000은 ms→초 변환이다. 일감 체크리스트에는 "스케일 1000"으로 적혀 있었지만 교체하지 않고 `MillisecondsPerSecond`로 이름만 바꿨다
- 실제로 스케일을 쓰던 곳은 배율 표시 3곳이었다: `WorkStationSlotView` · `CharacterSlotSource` 툴팁 · `WorkStationSelectPresenter`의 `FormatSpeed`·기대 속도 식
- `WorkStationSelectPresenter._selectedIndustryLevel`은 필드 초기화 대신 `Start`에서 채운다. MonoBehaviour 필드 초기화는 에디터 직렬화 때도 돌기 때문에, 그 시점에는 테이블이 없어 `Constants` 조회가 터질 수 있다

## 후속 작업 / 주의사항
- `FormatPermille`·`EquipLabel`의 `/ 10f`(천분율→%)는 스케일과 무관한 단위 변환이라 그대로 뒀다. 다만 스케일을 바꾸면 ‰ 데이터 전체를 다시 해석해야 하는 건 시트 주석에 적힌 그대로다
- 기준 주기 표시(`RequiredScore / 1000f`)의 1000도 서버 `IndustryLevelCatalog`가 하드코딩한 ×1000과 짝이라 건드리지 않았다
