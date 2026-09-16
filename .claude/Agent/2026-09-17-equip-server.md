---
date: 2026-09-17
title: 캐릭터 장비 시스템 서버 구현 — EquipTable · 장착/해제 · 속도 가산 (T-002)
tags: [server, data, protocol, test]
---

# 캐릭터 장비 시스템 서버 구현 (T-002)

## 목적 / 배경
- 캐릭터 4칸(무기·장신구2·보석)에 유저 소유 장비 개체를 장착·해제하고, 가산이 채취 속도에 실제로 반영되게 한다.
- 설계 → `docs/superpowers/specs/2026-09-17-equipment-design.md`, 계획 → `docs/superpowers/plans/2026-09-17-equipment.md`. 무엇을 만들었는지는 `tasks/archive/T-002-장비슬롯.md`.

## 변경 내용
- 엑셀 `Enum.xlsx`(EquipKind·EquipSlot)·`Equip.xlsx` → `EquipCatalog` → `t_user_equip`·`t_character_equip` → `User.Equip.cs` → `ResolveSlotSpeed` 가산 → 패킷 5개 → 치트 `GiveEquip`.
- 커밋 `13792a5`~`4cd9213` (7개). 파일 단위는 일감 파일이 갖는다.

## 주요 결정 / 근거
- **종류(EquipKind)와 칸(EquipSlot)을 다른 enum으로 뒀다.** 장신구 칸이 둘인데 매핑 유일키가 `(character_id, slot)`이라 칸을 나눴을 뿐, 장신구는 두 칸 어디든 들어가야 한다. 테이블에 칸을 적으면 "Accessory1용 장신구"라는 없는 개념이 생긴다.
- **칸은 클라가 고른다.** 서버가 자동 배정하면 장신구 2개 중 무엇을 교체할지 유저가 못 정한다.
- **자동 이동은 매핑 변경 목록을 호출자가 만들고 저장소는 그대로 반영한다** — 저장소가 `equip_id` 기준으로 알아서 DELETE하지 않는다. 메모리(`_worn`)와 DB가 같은 규칙으로 움직여야 어긋났을 때 테스트(`EquipRepositoryTest.실패하면_전부_되돌아간다`)가 잡는다. 비우기를 전부 먼저, 넣기를 뒤에 — `UNIQUE(equip_id)` 순서 문제.
- **정산 → 매핑 → `RefreshWorkStationSpeed`** 순서. 배치 변경과 같은 이유(소급 방지). `UserEquipTest.장착_전에_먼저_정산한다`가 잠근다.
- **장비는 적성을 바꾸지 않는다** — 속도 가산만. 그래서 `CharacterInfo`는 그대로이고 T-022(적성 푸시)는 사유가 소멸했다(폐기 여부는 사용자 결정).
- 지급 대기 중 창고 칸(`_pendingEquipPositions`) — PK 발급 전에 연속 지급하면 같은 칸을 고르는 문제를 막는다. 치트만 쓰지만 값싸다.
- 정식 획득 경로·강화는 넣지 않았다(YAGNI). `GrantEquip`이 지급 진입점.

## 겪은 함정
- **Unity가 새 프로토콜을 자동 컴파일하지 않았다.** 미러가 복사돼도 에디터가 포커스를 잃은 상태라 재컴파일이 안 돼 `S_EquipListResponse(27)`를 조용히 버렸다. `AssetDatabase.Refresh(ForceUpdate)`를 eval로 부르고(5초 타임아웃 오류가 나도 리프레시는 돈다) 30초 기다린 뒤 플레이하니 됐다. 새 `.cs`·`.bytes`의 `.meta`도 같은 리프레시가 만든다.
- **플레이 직후 6초 만에 로그인을 보내면 소켓 연결 전이라 패킷이 사라진다.** 서버 로그에 `C_LoginRequest`가 없으면 이 경우다 — 연결된 뒤 다시 보내면 된다.
- `git checkout -- Server/Shared/game.sqlite3`가 `unable to unlink`로 실패했다(파일 잠금). 테이블별 대조로 HEAD와 데이터가 같음을 확인했고 바이너리만 페이지 배치가 다르다.
- 같은 트리의 다른 세션이 `SqliteFixture.CreatePlayerTables`를 동시에 추가했다. 로그인 조회가 장비 테이블을 읽게 되면서 그쪽 테스트가 깨져, `CreatePlayerTables`가 `CreateEquipTables`를 부르게 했다.

## 후속 작업 / 주의사항
- 클라: T-043(창고 장비 탭·착용 UI). `ServerPacketHandler`에 이벤트 3개(`EquipListReceived`·`EquipSynced`·`EquipResponded`)가 준비돼 있다.
- 서버: 장비 획득 경로 일감(상자/가챠/드롭) · T-058이 장비 탭 정렬·이동을 함께 · `Global.GatherSpeedMultiplier` 6.0은 여전히 확인용.
- 새 획득 경로는 반드시 `User.GrantEquip(tid)`를 부른다 — 창고 칸 배정·싱크가 거기 있다.
