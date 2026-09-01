---
date: 2026-09-02
title: 재화 구조를 행(currency_type)에서 컬럼(gold·dia)으로
tags: [server, client, protocol, db, currency]
---

# 재화 구조를 행에서 컬럼으로 (currency_type → gold · dia)

## 목적 / 배경
- 재화 종류를 키로 갖는 구조(`t_user_currency.currency_type` · `CurrencyWallet` 딕셔너리 ·
  `S_CurrencyResponse { List<CurrencyInfo> }`)가 실제로는 골드 하나만 쓰는데 세 겹의 간접을 물고 있었다.
- 골드를 버는 길·쓰는 길(T-027 가챠비용 · T-028 즉시판매)이 아직 미착수라
  `GainCurrency`/`TrySpendCurrency` 호출부가 **하나도 없었다** — 접기에 가장 싼 시점이었다.

## 변경 내용
- **DB** — `t_user_currency`는 **테이블을 유지**하되 `(user_id, currency_type, amount)`를
  `(user_id PK, gold, dia)`로 바꿨다. `dia`는 유료 재화.
  `Server/Shared/game.sqlite3`에 직접 DDL로 반영(마이그레이션 스크립트 없음).
- `Server/MikaProtocol/` — `S_CurrencyResponse { List<CurrencyInfo> }` → `S_CurrencyResponse { long Gold; long Dia }`.
  `CurrencyInfo` 삭제. PacketId 16 유지.
- `Server/WSGameServer/` — `CurrencyWallet` 삭제. `User.Currency.cs`가 `_gold`·`_dia` 필드와
  `GainGold`/`GainDia`/`TrySpendGold`/`TrySpendDia`를 갖는다.
  `PlayerLoginData.CurrencyRows`(List) → `Currency`(`CurrencyRow?` 단건).
- 클라 — **손대지 않았다.** 한 번 고쳤다가 담당 폴더가 아니라 전부 되돌렸다
  (CLAUDE.md "각자 자기 담당 폴더만 수정한다"). 남은 몫은 **T-042** · [이슈 #20](https://github.com/JeongTaeWoong99/Windows_simulator/issues/20)로 등록했다.
  `Assets/Scripts_Server/Protocol/`·`Assets/Plugins/Analyzers/`는 서버 빌드가 만드는
  **생성물이라 되돌리지 않았다** — 손으로 되돌리는 것 자체가 미러 직접 수정이고, 다음 빌드가 다시 덮는다.

## 주요 결정 / 근거
- **확장 축은 "행"이 아니라 "컬럼"이다.** 재화가 늘면 `t_user_currency`에 컬럼을 추가한다.
  행(`currency_type`)으로 늘리지 말 것. `t_user`에 붙이지도 않는다 — 재화는 재화 테이블에 모은다.
- **패킷도 같은 축을 쓴다.** 재화마다 패킷을 나누지 않고 `S_CurrencyResponse`에 필드를 더한다.
  **한쪽만 바뀌어도 둘 다 실어 보낸다** — 둘 다 확정 잔액이라 덮어써도 안전하고,
  나누면 "무엇을 보내야 하는가"를 호출부가 매번 판단해야 한다.
  같은 이유로 `SaveCurrencyRepository`도 두 컬럼을 한 UPSERT에 담는다.
- **`TrySpend`는 `ref long`으로 잔액을 받는다.** 그래서 `Gold`/`Dia`가 자동 프로퍼티가 아니라
  `_gold`/`_dia` 필드다 — 재화가 늘어도 검사 로직(양수·잔액 부족)이 복사되지 않는다.
- **가입 시 0짜리 행을 만들지 않는다.** 행이 없으면(`CurrencyRow?`가 null) 0으로 읽는다.
  0 행을 깔면 재화 컬럼이 늘 때마다 기존 유저 전원 백필이 필요해진다. 첫 변동이 곧 첫 INSERT라
  `SaveCurrencyRepository`는 UPSERT다. 0으로 보는 판단은 리포지토리가 아니라 로직 스레드가 한다.
- **저장은 델타가 아니라 확정 잔액.** 재시도·중복 전송이 곧 재화 복제가 되기 때문(기존 규약 유지).
- **`long`을 고수한다.** 거래 경제가 붙으면 누적 골드가 int 상한(약 21억)을 넘고,
  넘는 순간 조용히 음수가 된다.
- **`GameData.CurrencyType` enum은 남겨 뒀다.** `Enum.xlsx`에서 생성되는 파일이라
  지우려면 엑셀 + 파이프라인을 건드려야 하는데, 지금은 코드 어디서도 참조하지 않아 무해하다.

## 후속 작업 / 주의사항
- **⚠️ 지금 Unity 프로젝트는 컴파일되지 않는다.** 미러의 `S_CurrencyResponse`가 필드형으로 바뀌고
  `CurrencyInfo`가 사라졌는데 클라 코드는 `res.Currencies`를 순회한다 → **T-042**([이슈 #20](https://github.com/JeongTaeWoong99/Windows_simulator/issues/20))에서 푼다.
  서버·클라 커밋이 갈라지는 구조라 **이 상태를 푸시하면 클라 쪽이 먼저 막힌다** — 순서를 맞출 것.
- **`Server/Shared/game.sqlite3`는 커밋 대상 파일이고 이번에 스키마가 바뀌었다.** 기존 재화 데이터는
  0행이라 이관하지 않았다. DDL 원본이 저장소에 없으므로(`.sql` 파일 부재) 스키마 변경 이력은
  이 로그와 커밋 diff에만 남는다.
- `User.Currency.cs`는 아직 호출부가 없어 커버리지가 0에 가깝다. T-027·T-028에서 호출부가 생길 때
  `server-tdd`로 채운다.
- **`Dia`는 그릇만 있고 결제 경로가 없다.** 유료 재화라 실제로 붙일 때 결제 영수증·환불·CS 대비
  **지급 이력 테이블**이 따로 필요하다 — 잔액만으로는 "얼마를 왜 줬는가"를 되짚을 수 없다.
- **`GameData.CurrencyType` enum에는 아직 `Dia`가 없다**(`Gold = 1`뿐). 코드는 이 enum을 쓰지 않아
  당장 문제는 없지만, T-027이 가챠 비용 통화를 엑셀 컬럼으로 두려 하므로 그때 `Enum.xlsx`에
  `Dia = 2`를 **뒤에 추가**해야 한다(중간 삽입 금지 — 게임기획코어 EID 규칙).
- 이번 작업과 무관하게 워킹트리에 하트비트 관련 수정(`Common/Global.cs`·`SessionWatchdog.cs`·
  `PingManager.cs`·`SessionIdleSweepTest.cs`·`tasks/T-040`)이 함께 올라와 있었다 — **커밋을 나눌 것.**
