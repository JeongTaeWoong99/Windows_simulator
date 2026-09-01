---
date: 2026-09-02
title: 캐릭터 가챠 — 시트 3분할 · 캐릭터 30종 · 골드 비용
tags: [server, data, design, gacha, character]
---

# 캐릭터 가챠 — 시트 3분할 · 캐릭터 30종 · 골드 비용

## 목적 / 배경

→ `tasks/archive/T-026-캐릭터가챠.md` · `tasks/archive/T-027-가챠비용.md`
(둘을 한 작업 단위로 끝냈다. 비용 없는 캐릭터 가챠는 검증할 수 없어서다.)

캐릭터가 1종뿐이라 배치가 퍼즐이 아니었고, 가챠는 아이템만 줬다.
사용자 요청으로 **캐릭터 30종 확장**이 이 작업에 합류했다.

## 변경 내용

`GameDesign/Excel` · 서버 `Gacha`/`User`/`Repository` · `MikaProtocol` — 상세는 diff.
구조만 적으면:

- `Gacha.xlsx`가 시트 **셋**이 됐다 — `GachaInfoTable`(풀 메타·비용) ·
  `GachaItemTable`(구 `GachaTable`) · `GachaCharacterTable`(신설).
- `CharacterTable`에 `GlobalRarity` 컬럼 + `1007~1030` 24종.
- `GachaEntry`가 `(GachaId, RewardType, RewardTID, Count, Weight)`로 정규화됐다.

## 주요 결정 / 근거

- **보상 종류를 컬럼이 아니라 시트로 갈랐다.** T-026 원안은 `GachaTable`에 `RewardType`
  컬럼을 넣는 것이었는데, 그러면 `ItemTID`의 `Ref`를 `Ref?`로 풀어야 한다 —
  **캐릭터 행이 아이템 참조 검사에 걸리기 때문이다.** 시트를 가르면 각 시트가 자기
  `Ref`를 그대로 유지하고, **종류는 값이 아니라 출처가 정한다.**
  `GachaPoolCatalog`가 두 시트를 같은 `GachaId` 키로 합치므로 **혼합 풀도 가능하다**(테스트 있음).

- **풀 메타를 `GachaInfoTable`로 두고 자식 시트가 참조한다.** T-027 원안은 반대 방향
  (`GachaInfoTable.GachaId → Ref: GachaTable.GachaId`)이었는데, 시트가 둘로 갈려
  **메타가 한쪽만 가리킬 수 없다.** 방향을 뒤집어 두면 *메타 없는 풀*이 생성 단계에서 막힌다 —
  메타가 없으면 비용이 없고, 곧 그 풀만 공짜가 된다.

- **`GachaInfoTable`의 키 이름은 `GachaInfoTID`인데 자식 시트 컬럼은 `GachaId`다.**
  이름이 다른 것은 의도다 — TID 규칙(`excel-table-creator`)은 첫 컬럼이 `<시트명>TID`여야
  한다고 못 박고 있고, `CharacterTable.Farming → WorkSpeedTable.WorkSpeedTID`처럼
  **이름이 다른 `Ref`는 이미 선례가 있다.**

- **등급 = 최고 적성의 상한이지 총합이 아니다.** 슬롯에 한 명만 들어가므로 체감되는 값은
  그 산업의 적성 하나뿐이다. Mythic(`1030`)을 **전 산업 7**로 두고 단일 산업 1위는
  Legendary(9)에 남긴 것도 같은 이유다 — 만능 1위를 만들면 배치가 "그 캐릭터부터"로 수렴한다.
  → `GameDesign/design/character/README.md` 1.2

- **`GrantCharacterRepository`를 복수 TID + `CharacterGrantReason`으로 넓혔다.**
  10연차가 캐릭터 10장을 주는데 장당 DB 왕복을 하면 `S_CharacterListResponse`가
  열 번 쪼개져 내려간다. 지급 SQL을 두 벌로 나누지 않으려고 **후속만 이유로 갈랐다**
  (`Login` → `FinishLogin` / `Gacha` → 목록 재하달).

- **`GachaService`에 카탈로그 주입 생성자를 뒀다.** `Singleton<T>`라 테스트가 전역
  등록 상태를 공유하면 순서에 따라 새어 나간다. `User`의 `dropTables ?? Instance`와 같은 규약.

## 후속 작업 / 주의사항

- ⏳ **더미 클라 실물 확인이 남았다.** 뽑으려면 골드가 있어야 하는데 이 브랜치에는
  골드 획득 경로가 없다 → `tasks/T-028-즉시판매.md`. (테스트용 `C_AddCurrencyRequest`를
  한 번 넣었다가 **되돌렸다** — main에서 T-028이 같은 `PacketId 19`를 쓰고 있어 충돌한다.
  급하면 `Server/Shared/game.sqlite3`의 `t_user_currency`에 직접 넣는 편이 낫다.)

- ⚠️ **이 브랜치는 `71fdaa4`에서 갈라졌고 main이 앞서 있다.** 합칠 때 겹치는 곳:
  `MikaProtocol/MikaPacket.cs`(PacketId 대역) · `MikaProtocol/PacketEnum.cs`(`EResultCode` 100번대) ·
  `tasks/README.md` · `GameDesign/design/trade/README.md`.
  `EResultCode`는 **겹치지 않는다** — T-028이 상점 대역 `300~`을 새로 팠고 이쪽은 가챠 `102`다(확인함).
  `PacketId`는 T-028이 `19`·`20`을 쓴다 — 이 브랜치는 **패킷을 추가하지 않는다.**

- ⚠️ **`GachaTable`이라는 이름은 이제 없다.** 상자 개봉(T-029)이 "기존 `GachaTable` 풀을
  재사용한다"고 적어 두었는데, 그 자리는 `GachaItemTable`이고 **`GachaInfoTable`에
  메타 행이 없으면 뽑히지 않는다.**

- **가챠 천장·확정 지급은 여전히 없다.** 적성 N을 확정적으로 얻는 길이 없어
  산업 레벨 해금(`gathering/산업레벨.md` 3장)이 운에 걸린다 — 새 미정으로 등재했다.
