---
date: 2026-10-09
title: 클라 ServerClock과 치트 창 시간 칸 (T-106 · 이슈 #48)
tags: [client, cheat, clock]
---

# 클라 ServerClock과 치트 창 시간 칸

## 목적 / 배경
- 서버가 게임 시계(`GameClock`)·`AdvanceTime=12`·`ResetTime=13`·`S_ServerTimeResponse`를 끝냈다(T-128). 클라 표시가 같은 시계를 따라가야 시간 치트로 만료·삭제·전일 평균을 시험할 수 있다.

## 변경 내용
- `Clock/ServerClock.cs` 신설 — 서버 시각 − PC 시각 차이 하나. `S_ServerTimeResponse` 구독
- 서버 시각과 비교하던 4곳(`AuctionText.FormatRemaining` · `MailPresenter.FormatDeleteIn` · `WorkStationProgress.GetPendingUnits` · `PlayerDataModel` 우편 받은 시각)을 `ServerClock`으로
- `CheatWindow.DrawTime` — +1시간·+1일·+2일·+7일 · 초 입력 · 되돌리기 · 지금 넘긴 양
- 문서: `Clock 규칙.md` 신설 · `폴더 구조.md` · `cheat-console 규칙.md`

## 주요 결정 / 근거
- 일감엔 `ServerClock(Managers)`라 적혀 있었지만 **정적 클래스 + `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`** 로 했다. 쓰는 쪽이 전부 정적 헬퍼라 `Services.Get`·씬 오브젝트 추가가 비용만 늘린다. `Managers/`는 MonoService 전용이라 새 폴더 `Clock/`
- `AuctionModel`의 `DateTime.Now`(받은 지 몇 초)는 클라 안 경과라 그대로 둔다

## 후속 작업 / 주의사항
- 2026-10-09 사용자 실측 완료 — T-106 보관 · 이슈 #48 종료
