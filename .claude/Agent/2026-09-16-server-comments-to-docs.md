---
date: 2026-09-16
title: 서버 코드의 긴 설계 근거 주석을 Server/docs/로 분리 — 코드엔 1~2줄 + 링크만
tags: [server, docs, refactor]
---

# 서버 코드의 긴 설계 근거 주석을 Server/docs/로 분리

## 목적 / 배경
- `WorkStationSlot.cs` 264줄 중 절반이 XML 설계 근거라 로직을 찾으려면 스크롤을 해야 했다.
  같은 기준으로 서버 전체를 재서 주석 비율 40%↑·연속 주석 15줄↑인 파일을 함께 정리했다.
- `server-code-style` 규칙(주석 1~2줄)에도 맞춘다. 근거는 버리지 않고 문서로 옮겼다.

## 변경 내용
- 새 문서 `Server/docs/`
  - `채취-정산.md` — WorkStationSlot · WorkSpeed · GatheringScheduler · Global.GatherSpeedMultiplier · User.SettleWorkStation 호출 시점 · WorkStationSlotInfo 패킷
  - `세션-감시.md` — Global.SessionIdleTimeout(핑 ≤ 판정÷3 불변식, 이슈 #19) · SessionWatchdog(이슈 #10)
  - `데이터-카탈로그.md` — DropTableCatalog · IndustryLevelCatalog · GachaPoolCatalog · WeightedPicker · GachaRewardInfo
  - `테스트커버리지.md` 7장 — IClientChannel을 ISession에서 잘라 낸 이유
- 코드 주석 축약(14파일): WorkStationSlot(264→177줄) · WorkSpeed · Global · GatheringScheduler · SessionWatchdog · IClientChannel ·
  DropTableCatalog · IndustryLevelCatalog · GachaPoolCatalog · WeightedPicker · User.WorkStation · PacketInfo(MikaProtocol → Unity 미러 자동 갱신) ·
  CurrencyRepository · Character · ServerLog
- `Server/CLAUDE.md` docs 표에 새 문서 3개 등록.

## 주요 결정 / 근거
- **"왜 주기가 아니라 속도인가"·정산 의사코드는 기획 문서(`GameDesign/design/workslot/README.md` 2~3장)가 이미 원본**이라
  서버 문서에 다시 쓰지 않고 링크만 걸었다. 서버 문서에는 구현에서 정한 단위·상태 경계·불변식·함정만 담는다.
- 문서는 클래스별이 아니라 **주제별 3개**(채취 정산 / 세션 감시 / 데이터 카탈로그)로 묶었다. 클래스별로 쪼개면 같은 근거(밀리초 이월, 정산→변경 순서)가 여러 문서에 반복된다.
- 근거가 한두 문장으로 끝나는 블록(CurrencyRepository·Character·ServerLog)은 문서 없이 `//` 2줄로만 줄였다.
- 코드의 링크 형식은 `→ Server/docs/<문서>.md N장` 한 가지로 통일했다.

## 후속 작업 / 주의사항
- 작업 중 같은 트리에서 다른 세션이 `DBManager.cs`·`AccountRepository.cs`·`User.cs`·`IRepository.cs` 등을 수정하고 있었다(내 변경 아님). 커밋 시 내 파일만 골라 담는다.
- 주석 비율 재집계 결과 이제 최대 34%·연속 블록 ≤10줄이다. 새 근거가 생기면 코드에 늘리지 말고 위 문서에 절을 더한다.
- 확인: `dotnet test` 291개 통과, 서버 빌드 경고 0.
