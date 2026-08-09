---
date: 2026-08-10
title: 산업 레벨 선택 수단 — DB 컬럼·해금 테이블·배치 패킷 + 서버 검증
tags: [server, db, packet, workslot, industry-level, T-017]
---

# 산업 레벨 선택 수단 (T-017 DB·패킷)

## 목적 / 배경

- 서버는 레벨을 **쓸** 줄 알았지만(드롭 롤·판정 비용) 레벨이 1 말고 다른 값이 될 **경로가 없었다** —
  패킷에 필드가 없고 DB에 컬럼이 없어 코드에 `DefaultIndustryLevel`이 박혀 있었다.
- 사용자 지시로 T-017의 DB·패킷을 진행. **해금 판정(3번)은 다음 작업**이지만,
  검증 없이 필드만 뚫으면 클라가 Lv5를 적어 보내는 구멍이 생기므로
  **저장된 해금 레벨과 대조하는 것까지는 이번에 넣었다.**

## 변경 내용

- DB(`Server/Shared/game.sqlite3`) — `t_user_workstation_slot.industry_level` 추가,
  `t_user_industry_level`(user_id, industry, unlocked_level) 신설
- `MikaProtocol` — `C_WorkStationAssignRequest.IndustryLevel`(byte) ·
  `WorkStationSlotInfo.IndustryLevel` · `EResultCode.IndustryLevelLocked = 203`
- `RepositoryContracts` — `WorkStationSlotRow.industry_level`, `UserIndustryLevelRow` 신설,
  `PlayerLoginData`에 `IndustryLevelRows` 추가
- `LoginRepository`(SELECT 2곳) · `SaveWorkStationSlotRepository`(UPSERT) · `User.DB.cs` 배선
- `User.WorkStation.cs` — `_industryUnlocks` 사전 · `LoadIndustryLevels` · `GetUnlockedIndustryLevel`,
  `AssignWorkStation`에 레벨 인자 + 해금 검증
- `ClientPacketHandler` — 레벨 전달 + 구 클라 `0` 정규화
- 테스트 7건 추가(미해금 거절·하향 선택·산업별 독립·하한·로그인 복원) — 전체 **185건 통과**

## 주요 결정 / 근거

- **`ALTER TABLE ADD COLUMN`을 쓰지 않고 테이블을 재작성했다.** 이 저장소는 스키마 `.sql` 파일이 없어
  **sqlite_master의 DDL 텍스트가 유일한 문서**인데, ADD COLUMN이 새 컬럼을 기존 컬럼의 `--` 주석
  앞에 끼워 넣어 주석이 다른 컬럼을 가리키게 됐다. 새 테이블 → INSERT SELECT → RENAME으로
  컬럼 순서·주석을 정합하게 맞췄다(기존 4행 보존·`integrity_check` ok 확인).
  **다음에 컬럼을 더할 때도 같은 절차를 쓴다.**
- **레벨 `0` 정규화는 도메인이 아니라 핸들러에서** 한다. 구 클라이언트가 필드를 안 채우면 0이 오는데,
  이건 와이어 호환 문제지 도메인 규칙이 아니다. 도메인(`AssignWorkStation`)은 `1 미만`을 그대로 거절한다 —
  0·음수가 "열린 레벨 이하" 비교를 통과해 버리기 때문에 하한 검사가 따로 필요했다.
- **해금 기록이 없는 산업 = Lv1**로 본다(가입 시 5종 행을 만들지 않는다). 재화 테이블과 같은 규약이라
  산업이 늘어도 백필이 필요 없다.
- 클라이언트(`Assets/Scripts_Client`)는 담당이 달라 손대지 않았다. 0 정규화 덕에 **구 클라도 그대로 동작**한다.

## 업데이트 (2026-08-10) — T-017 완료 · 해금은 T-021로 분리

- 사용자 지시로 **해금 판정을 [T-021](../../tasks/T-021-산업레벨해금판정.md)로 떼어냈다.**
  요구치 `N`·`M`이 임시값(T-016 미완)이라 지금 만들어도 밸런스 검증이 안 되기 때문이다.
- T-017의 남은 항목을 확인해 보니 **해금 외에는 이미 다 돼 있었다** — 적성 0 거절(`NoAptitude`),
  레벨 변경 전 정산(`AssignWorkStation`이 같은 경로를 탄다). 후자는 완료 조건이라
  **테스트로 잠근 뒤** 체크했다: `레벨을_바꾸기_전_구간은_이전_레벨의_비용으로_정산된다`
  (Lv1 60초 = 2판정 / 새 Lv2 비용으로 계산하면 0판정이라 순서가 뒤집히면 즉시 빨개진다). 전체 **186건**.
- **T-017을 `tasks/archive/`로 내렸다** — 상대 링크 한 단계 조정 + 각 문서의 `T-017` 참조를
  `T-021`로 갱신(산업레벨.md · fishing · gathering · 기획평가 R21 ✅해소 · 문서관계도 · ui).

## 후속 작업 / 주의사항

- 🔴 **해금 판정이 없다 — `t_user_industry_level`을 올려 주는 코드가 어디에도 없다.**
  그래서 지금은 전원 Lv1이고 상위 레벨 배치는 전부 `IndustryLevelLocked`로 거절된다.
  판정 시점은 **캐릭터 획득 시 · 계정 레벨업 시** 두 곳(산업레벨.md 3.2), 조건은 적성 N + 계정 레벨 M.
  ⚠️ **요구치 N·M이 아직 임시값**이라(계정 레벨 곡선 미정) 구현해도 밸런스 검증은 못 한다.
- 해금 UPSERT용 Repository도 아직 없다 — 판정 로직과 같은 작업 단위로 만든다.
- 클라가 레벨을 실제로 보내려면 `Assets/Scripts_Client` 쪽 작업이 필요하다(합의 후).
