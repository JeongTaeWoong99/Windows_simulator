---
date: 2026-10-19
title: 위젯 머리 그림 NRE · 배치 완료 콜백 끊김 (T-172)
tags: [client, ui, bug]
---

# 위젯 머리 그림 NRE · 배치 완료 콜백 끊김 (T-172)

## 목적 / 배경
- 이슈 #80 — 작업슬롯 배치 응답에서 `VisualCatalog.HeadOf` NRE → 그 뒤 배치 결과를 기다리는 화면이 응답을 못 받음 → `tasks/T-172`
- 스택: `Handle_S_WorkStationAssignResponse` → `PlayerDataModel.OnWorkStationAssigned` → `WidgetPresenter.Rebuild` → `WidgetMiniSlotView.Bind` → `HeadOf`

## 변경 내용
- `VisualCatalog.GetCharacter` — 반환을 `CharacterVisual?`로. 대체 그림이 빠졌으면 null. `HeadOf`·`PortraitOf`는 `?.`
- `CharacterSizeLabel.Of` — 같은 구멍이라 `?.` + `None`
- `PlayerDataModel.OnWorkStationAssigned` — 슬롯 갱신·`WorkStationSlotsChanged`를 try, `WorkStationAssignCompleted`를 finally

## 주요 결정 / 근거
- `FindCharacter(...) ?? fallbackCharacter`는 Unity null(빠진 참조)을 못 거른다 → `!= null` 비교로 바꿈
- 완료 알림은 화면 구독자의 예외와 무관하게 반드시 나가야 해서 finally. 예외 자체는 삼키지 않는다(큐가 잡아 로그를 남긴다)

## 후속 작업 / 주의사항
- NRE 근본 원인 미확정 — 아트 저장소의 `fallbackCharacter`는 이력 내내 걸려 있었다(black-knight → `99b5cc0`부터 medieval-warrior).
  저장소 한쪽만 받은 경우로는 재현되지 않는다. 추정: 도메인 리로드 꺼짐(`m_EnterPlayModeOptions: 0`) + 에디터 켠 채 아트 pull → 정적 `_current`·참조가 낡음. 진우 PC에서 에디터 재시작 후 재현 확인 필요
- 다른 `PlayerDataModel` 응답 핸들러도 "변경 이벤트 → 완료 이벤트" 순서면 같은 위험이 있다 — 이번엔 배치만 고침

## 업데이트 (2026-10-19) — 방어 코드 점검 확장

- 사용자 결론: 받는 순서는 꼬일 수 있다 → 최신 여부와 무관하게 방어 코드가 본 수정이다
- `PlayerDataModel` — 첫 수정의 try/finally를 걷고 `Notify(evt)`·`Notify<T>(evt, arg)`로 일반화.
  `XxxChanged` 34곳 + 뒤에 알림이 이어지는 `GatherResultReceived`·`MailRewardsClaimed`를 구독자별 try/catch로 부른다
  - 근거: 같은 "변경 알림 → 완료 알림" 모양이 장착·응축·우편·창고 등 거의 모든 응답에 있다. 핸들러마다 try/finally를 다는 것보다 한 곳에서 막는 편이 빠뜨림이 없다
  - 완료 알림(`XxxCompleted`)은 뒤에 이어지는 것이 없어 `?.Invoke` 그대로 둔다 — 예외는 큐가 잡는다
- `VisualCatalog` — `GetBackground`·`GetEmptySlotBackground`도 `?` + Unity null 거르기(`SlotStageView.Show`는 이미 null을 받는다).
  `ResetStatics`(SubsystemRegistration)로 플레이마다 `_current`·`_searched`를 비운다 — 도메인 리로드 꺼짐 대비
- ⚠️ `Notify`를 `?.Invoke()`로 되돌리면 T-172가 다시 난다
