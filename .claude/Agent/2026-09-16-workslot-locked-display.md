---
date: 2026-09-16
title: 잠긴 작업슬롯 표시 · 해금 확인 팝업 (패킷 자리는 TODO)
tags: [client, ui, unlock, workstation]
---

# 잠긴 작업슬롯 표시 · 해금 확인 팝업 (T-039 1단계)

## 목적 / 배경
- 해금 기획·데이터(`UnlockTable`·`WorkSlotTable`)는 들어왔지만 서버 패킷(T-038)이 없다.
- 8칸이 전부 "비어있음."으로 보였고, 잠긴 칸도 선택 화면까지 들어간 뒤 `InvalidSlotIndex`로 거절됐다.

## 변경 내용
- `WorkStationListPresenter` — 프레임 라벨에 칸 상태(잠김 조건 / "비어있음.") 표시, 잠긴 칸 클릭은 해금 흐름(선행 → 골드 → `AskConfirm` → 지금은 안내)
- `PlayerDataModel.IsUnlocked` — 지금은 `UnlockTID == 0`만 true
- `GameDataLoader` — 슬롯 해금 TID · 해금 행 · 해금→칸 번호 조회
- 신규 `ConfirmPresenter` + `UIManager.AskConfirm` · 씬 `!System Canvas`에 `Confirm Presenter` (Amount Input 복제 후 입력칸 제거)
- `TODO(T-038)` 위치: `PlayerDataModel`(이벤트·구독·응답 처리) · `ServerPacketHandler` · `WorkStationListPresenter`(구독·`RequestUnlock`·결과) · `ResultMessages`

## 주요 결정 / 근거
- 열림 판정을 슬롯 스냅샷 유무가 아니라 테이블 + `IsUnlocked`로 했다 — 서버 설계(열린 목록 → `IsUnlocked`)와 같은 모양이라 패킷이 오면 `IsUnlocked` 내부만 바뀐다.
- 프레임 라벨은 View 스크립트를 새로 두지 않고 `BindFrameButtons`에서 `GetComponentInChildren<TMP_Text>`로 잡는다 — 슬롯 뷰가 생기기 전이라 텍스트가 라벨 하나뿐이다. **이 호출 순서(`Rebuild`보다 앞)를 바꾸면 슬롯 뷰의 텍스트를 잡는다.**
- 선행·골드 부족은 확인 팝업 대신 알림(`RaiseNotice`) — 확인할 것이 없어서.

## 후속 작업 / 주의사항
- **1번 칸은 열린 빈 칸으로 보이지만 배치는 서버에서 거절된다** — 서버 `DefaultSlotCount = 1`, `EnsureDefaultSlots`는 슬롯이 하나라도 있으면 건너뛴다. T-038 일감에 메모함.
- 계정 레벨 검사는 클라에 값이 없어 생략(데이터도 전부 0).
- 서버가 꺼져 있어 플레이 모드 확인은 못 했다 — 컴파일·씬 배선까지만 확인.

## 업데이트 (2026-09-16) — 해금 패킷 연결 · stash 병합
- 위 작업이 pull 충돌로 GitHub Desktop stash에 들어간 사이 서버가 T-038(`39ca310`)을 끝냈다.
  stash에서 겹치지 않는 파일만 `git checkout stash@{0} -- …`로 꺼냈다.
  겹친 5개 중 `ServerPacketHandler.cs`·`MikaSourceGen.dll`·`game.sqlite3`는 main 쪽을 남겼고, `Agent/README.md`·`T-039`는 손으로 합쳤다.
  stash의 `tasks/T-038` 수정은 버렸다(archive로 이동됨 · 메모 내용은 서버가 해결).
- `PlayerDataModel` — `_unlockedTids` + `UnlocksChanged`·`UnlockCompleted`. **해금 결과는 치트·퀘스트로도 오므로** 목록 갱신은 요청 여부와 무관하게 한다.
- `WorkStationListPresenter` — `C_UnlockRequest{UnlockTID, Currency}` + `_unlockWait`. 대기 핸들이 없을 때 온 결과는 문구를 띄우지 않는다.
- 선행 표시는 `UnlockTable.Name` — 기획 #13("클라가 콘텐츠 테이블을 역색인하지 않는다")에 맞춰 `GameDataLoader.FindWorkSlotIndexOfUnlock`을 지웠다.
- 1번 칸 배치 거절 문제는 서버가 로그인 시 `WorkSlotTable`로 칸을 만들게 바뀌어 해결됐다(기존 계정 포함).
- 실서버 플레이 확인은 아직 — T-039 `진행중`.
