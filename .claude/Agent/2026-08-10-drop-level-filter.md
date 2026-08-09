---
date: 2026-08-10
title: 드롭 롤 레벨 분리 — DropTableCatalog 키를 (산업, 레벨)로
tags: [server, drop, industry-level, T-017]
---

# 드롭 롤 레벨 분리 — T-017 부분 구현

## 목적 / 배경

- 엑셀은 레벨별로 갈라졌는데 서버가 `IndustryLevel`을 안 읽어 **모든 레벨 아이템이 한 통에 섞여
  나오던 오동작**(T-017 🔴)을 해소. 사용자 요청: "특정 레벨의 드롭테이블만 보고 뽑도록".

## 변경 내용

- `Server/WSGameServer/Common/DropTableCatalog.cs` — 키 `ItemType` → `(ItemType, int Level)`.
  `LoadAll`이 시트 행을 `IndustryLevel`로 `GroupBy`해 25개 테이블 등록(이름 `이름.Lv레벨`)
- `Server/WSGameServer/User/WorkStation/WorkStationSlot.cs` — `IndustryLevel` 프로퍼티 +
  `DefaultIndustryLevel = 1` 상수. 생성자·`Assign`에 선택 인자(끝자리)로 추가
- `Server/WSGameServer/User/WorkStation/WorkStation.cs` — `Settle`이 `(산업, 슬롯.레벨)`로 조회
- 테스트: `DropTableTest` 카탈로그부 (산업, 레벨) 축으로 갱신, `DropSheetTest` 25조합
  Theory로 재편(+등록 수 25 회귀 테스트), `WorkStationSlotTest`·`TestUserBuilder` 등록부 보수
- 문서: T-017(드롭 롤 항목 완료) · tasks/README · 산업레벨.md 6.2/6.4 · gathering/README ·
  fishing/README · 기획평가 R21(🔴→🟡) — 전파 후 `check-doc-graph -Changed` 통과

## 주요 결정 / 근거

- **레벨 인자를 생성자·`Assign`의 끝자리 선택 인자(기본 Lv1)로 뒀다.** DB·패킷에 레벨이 없어
  호출부가 레벨을 줄 방법이 아직 없고, 현재 해금이 Lv1뿐이라 기본값이 기획과 일치한다.
  필수 인자로 만들면 지금은 모든 호출부가 상수 1을 적게 될 뿐이다.
- 테이블 이름을 엑셀 레벨별 시트명과 같은 꼴(`FishingBasicTable.Lv2`)로 맞춰 로그에서 시트가 바로 짚인다.

## 후속 작업 / 주의사항

- **슬롯 레벨은 DB 미저장** — 재접속하면 항상 Lv1로 적재된다. `t_user_workstation_slot.industry_level`
  컬럼·패킷(`WorkStationSlotInfo.IndustryLevel`)·해금 검증이 붙어야 실제로 레벨 선택이 산다 (T-017 잔여).
- **`JudgeCost`는 여전히 상수 30초** — 상위 레벨도 30초 주기로 돈다. `RequiredScore × 1000` 전환은 T-017 잔여.
- `DropSheetTest`의 분포 검증은 이제 (산업, 레벨) 축 — 레벨이 늘면 `AllIndustryLevels()`의 상한(5)을 고친다.
