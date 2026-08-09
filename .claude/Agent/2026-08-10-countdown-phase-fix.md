# 2026-08-10 카운트다운이 결과보다 0.9초 빠른 문제 — 위상 오차 3종 수정 (이슈 #11)

## 배경

클라이언트 보고: 작업슬롯 카운트다운이 0에 닿고 **약 0.9초 뒤**에 `S_GatherResultResponse`가 온다.
어긋남이 **누적되지 않고 매 사이클 일정**하다 — 주기 계산은 맞고 위상만 어긋났다는 뜻이다.

서버 코드를 확인한 결과 원인이 셋이고 전부 사실이었다. 관측된 0.9초는 ①과 ②의 **합**이다.

## 원인과 수정

### ① `ToInfo()`가 밀리초를 잘랐다 — 주원인

패킷은 **(기준 시각, 그 시각의 진행도) 쌍**이고 클라는 이걸로 현재값을 복원한다.

```
지금 진행도 = ProgressUnits + (지금 − 기준시각) × 속도
```

`ToUnixTimeSeconds()`가 **시각만** 초 경계로 내리고 `ProgressUnits`는 그대로 뒀다.
"12:00:00.910의 진행도"를 "12:00:00.000의 진행도"라고 말한 셈 → 클라가 910ms어치를 더 갖는다.

**→ 필드를 `LastTickAtUnix`(초) → `LastTickAtUnixMs`(밀리초)로 개명.**

> **값만 ms로 바꾸고 이름을 두면 안 된다.** 클라의 `* 1000L`이 조용히 컴파일돼
> 1970년대 시각이 된다. 개명하면 컴파일 에러로 잡힌다 — 이게 개명한 이유다.
> MemoryPack 와이어 포맷은 위치 기반이라 필드명 변경으로 깨지지 않는다(타입·순서·개수 동일).

**대안으로 검토했다가 안 쓴 것:** 초는 유지하고 잘린 소수부만큼의 작업량을 `ProgressUnits`에
얹어 보내는 "초 경계 재기준화". 클라 무수정이 장점이지만, 패킷의 `ProgressUnits`가
서버 필드값과 달라져 디버깅 때 헷갈린다. 사용자가 개명 쪽을 선택했다.

### ② 푸시 해상도 1초 → 0.1초

판정이 완성돼도 다음 틱에야 감지·푸시된다. 보통 0~1초 랜덤 지연인데,
**주기 5초가 틱 1초로 나누어떨어져** 매 사이클 같은 값으로 고정됐다.

`GatheringScheduler.Interval` 1초 → **0.1초**. 정산량은 경과 시각이 정하므로 **나오는 개수는 그대로**다.
수확이 없으면 `SettleWorkStation`이 `harvests.Count == 0`에서 곧바로 빠져나가 패킷·DB 작업이 없다.

> 사용자가 처음 제안한 **60FPS 틱 + 슬롯마다 Task**는 반대했다.
> ⓐ 로직 스레드가 단일(`LogicExecutor`)인 게 이 서버의 동시성 모델인데, Task가
> `ProgressUnits`·`Inventory`를 만지면 **재화 생성 경로에 데이터 레이스**가 생긴다.
> ⓑ `ConsumeJudgeCount` 한 번은 ~100ns라 Task 스케줄링 오버헤드가 실제 일보다 수십 배 크다.
> ⓒ 30초 주기에 16ms 정밀도는 의미가 없다. (2026-07-29 `workstation-slot-design` 로그의
> "30fps 루프 반대 근거"와 같은 결론)

### ③ `LastTickAt`을 소비한 양보다 더 전진시켰다

```csharp
var elapsedMs = (long)(now - LastTickAt).TotalMilliseconds;  // 버림
LastTickAt    = now;                                          // 전부 전진 ← 버그
```

버린 1ms 미만이 진행도에 안 쌓이는데 시계는 지나갔다. **틱당 최대 1ms 영구 소실.**

```csharp
LastTickAt = LastTickAt.AddMilliseconds(elapsedMs);   // 정산한 만큼만
```

⚠️ **②와 묶어야 하는 이유가 여기 있다.** 손실이 틱 수에 비례하므로 1초 틱에서 시간당 최대 3.6초였던 게
**0.1초 틱에서는 시간당 최대 36초**가 된다. Interval을 줄이면서 ③을 안 고치면 더 나빠진다.

`AddMilliseconds`는 더하는 값만 ms로 반올림하고 `LastTickAt`의 1ms 미만 성분은 보존한다(정수라 정확).
`!IsActive` 분기의 `LastTickAt = now`는 그대로 뒀다 — 빈 슬롯의 시간은 **의도적으로** 버린다.

## 관측값 0.9초의 정체

```
gap = f + a
  f = 로그인 시각의 소수부   (①이 자른 값. 세션 고정)
  a = 로그인 후 첫 스케줄러 틱까지의 시간 (②. 세션 고정)
```

둘 다 세션 내내 상수라 어긋남이 누적되지 않는다. **클라 로그만으로는 0.90의 내역을 못 가른다** —
재로그인해서 값이 달라지면 `f`가 살아 있다는 뜻이다(①의 역검증 수단).

