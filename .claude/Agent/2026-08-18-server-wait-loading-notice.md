---
date: 2026-08-18
title: 서버 왕복 대기 로딩·실패 알림·연결 끊김 종료 (A-2 + A-5)
tags: [client, ui, network]
---

# 서버 왕복 대기 로딩·실패 알림·연결 끊김 종료 (A-2 + A-5)

## 목적 / 배경
- `먼저 할일.MD`의 A-2(실패 알림)·A-5(대기 로딩·타임아웃·오류)를 함께 처리.
- 지금까지 서버 왕복 구간이 화면에 조용했다 — 요청 후 버튼만 잠기고, 실패·타임아웃·연결 끊김은
  콘솔 로그로만 남고 버려졌다(`EResultCode`를 `PlayerDataModel`이 로그 후 폐기).
- 서버 폴더는 담당이 달라 건드리지 않고, 기존 요청/콜백/타임아웃 배선 위에 화면 표시만 얹었다.

## 변경 내용
- 신규 `UI/System/` 오버레이 캔버스 — `SystemCanvasView`·`ResultMessages`·`LoadingPresenter`·`NoticePresenter`.
- 신규 `Managers/ServerWaitManager`(`MonoService`) — 대기·알림의 단일 창구. `Begin/Succeed/Fail`,
  5초 단일 타임아웃 상수, `BusyChanged`·`NoticeRaised`·`FatalRaised` 이벤트, `RaiseFatal`.
- `PlayerDataModel` — `LoginCompleted`·`WorkStationAssignCompleted`를 `(bool, EResultCode)`로 확장,
  `GachaFailed(EResultCode)` 신설(실패 사유를 이벤트에 실어 보냄).
- 구독자 시그니처 파급 반영 — `PlayerDataLogger`·`StatePresenter`(`using MikaProtocol;` 추가).
- `LoginPresenter`·`WorkStationSelectPresenter`·`GachaPresenter` — 자체 타임아웃/미대응을 걷어내고
  `ServerWaitManager.Begin`으로 위임, 응답 이벤트에서 `Succeed`/`Fail(ResultMessages.ToText(code))`.
- `PingManager` — 15초 무응답 시 `RaiseFatal` 호출. `_everConnected`로 "최초 접속 실패"와
  "도중 끊김" 문구를 가른다.

## 주요 결정 / 근거
- **`ServerWaitManager`는 콜백 수집기가 아니다.** 서버 패킷을 받지 않는다 — Presenter가 요청 직후
  대기를 열고, 자기 응답 이벤트에서 결과만 보고한다. 무응답(아무 보고 없음)만 타이머로 잡는다.
  핑(연결 생사)과 축이 다르다. 둘이 만나는 곳은 하나 — 핑이 끊김을 판정하면 알림 창구로 `RaiseFatal`을 빌린다.
- **Managers→UI 역방향 의존 금지 준수** — 매니저는 문자열만 다룬다. `EResultCode→문구`(`ResultMessages`)는
  UI 폴더에 두고 Presenter가 호출한다.
- **Loading/Notice는 자기 자신이 아니라 자식 `panel`을 켜고 끈다** — 자기를 끄면 다시 켤 이벤트를 못 받는다.
- **최초 접속 실패 감지를 클라 쪽 `PingManager` 15초 경로로 통합** — `NetworkManager`(서버 담당 폴더)를
  건드리지 않고, 접속 못 함(Pong 한 번도 없음)과 도중 끊김을 같은 경로로 잡는다.
- **작업슬롯 "무응답 영구 잠김" 버그 동시 해결** — 타임아웃 `onClosed`가 잠금을 푼다.
- 타임아웃은 요청별 단일 5초 상수 1개(연결 판정 15초와 별개).

## 후속 작업 / 주의사항
- **에디터 수작업 남음(핸드오프 프롬프트 참조):** `!Loding Canvas`→`!System Canvas` 개명 + `SystemCanvasView`
  부착 + Override Sorting/Sorting Order 300/GraphicRaycaster, `Loding Presenter`→`Loading Presenter`,
  `Alarm Presenter`→`Notice Presenter`, 각 참조(panel/text/button) 배선, `ServerWaitManager` 컴포넌트를 씬에
  올리기, 새 `.cs`의 `.meta` 생성.
- `game.sqlite3`는 커밋에서 항상 제외(수동 add).
- `OnAssignCompleted`는 `Succeed/Fail`이 `onClosed`로 `_pending`을 지우므로 종류를 먼저 붙잡는다(순서 의존).

## 업데이트 (2026-08-18) — 에디터 배선 완료(MCP)
- `!System Canvas`: Sorting Order 300, Override Sorting, GraphicRaycaster 유지. **상시 딤이던 캔버스 Image는 비활성화**하고
  딤을 각 패널로 내렸다 — 캔버스가 항상 켜져 있어도 유휴 시 화면을 가리거나 입력을 막지 않게 하기 위함.
  (UIManager는 이 캔버스를 관리하지 않는다 = 다른 화면과 독립 오버레이.)
- 구조: `Loading/Notice Presenter`(항상 켜짐·스크립트) → `Panel`(토글·전체 딤 Image 39% 흰색, raycast on=모달) → `Box`(기존 박스).
  프레젠터가 자기 자신이 아니라 `Panel`을 토글한다. Notice가 Loading보다 형제 순서 뒤(위에 그려짐).
- 배선: Loading.panel / Notice.panel·messageText·closeButton 모두 연결. `ServerWaitManager`는 `ServerWait Manager` 오브젝트에 이미 존재.
- 씬 저장·컴파일 에러 0. `.meta`는 컴파일 시 자동 생성됨 — 스크립트 원본과 함께 커밋할 것.
