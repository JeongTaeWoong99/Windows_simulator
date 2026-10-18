---
id: T-172
제목: 플레이 중 VisualCatalog.HeadOf NRE · 이후 서버 콜백이 안 온다
담당: 태웅
상태: Closed
우선순위: 높음
목표: 261024
---

# T-172 플레이 중 VisualCatalog.HeadOf NRE · 이후 서버 콜백이 안 온다

## 배경

게임을 하다 보면 `VisualCatalog.cs:156`에서 `NullReferenceException`이 뜬다 (2026-10-19 진우 제보).
**그 뒤로 서버 응답 콜백이 오지 않는다** — 화면이 서버 결과를 받지 못해 멈춘 것처럼 보인다.

156줄은 위젯 미니 슬롯의 캐릭터 머리 그림이다.

```csharp
public static Sprite? HeadOf(int characterTid) => Current != null ? Current.GetCharacter(characterTid).Head : null;
```

- 부르는 곳: `WidgetMiniSlotView.cs:77` (`VisualCatalog.HeadOf(characterTid)`)
- `GetCharacter`는 `FindCharacter(tid) ?? fallbackCharacter` — 대체 그림까지 null이면 여기서 터진다
- 저장소의 `VisualCatalog.asset`에는 `fallbackCharacter`(medieval-warrior)가 걸려 있다 → 다른 원인일 수 있다.
  캐릭터 그림 27종을 넣은 `e09d37ef` 이후에 생겼는지 본다

[이슈 #80](https://github.com/JeongTaeWoong99/Windows_simulator/issues/80)

**콜백이 끊기는 것이 더 큰 문제다.** 패킷 처리 흐름 안에서 예외가 나 콜백 디스패치가 멈췄을 가능성이 있다 —
그림 하나가 없다고 네트워크가 죽으면 안 된다.

## 스택 트레이스 (2026-10-19 진우)

```
NullReferenceException
VisualCatalog.HeadOf (VisualCatalog.cs:156)
WidgetMiniSlotView.Bind (WidgetMiniSlotView.cs:77)
WidgetPresenter.Rebuild (WidgetPresenter.cs:281)
PlayerDataModel.OnWorkStationAssigned (PlayerDataModel.cs:955)
ServerPacketHandler.Handle_S_WorkStationAssignResponse (ServerPacketHandler.cs:391)
NetworkMessageQueue.Flush (NetworkMessageQueue.cs:17)
```

**콜백이 끊기는 이유는 이걸로 설명된다.** 작업슬롯 배치 응답에서 `OnWorkStationAssigned`가
`WorkStationSlotsChanged?.Invoke()` → 위젯 `Rebuild`에서 예외가 나면
- 같은 이벤트의 **뒤 구독자**가 불리지 않고
- 그 아래 줄 **`WorkStationAssignCompleted?.Invoke(...)`가 실행되지 않는다** → 배치 결과를 기다리는 화면이 응답을 못 받는다

네트워크 큐(`NetworkMessageQueue.Flush`)는 예외를 잡아 로그만 남기고 다음 패킷을 계속 돌린다 — 연결이 죽은 것은 아니다.
`HeadOf`가 왜 null을 만나는지(`fallbackCharacter`가 런타임에 비는지)는 에디터에서 확인이 필요하다.

## 할 일

- [x] 재현 — 작업슬롯 배치 응답 → 위젯 갱신 경로 (위 스택 트레이스) · `characterTid` 값은 아직 모름
- [x] `HeadOf` NRE 수정 — 대체 그림까지 빠져도 null로 버틴다(`PortraitOf`·크기 등급·배경 함께). 근본 원인은 미확정 — 아래 "원인 조사"
- [x] 예외 뒤 서버 콜백이 안 오는 원인 — 변경 알림이 첫 구독자 예외에서 멈춰 뒤의 완료 알림이 빠졌다. `PlayerDataModel.Notify`로 구독자마다 따로 부른다
- [x] 실플레이 확인 — NRE 없음 · 오래 플레이해도 응답이 계속 온다 (2026-10-19 사용자 확인)

## 원인 조사 (2026-10-19)

- 아트 저장소의 `fallbackCharacter`는 이력 내내 걸려 있었다(black-knight → `99b5cc0`부터 medieval-warrior). **한쪽 저장소만 받은 경우로는 재현되지 않는다**
- 추정: 도메인 리로드 꺼짐(`m_EnterPlayModeOptions: 0`)에서 `VisualCatalog.Current` 정적 캐시가 플레이를 넘어 남고, 에디터를 켠 채 아트를 받으면 낡은 참조를 쥔다 → 플레이 시작마다 비우게 했다
- **결론: 받는 순서는 언제든 꼬일 수 있다 — 고칠 것은 방어 코드 쪽이다.** 그림이 비어도 자리 표시로 그리고, 화면 하나의 예외가 응답 흐름을 끊지 않는다

## 완료 조건

플레이 중 `VisualCatalog` NRE가 나지 않고, 화면 쪽 예외가 나도 이후 서버 콜백은 계속 온다.

## 막고 있는 것 / 선행 일감

- 없음

## 관련 커밋

- `ac38ca7d` fix: 그림이 비거나 화면이 터져도 서버 응답이 끊기지 않게 한다
- 사용자 실측 확인 (2026-10-19) · #80 닫힘
