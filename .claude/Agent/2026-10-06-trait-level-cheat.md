---
date: 2026-10-06
title: 특성 레벨 치트 SetTraitLevel (T-115 · #51)
tags: [server, test, docs]
---

# 특성 레벨 치트 `SetTraitLevel` (T-115 · #51)

## 목적 / 배경
- 레벨형 특성(T-108) 뒤로 특성이 `UnlockTable`에서 빠져 `Unlock` 치트로 특성이 안 오른다 → 산업 Lv5 확인에 계정 Lv50이 필요했다.
- 요청·인자 → [이슈 #51](https://github.com/JeongTaeWoong99/Windows_simulator/issues/51) · `tasks/T-115` · 명령 표 → `Server/docs/치트.md` 2장.

## 주요 결정 / 근거
- **번호 11** — #48(시간 치트)이 예시로 `AdvanceTime = 11` · `ResetTime = 12`를 적어 두었지만 미착수다. 먼저 온 이쪽이 11을 쓴다 — #48은 12부터.
- **결과는 `S_UserTraitListResponse` 스냅샷.** `0`(전 특성)이면 여러 개가 한꺼번에 바뀌고 내려가기도 해서 `Learn` 응답 한 개로는 표현이 안 된다. 클라는 목록을 통째로 갈아 끼우고, 목록에 없는 특성 = 기본 레벨이다.
- **기본 레벨로 내리면 메모리 딕셔너리에서 지우고, DB에는 기본 레벨 값으로 덮어쓴다**(행 삭제 아님 — 저장소가 upsert 하나뿐이라 그대로 썼다). 대신 `LoadTraits`가 **기본 레벨 행을 담지 않게** 바꿨다 — 안 그러면 다음 로그인 목록에 기본 레벨 항목이 섞여 "내리기 직후"와 달라진다.
- **정산 순서는 `TryLearnTrait`와 같다** — 속도·산출량 특성이 하나라도 바뀌면 바꾸기 *전에* `SettleWorkStation`, 속도면 바꾼 *뒤에* `ApplyWorkStationSpeed`. `RefreshWorkStationSpeed`를 부르면 정산이 한 번 더 돌 뿐이라 `Apply`만 직접 불렀다.
- **포인트는 돌려주지 않는다** — 치트는 포인트를 보지 않는다(`Unlock` 치트와 같은 성격).

## 후속 작업 / 주의사항
- ⚠️ **산업 개척을 내려도 이미 그 레벨로 돌던 슬롯은 그대로 돈다.** 산업 레벨 잠금은 배치(`AssignWorkStation`) 때만 보고, 로그인 적재(`LoadWorkStation`)도 산업 레벨을 다시 검사하지 않는다. 치트 전용 상황이라 두었다 — 정상 경로로는 특성이 내려가지 않는다.
- 클라 치트 창 묶음은 `tasks/T-117`(태웅).
- 실서버 확인은 T-117이 붙으면 함께 본다.

## 업데이트 (2026-10-06) — 클라 치트 창 (T-117 · #51 닫음)
- 치트 창에 '특성 레벨' 칸 — [전부 최대]는 테이블 최대 `MaxLevel`을 Arg2로 보내 서버 자르기에 맡긴다 · [전부 기본] = `0, 0` · 산업 띠 [산업 최대]는 특성마다 제 최대로 한 번씩.
- `UnlockTable`이 작업슬롯 6줄뿐이 되어 옛 세 묶음 접기·`ShowTraitUnlockGroups`·`GroupOf`/`IndustryOf`를 걷어 냈다. 일괄 전송은 `RequestAll(command, args)` 하나로 합쳤다(가드는 먼저 한 번만).
- 사용자 실측 확인(특성 화면 · 산업 레벨 잠금 즉시 반영) — T-115 · T-117 보관.
