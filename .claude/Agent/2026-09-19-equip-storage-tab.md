---
date: 2026-09-19
title: 창고 장비 탭 · 치트 장비 지급·정산 횟수 (T-043 장비 몫 · T-059)
tags: [client, ui, editor]
---

# 창고 장비 탭 · 치트 장비 지급·정산 횟수 (T-043 장비 몫 · T-059)

## 목적 / 배경
- 서버 T-060(`3299259`)이 장비 지급 치트 `GiveEquip = 8`을 냈는데, **클라에 장비가 들어올 자리가 없었다.**
  `S_EquipListResponse`·`S_EquipSyncResponse`를 구독하는 코드가 하나도 없어 치트를 써도 서버 로그로만 확인됐다.
- 장비가 화면에 보이는 첫 경로를 만드는 작업이다 — 치트로 넣고 창고 탭에서 본다.

## 변경 내용
- `Managers/PlayerDataModel.cs` — `Equips`·`EquipsChanged`·`GetEquipTid`·`IsEquipped` 신설, 장비 이벤트 2종 구독
- `Data/GameDataLoader.cs` — `GetEquipName`·`GetEquipRarity`·`TryGetEquip`
- `UI/Storage/StorageGridPresenter/EquipSlotSource.cs` (신규) + 등록 한 줄 · `Redraw`의 '배' 마크 분기
- `UI/Shared/SlotView.cs` — `assignMark` 주석·툴팁만 (프리팹·씬 무수정)
- `Editor/cheat-console/CheatWindow.cs` — `장비 지급` 칸 · `정산` 판정 횟수 슬라이더(1~100)
- 문서 — `Storage 규칙.md` · `UI 배치 현황.md` · `cheat-console 규칙.md` · `tasks/T-043`·`T-059`·신규 `T-067`

## 주요 결정 / 근거
- **T-043이 적어 둔 선행 "`ItemType` 축 결정"은 이미 해소돼 있었다.** T-002가 장비를 `ItemTable`이 아니라
  별도 `EquipTable`·별도 패킷으로 냈기 때문이다. **일감의 선행이 낡아 탭이 반년 잠겨 있을 뻔했다** —
  선행이 "풀렸는지"를 일감만 보고 판단하지 말 것.
- **장비 동기화는 캐릭터와 반대로 "없는 개체를 추가"한다.** `OnCharacterSynced`는 스냅샷이 원본이라
  목록에 없는 개체를 무시하는데, 장비는 `S_EquipSyncResponse`가 **지급 경로 그 자체**다(치트·가챠).
  캐릭터 쪽을 그대로 베끼면 치트로 준 장비가 조용히 사라진다.
- **칸 순서는 서버 `SlotPosition`.** 자원·캐릭터는 도착 순서(T-058 대기)인데 장비만 서버 위치가 이미 있다.
  둘을 맞추려 일부러 무시하기보다 있는 것을 쓰고, T-044에서 셋을 함께 정리한다.
- **'배' 마크를 장착 표시로 재사용했다.** 프리팹에 표시를 하나 더 두지 않기 위해서다 — 뜻은 다르지만
  둘 다 "지금 다른 데 나가 있다"라 사람이 읽는 법이 같다. 대신 `SlotView` 툴팁이 캐릭터 전용 문구여서 넓혔다.
- **장비 뽑기는 서버·엑셀 선행이라 손대지 않았다** — 가챠 풀이 아이템·캐릭터 두 시트뿐이고
  `GachaRewardType`에 `Equip`이 없다. → T-067 · 이슈 #27.

## 후속 작업 / 주의사항
- ⚠️ **'배' 마크(장착 중)는 미검증이다** — 장착 UI가 없어 장비를 낄 경로가 없다.
  장착 패킷(`C_EquipRequest`·`C_UnequipRequest`·`S_EquipResponse`)은 **서버·미러에 이미 다 있고 클라 구독자만 없다.**
- ⚠️ **치트 창의 `NoPermission` 팝업은 지금 재현되지 않는다** — 서버 `CheatAdminLevel = 0`이라 모든 계정이 통과한다.
  배포 전 1로 되돌릴 때 함께 확인한다(T-059 · T-060).
- 실플레이 확인이 남았다 → `tasks/T-059`. 확인 후 **이슈 #26을 닫는다.**
- `Server/docs/치트.md`의 "클라 입력 UI 미착수" 상태 줄은 서버 담당 문서라 고치지 않았다.
