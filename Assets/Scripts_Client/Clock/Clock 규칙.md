# Clock 규칙

> 최종 업데이트: 2026-10-09 (신설 — `ServerClock` · T-106 · 이슈 #48) · 대상: `Assets/Scripts_Client/Clock/`

**서버의 "지금"(게임 시계)을 클라에서 읽는 곳.** 지금은 `ServerClock` 하나다.

---

## 1. 서버가 준 시각과 비교하는 표시는 `ServerClock`으로

서버는 시간 치트(`AdvanceTime`·`ResetTime`)로 **게임 시계를 실제 시각보다 앞당길 수 있다**
([`Server/docs/치트.md`](<../../../Server/docs/치트.md>) "게임 시계" 절).
경매 만료 시각 · 우편 받은 시각 · 슬롯 `LastTickAtUnixMs`는 그 시계로 찍혀 오므로,
PC 시각(`DateTimeOffset.UtcNow`)과 빼면 넘긴 만큼 어긋난다.

| 쓴다 (`ServerClock.UtcNow` · `UtcNowUnixMs`) | 쓰지 않는다 (PC 시각 그대로) |
|---|---|
| 경매 남은 시간(`AuctionText.FormatRemaining`) · 우편 삭제까지(`MailPresenter.FormatDeleteIn`) · 채취 진행도(`WorkStationProgress`) · 받은 우편 시각 채우기(`PlayerDataModel`) | "몇 초 전에 받았나"처럼 **클라 안에서 잰 경과**(`AuctionModel`의 `…ReceivedAt`) · 로그 시각 · 핑 |

판단 기준: **서버가 보낸 시각과 빼거나 비교하는가?** 그렇다면 `ServerClock`이다.

## 2. 무엇을 들고 언제 맞추나

- 드는 값은 **서버 시각 − PC 시각** 하나다. 넘긴 시간과 PC 시계 오차가 함께 들어 있어, PC 시계가 틀린 경우도 덤으로 맞는다.
- 서버는 `S_ServerTimeResponse`를 **로그인 직후 · 시간 치트 직후**에 보낸다. 받기 전(로그인 전)은 차이 0 = PC 시각이다.
- 전송 지연(로컬 수 ms)은 보정하지 않는다 — 표시가 분 단위다.

## 3. 왜 정적 클래스인가 (`Managers/`가 아닌가)

쓰는 쪽이 전부 **정적 헬퍼**(`AuctionText` · `WorkStationProgress` · `MailPresenter`의 정적 메서드)라
`Services.Get`을 거치면 조회만 늘고 씬에 오브젝트를 하나 더 둬야 한다.
들 상태가 숫자 하나뿐이고 다른 서비스를 모르므로, `GameDataLoader`·`ClientLogger`처럼
`RuntimeInitializeOnLoadMethod`로 구독만 건다(`SubsystemRegistration` — 플레이마다 차이를 비운다).