## 함께 정리한 것 — 폐지된 오프라인 정산 전제 주석 8곳

서버는 **로그인 시 정산을 부르지 않는다**(`User.WorkStation.cs:92`). 그런데 주석 곳곳이
오프라인 정산이 살아 있는 것처럼 서술하고 있었다. 동작에는 영향이 없지만 읽는 사람을 헷갈리게 한다.

| 위치 | 무엇이 틀렸나 |
|---|---|
| `MikaNetwork.Unity/ServerPacketHandler.cs` (3곳) | 로그인 수신 세트에 `S_GatherResultResponse`가 들어 있었다("네 개"→"세 개") · 인벤토리 스냅샷이 "정산 전 값"이라 보정이 필요하다는 서술 · "30초 주기로 밀어 준다" |
| `PlayerDataManager.cs` (2곳) | 같은 수신 세트 · 인벤토리 스냅샷 보정 서술 |
| `DropTable.cs` · `WeightedPicker.cs` (3곳) | `RollMany`/`PickMany`의 **존재 이유**를 "오프라인 정산이 접속 한 번에 수천 회 돌리므로"로 설명. 경로 자체는 살아 있으므로 근거만 실제 사용처(가챠 다연차·드롭 분포 검증 1M회)로 바꿨다 |

**"오프라인 진행은 폐지됐다"고 적은 근거 서술 9곳은 남겼다** (`Global.SessionIdleTimeout`,
`SessionWatchdog`, `WorkStationRepository`, `SessionIdleSweepTest` 등). 지우면 왜 그렇게 설계했는지가 사라진다.

**A-2 진단 코드**(`WorkStationScrollViewPanelUI.cs:245` 이하 "임시" 구역)는 이번 검증에 쓰이므로 남겼다.
클라 담당자가 확인 후 제거할 몫이다.

## 변경 파일

| 파일 | 내용 |
|---|---|
| `Server/MikaProtocol/PacketInfo.cs` | 필드 개명 + 쌍 정합성 주석 |
| `Server/WSGameServer/User/WorkStation/WorkStationSlot.cs` | `ToInfo()` ms · `LastTickAt` 전진 방식 · `LastTickAt` 프로퍼티 주석 |
| `Server/WSGameServer/Common/GatheringScheduler.cs` | `Interval` 0.1초 + 근거 주석 |
| `Server/WSGameServer/Common/SessionWatchdog.cs` | "매초"·"1초 vs 5초" 주석 정정 |
| `Server/MikaDummyClient/Network/ServerPacketHandler.cs` | 로그 필드명 |
| `Assets/Scripts_Client/UI/.../WorkStationScrollViewPanelUI.cs` | `* 1000L` 제거(2곳) |
| `Assets/Scripts_Client/Log/PlayerDataLogger.cs` | 로그 필드명 |
| `Server/WSGameServer.Tests/WorkStation/WorkStationSlotTest.cs` | 테스트 3건 추가 |
| `GameDesign/design/workslot/README.md` | 푸시 해상도 3곳 + 갱신일 |

`Assets/Scripts_Server/Protocol/PacketInfo.cs`는 빌드 post-build가 동기화했다(`copied=1`).

## 검증

- **red-green 확인** — 수정을 되돌리고 새 테스트 3건이 전부 실패하는 것을 봤다
  (`LastTickAtUnixMs` 1785326400 vs 1785326400910 / `judged` 0 vs 1). 복원 후 **194건 통과**.
- `check-doc-graph.ps1 -Changed` → **그래프 정합성 OK**. `[workslot]` 갱신일 역전 경고 5건은
  전파 대상 문서를 전수 확인한 결과 "푸시 해상도"를 언급하는 문서가 없어 그대로 뒀다.
- ⛔ **실제 구동 검증은 안 했다** — 서버·Unity를 띄워 A-2 로그로 갭이 0.1초 이내인지 확인해야 한다.

## 남은 것

- 이슈 #11에 [수정 내역을 코멘트로 남겼다](https://github.com/JeongTaeWoong99/Windows_simulator/issues/11#issuecomment-5232859910)
  — 원인 3종 · 필드 개명 · A-2 로그로 실구동 확인 요청. **이슈는 열어 둔다**(실구동 확인 전).
- **이슈 #12** (`S_GatherResultResponse`에 슬롯 상태 포함 — 위에 "#2"로 잘못 적었다) — 이 수정과 독립.
  지금은 **채취 결과 경로와 슬롯 스냅샷 경로가 만나지 않아** 클라가 로그인/배치 때 받은 기준점을
  세션 내내 그대로 쓴다. #2가 되면 매 사이클 서버 기준으로 재동기화된다.
- **이벤트 기반 스케줄러** — 완성 시각이 닫힌 형태로 나오므로(`TimeUntilNextJudge`) 최소 힙에 넣고
  그때만 깨우면 헛도는 틱이 0이 된다. 슬롯이 수만 개가 되기 전까지는 0.1초 폴링으로 충분하다.
