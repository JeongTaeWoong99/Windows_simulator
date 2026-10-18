---
id: T-172
제목: 플레이 중 VisualCatalog.HeadOf NRE · 이후 서버 콜백이 안 온다
담당: 태웅
상태: New
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

## 할 일

- [ ] 재현 — 어떤 화면·동작에서 뜨는지 · 스택 트레이스 전체 · 그때 `characterTid` 값
- [ ] `HeadOf` NRE 원인 확인 · 수정 (`PortraitOf`도 같은 모양이라 함께 본다)
- [ ] 예외 뒤 서버 콜백이 안 오는 원인 — 패킷 핸들러·콜백 디스패치가 예외 하나로 멈추는지 확인하고, 한 콜백의 예외가 다음 콜백을 막지 않게 한다
- [ ] 실플레이 확인 — NRE 없음 · 오래 플레이해도 응답이 계속 온다

## 완료 조건

플레이 중 `VisualCatalog` NRE가 나지 않고, 화면 쪽 예외가 나도 이후 서버 콜백은 계속 온다.

## 막고 있는 것 / 선행 일감

- 없음

## 관련 커밋

- 없음
