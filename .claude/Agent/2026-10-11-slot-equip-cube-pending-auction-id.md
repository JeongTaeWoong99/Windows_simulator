---
date: 2026-10-11
title: 슬롯 장비 큐브 표시 · 상급 큐브 대기 강조 · 경매 매물 ID 충돌 거부
tags: [client, ui, server, auction]
---

# 슬롯 장비 큐브 표시 · 상급 큐브 대기 강조 · 경매 매물 ID 충돌 거부

## 목적 / 배경
- 6개 항목 요청 중 구현분(1·4·5). 2(목업)·3(제거 전 목록)·6(이슈 초안)은 확인 대기라 코드 변경 없음.

## 변경 내용
- 1번 — 서버 변경 없음. `EquipInfo.EnchantGrade`·`EnchantOptions`·`PendingEnchantGrade`가 이미 로그인 스냅샷·동기화로 온다.
  - 슬롯 설정 장비 칸: `EquipSlotButton.enchantLabel`(선택) + `EquipLabel.GetEnchantLines`. 씬에 `Enchant Text (TMP)` 4개, `Equipment Panel` LayoutElement preferredHeight 114 → -1(min 114 유지 — 큐브 줄 수만큼 칸이 자란다).
  - 슬롯 목록 장비 아이콘: `EquipPipsView.gemViews`(선택) + `EnchantGemView.Show(grade)`. `EquipIconsView.prefab`에 7px 보석 4개.
- 4번 — 원인은 쿼리·렌더링이 아니라 **listing_id 충돌**. 메인 DB가 비워져 trade_id가 1부터 다시 났는데 `auction.sqlite3`에 옛 매물 1~19가 남아 있었다. 등록이 `ON CONFLICT DO NOTHING` + `RegisterResult.Ok`로 삼켜졌다. 캐릭터(trade 6)는 우연히 빈 ID 6이라 보였다.
  - `AuctionStore.IsSameListing` + `AuctionEngine.RegisterAsync`: 삽입 실패 시 판매자·종류·TID가 다르면 `Rejected` → 메인이 바로 우편 반환.
- 5번 — 접속 알림(`PlayerDataModel.NotifyPendingEnchants`, 장비 스냅샷마다), 인벤토리 장비 칸 금색 테두리 깜빡임(`SlotView.attentionFrame` CanvasGroup + `InventorySlotSource.NeedsAttention`), [장비] 탭 배지(`InventoryTabPresenter.equipPendingBadge`), 슬롯 설정 카드 '결과 선택 대기' 줄.

## 주요 결정 / 근거
- 메뉴 줄 [인벤토리] 버튼에는 배지를 달지 않았다 — `MenuPresenter` 주석의 P1(상주 창 버튼에 배지 금지). 열 밖은 접속 알림이 맡는다.
- 장비 칸 큐브 글자는 등급색으로 칠하지 않았다 — 칸 바탕이 장비 등급색이라 같은 색이 겹친다.
- 경매 충돌을 메인 쪽 대사에서 잡지 않고 경매장 등록에서 거부했다 — 대사(`GetListingStates`)는 ID로만 물어 충돌한 매물이 Listed면 영원히 정상으로 본다.

## 후속 작업 / 주의사항
- ⚠️ **씬은 저장하지 않았다** — 작업 전부터 씬에 저장 안 된 변경이 있었다. 1번 `Enchant Text`·5번 `Pending Badge` 배선은 에디터에만 있다. 확인 후 저장할 것.
- 경매 데이터 복구 필요: 메인 trade 5(장비 1205, 신화 인챈트)는 메인에서 state 1인데 경매장 ID 5는 다른 판매자(4)의 매물이다 — 대사로도 안 돌아온다. 경매장 서버 재시작(수정 반영) + `auction.sqlite3` 정리(메모리 `auction-db-reset-pair` 절차) 필요.
- 실행 중인 AuctionServer가 DLL을 잠가 테스트는 `--artifacts-path`로 따로 빌드했다(114 + 151 통과).

## 업데이트 (2026-10-11) — 2차 요청

- **획득 연출 멈춤** — 연출 중 화면이 바뀌면 칸이 꺼지며 반납이 다음 재생까지 미뤄지는데, `ItemGainFx.HideAll`이 아이콘만 안 꺼 반쯤 흐린 아이콘이 남았다. 아이콘도 끈다(`Build`가 다시 켠다).
- **작업슬롯 칸 A안**(위젯은 그대로) — `WorkStationSlotView` 필드 `indexText`·`industryText`·`speedText`·`characterText`. 남은 시간 글자 삭제(`WorkStationProgress.CalculateRemainSeconds`는 이제 안 쓴다 — 남겨 둠).
  프리팹: `Head Panel`(칩 2 + 배속) · `Stage Frame`(무대 + `Stage Overlay` — 이름·4px 막대) · `Foot Panel > Equip Icons`(왼쪽). 루트 VLG `childForceExpandHeight` 끔 — 켜 두면 머리·발 줄이 68px로 늘었다.
