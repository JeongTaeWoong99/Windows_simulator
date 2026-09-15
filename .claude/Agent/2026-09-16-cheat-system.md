---
date: 2026-09-16
title: 치트 명령 — admin 전용 지급·정산 패킷 (기획 → 서버 구현)
tags: [server, protocol, cheat, test]
---

# 치트 명령 — admin 전용 지급·정산 패킷

## 목적 / 배경

- 사용자 요청 "치트 사용하는 것도 기획부터 시작해서 쭉 구현" — 중단 없이. 결정은 내가 내리고 근거를 여기 남긴다.
- 기획은 `Server/docs/치트.md`. 게임 규칙이 아니라 개발 인프라라 `GameDesign/design`에 넣지 않았다(전파 그래프 밖).

## 주요 결정 / 근거

- **명령은 enum, 인자는 `long Arg1·Arg2`.** 문자열 명령줄(`"gold 100"`)을 서버가 파싱하지 않는다 — 해금 조건에서 파서를 거부한 것과 같은 이유.
  `long` 하나로 골드(long)·개체 PK(long)·TID(int)를 전부 나른다.
- **치트 전용 지급 코드를 만들지 않았다.** `GainGold`·`TrySpendGold`·`GainItem`·`GrantGachaCharacters`·`GrantCharacterExp`·`SettleWorkStation`을
  그대로 부른다. 치트로 확인한 경로가 곧 게임 경로라야 검증에 의미가 있다. 그래서 `GrantWorkExp`를 `GrantCharacterExp`(public)로 이름을 바꿨다.
- **권한은 `admin_level ≥ 1`뿐.** 운영 빌드 차단 스위치를 따로 두지 않았다 — `admin_level`이 곧 스위치다. 성공·실패 전부 `ServerLog.Warn("치트")`.
- **음수 금액 = 차감.** `Gain*`이 음수를 던지므로(`ThrowIfNegativeOrZero`) 부호로 갈라 `TrySpend*`를 탄다.
- **결과 코드 400대(치트) 신설**: `NoPermission`·`InvalidCheatCommand`·`InvalidCheatArgs`. 캐릭터 미보유·잔액 부족은 기존 코드를 재사용.
- **해금 명령은 뺐다.** `GrantUnlock`(해금 2.4)이 서버에 생기면 `Unlock = 7`을 뒤에 더한다 — T-038 할 일에 적었다.

## 함정

- **`GameTableFixture`가 읽는 `ItemTable`에 테스트 드롭 TID(1001)가 없다.** 치트는 TID 존재를 검사하므로 테스트에 실제 첫 행(10001 붕어)을 써야 한다.
- 테스트 곡선에 Lv3 행이 없으면 Lv2가 만렙이라 이월 조각이 0으로 버려진다 — 레벨업 테스트는 만렙보다 한 칸 아래에서 한다.
- TDD red는 `dotnet build`로 확인했다(`ExecuteCheat` 미정의). 실행 중 서버 때문에 `-p:BaseOutputPath`로 출력을 분리하는 것은 그대로다.

## 후속 작업 / 주의사항

- **클라 입력창은 T-053**(클라). Unity 쪽은 `CheatResponded` 이벤트까지만 있다. 더미 클라 메뉴 "Cheat"로는 지금 보낼 수 있다.
- admin 계정은 DB에서 `t_user.admin_level`을 1로 올려야 한다 — 올리는 치트는 일부러 없다.
- 이번 세션의 해금 재검토 반영(`Name`·`GrantUnlock`·검증·지불 OR·확장권 폐지)은 `2026-09-14-unlock-system-design.md`의 업데이트 절.