- **ESC 종료 삭제** — `WindowManager.Update`. Shift+우클릭 경매 등록은 유지. `using UnityEngine.InputSystem`은 `Mouse` 때문에 남겼다.
- **상급 큐브 대기** — 주의 테두리를 빨강(`Negative`)으로, 깜빡이는 동안 레벨 배지 바탕도 빨강. 큐브 창이 열려 있으면 그 장비 말고는 걸러진 것처럼 흐리게(`EquipEnchantPresenter.TargetChanged` → 격자 다시 그림).
- **가방 가득 연출** — 슬롯·위젯 획득 줄 끝에 자루 + 빨간 X(`Gain.BagFull` · `ItemGainFx.BuildBagFull` · 그림 `Art/fx/bagfull/` 임시 — `make_fx.py bagfull`).
  서버가 버린 몫을 알리지 않아 **클라 추정**이다(`ReadGains` — 판정 있는데 받은 것 0 / 받은 아이템이 `MaxStack` 도달). 우편·상자·뽑기의 칸 부족은 이미 팝업이 있어 손대지 않았다.
  후속 이슈 [#67](https://github.com/JeongTaeWoong99/Windows_simulator/issues/67) (기획·서버) — 받지 못한 목록 필드 · 우편/상자 `MaxStack` 검사 · 2단계 여부. T-145에 링크.

### 주의
- `Assets/Art`는 별도 저장소다 — `fx/bagfull/`·`VisualCatalog.asset`·`make_fx.py` 변경은 그쪽에 있다(같은 저장소의 다른 SO 변경은 사용자 것).
- 가방 가득 추정의 빈틈: 이미 9,999인 아이템이 다른 아이템과 같이 나오면 못 잡는다 — #67 필드가 오면 교체(`ReadGains` TODO).

## 업데이트 (2026-10-11) — 3차 요청

- **상급 큐브 대기 표시** — 레벨 배지 자리(26×14)의 작은 `선택` 글자를 없애고, 칸 아래쪽에 칸 폭만큼 빨간 띠 `선택 대기`(15 bold)를 깐다(`SlotView.attentionBadge`). 테두리 깜빡임은 그대로이고, 띠는 글자가 읽혀야 해서 깜빡이지 않는다. `EquipSlotSource.GetBadge`는 쓰지 않게 됐다.
- **빈 보석** — `EnchantGemView.EmptyColor`를 반투명 검정(35%)에서 불투명 검정으로. 일반 등급(회색)과 구분된다.
- **작업슬롯 칸** — 목업대로 아래 줄 왼쪽에 남은 시간(`remainText`)을 되살리고 장비는 오른쪽으로. 진행 막대는 초록(`Positive`) · 높이 6 · 칸 폭 끝까지(루트 여백 5를 밖으로). 번호는 `#1`부터(`DisplayNumber`) — 인벤토리 캐릭터 툴팁 '슬롯 N'과 슬롯 설정 제목도 같이 바꿨다.
- **위젯 칸** — 막대만 칸 바닥·좌우에 붙였다(여백 3 → 0). 남은 시간 글자는 넣지 않는다.
- **키 입력** — `Managers/KeyInputManager` 새로 만듦(씬 `Key Input Manager`). 아웃게임(로그인 Enter · UI 키 이동) / 인게임(Shift+우클릭) 두 구역이고 로그인 성공으로 인게임이 된다. 인게임에서는 `EventSystem.sendNavigationEvents`를 끈다. 거래소·경매 검색칸 Enter 검색은 지웠다. 키 표는 `Managers 규칙.md` 2장.
- **경매장 DB 초기화** — AuctionServer를 멈추고 `auction.sqlite3`(+wal/shm)를 스크래치패드에 백업한 뒤 지우고, 다시 빌드해서 띄웠다. 메인 `t_auction_trade` 5건은 모두 `Returned`(3)라 남은 물건은 없다.

### 주의
- ⚠️ **씬 저장 필요** — `Key Input Manager`가 씬에만 있다. 저장하지 않고 씬을 다시 열면 `LoginPresenter`의 `Services.Get<KeyInputManager>()`가 실패한다.
