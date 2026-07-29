# 클라이언트 네트워크 작업 노트 (개인 리마인드)

클라이언트(`Assets/Scripts_Client`) 담당으로 **서버 연동/패킷** 작업하면서 자주 확인·기억할 것들 메모.
(윈도우 제어 데모 기록은 `README.md` 참고 — 이 파일은 네트워크 전용.)

---

## 서버 실행

```
dotnet run --project Server/WSGameServer
```

- 콘솔에 `[Server] 10050 포트에서 대기 중...` 뜨면 정상.
- 테스트하려면 **서버를 먼저 켜둔 상태**에서 Unity Play.
- 서버는 `.NET 10` 필요(이미 설치됨).

## 접속 정보

- 서버 리슨 포트: **10050** (`Server/WSGameServer/Network/NetworkManager.cs`)
- 클라 접속 포트: **10050** (`Assets/Scripts_Server/Network/MikaNetwork.Unity/NetworkManager.cs`)
- 포트 안 맞으면 `SocketException: 연결 거부` 발생.

---

## 봐야 할 서버 폴더 스크립트

> `Assets/Scripts_Server/...`는 원래 서버 영역. 아래는 협의 후 내가 다루는 것들.

| 파일 | 용도                                                           |
|------|--------------------------------------------------------------|
| `Assets/Scripts_Server/Network/MikaNetwork.Unity/ServerPacketHandler.cs` | **응답 패킷 수신 진입점.** `Handle_S_XXX` 핸들러 추가 → 이벤트 발행. (클라 작업 OK) |
| `Assets/Scripts_Server/Protocol/MikaPacket.cs` | **주고받는 패킷 내용 확인용.** 어떤 필드가 오는지 확인. (직접 수정 금지 — 서버에서 미러링됨)    |
| `Assets/Scripts_Server/Protocol/PacketInfo.cs` | 패킷 안 데이터 타입(ItemInfo, GachaRewardInfo 등) 확인. (직접 수정 금지)      |
| `MikaGenerated.GeneratedHandlers` (자동 생성) | 핸들러가 실제 등록됐는지 확인용. 소스 제너레이터가 `[PacketHandler]` 보고 자동 생성.     |

> ⚠️ 수신 패킷(`S_*`)에 `[PacketHandler]`가 없으면 **MIKA001 경고**가 뜬다.
> 현재 `S_PongResponse` · `S_UpdateItemResponse` 2건이 남아 있는데, **서버가 보내지 않는 미사용 패킷**이라
> 의도적으로 둔 것이다(정의만 있고 호출부 없음). 새로 뜨는 경고는 진짜 누락이니 확인할 것.

## 3계층 구조 — 어디에 뭘 쓰나

새 패킷을 붙일 때 손대는 곳은 항상 이 3개다.

| 계층 | 파일 | 하는 일 |
|------|------|---------|
| 수신 진입점 | `Scripts_Server/.../ServerPacketHandler.cs` | `[PacketHandler]` + `Debug.Log` 한 줄 + static 이벤트 발행. **얇게.** |
| 파사드 | `Scripts_Client/Managers/SessionManager.cs` | 요청 API · 상태 캐시 · 가공 이벤트. 서버 의존을 여기서 끊는다 |
| UI | `Scripts_Client/UI/*.cs` | SessionManager 이벤트만 구독. **ServerPacketHandler를 직접 보지 않는다** |

핸들러 등록 코드는 **직접 쓰지 않는다** — `[PacketHandler]` 어트리뷰트만 붙이면 소스 제너레이터가 자동 등록한다.

## 패킷 흐름 (수신)

```
서버  → NetworkManager(PacketReceived) 
     → ServerPacketManager(자동 등록 핸들러로 분배)
     → NetworkMessageQueue(메인 스레드로) 
     → Update에서 Flush 
     → Handle_S_XXX 호출
     → static 이벤트 발행 
     → 내 클라 스크립트가 구독해서 처리
```

- 핸들러는 **Unity 메인 스레드**에서 실행됨 → UI 갱신·이벤트 발행 안전.
- `ServerPacketHandler`는 **얇게**(받아서 이벤트만), 실제 처리는 `Scripts_Client`에서.

---

## 기능별 패킷 요약

### 로그인 — ⭐ 한 번 보내면 4개가 따라온다

- 보냄: `C_LoginRequest { Id }` (Id만 넘기면 됨)
- 받음: **아래 순서로 연달아 1회씩.** 인벤토리·슬롯을 따로 요청하는 패킷은 **서버에 아예 없다.**

```
S_LoginResponse             { Success, SessionId }
S_InventoryResponse         { Items }        ← 인벤토리 전체 스냅샷
S_GatherResultResponse      { ... }          ← 오프라인 누적 채취분 (있을 때만)
S_WorkStationSlotsResponse  { Slots }        ← 작업슬롯 전체 스냅샷
```

**UI 초기화는 이 4개가 다 온 뒤를 기준으로 잡는다.** 로그인 응답만 보고 화면을 그리면 빈 상태가 보인다.

> ⚠️ **인벤토리 스냅샷은 채취 정산 "전" 값이다.** 서버가 `SendInventory()`를 먼저 보내고
> 그 다음에 정산한다(`User.cs:61,65`). 오프라인 누적분은 뒤이어 오는
> `S_GatherResultResponse.ItemChanges`에 들어 있으니, 스냅샷만 믿으면 수량이 모자라 보인다.

### 가챠
- 보냄: `C_GachaDrawRequest { GachaId, DrawCount(1 or 10) }`
- 받음: `S_GachaDrawResponse { Success, Rewards }`
- ⚠️ **로그인으로 User가 생성된 뒤에만 동작** → 반드시 **로그인 먼저 → 가챠** 순서.
- 가챠로 얻은 아이템은 **인벤토리 갱신 패킷이 따로 오지 않는다**(`GachaService.cs:48`).
  `Rewards`로 직접 반영해야 한다.

### 작업슬롯 / 채취 (낚시)

**"낚시 패킷"은 없다.** 낚시 = `Industry = 2`(`GameData.ItemType.Fishing`)인 작업슬롯 배치일 뿐이고,
채취 결과는 산업 구분 없이 전부 `S_GatherResultResponse`로 온다.

- 보냄: `C_WorkStationAssignRequest { SlotIndex, Industry, CharacterId }`
  - **배치와 해제가 같은 패킷.** `Industry = 0, CharacterId = 0`으로 보내면 해제다.
- 받음: `S_WorkStationAssignResponse { Success, Slot }` — 변경된 **슬롯 하나만** 온다
- 받음(푸시): `S_GatherResultResponse { SlotIndex, JudgeCount, ItemChanges }`

⭐ **핵심 — 로그인만으로는 채취 패킷이 오지 않는다.**
서버의 30초 타이머(`GatheringScheduler`)는 항상 돌지만, 슬롯이 **활성**
(`Industry != None && CharacterId != 0`)이어야 판정이 나간다.
신규 계정 슬롯은 `industry=0, character_id=0`으로 생성되므로 **배치 요청을 보내야 시작된다.**

```
로그인 → 슬롯 배치 → (30초) → 채취 푸시 → (30초) → 채취 푸시 → ... → 해제하면 멈춤
```

- 수확이 없으면 패킷 자체가 안 온다(빈 패킷 스팸 없음).
- 로그인·슬롯 변경 시엔 밀린 구간을 한 번에 정산하므로 `JudgeCount`가 2 이상일 수 있다.
- ⚠️ **`ItemChanges`의 `Count`는 델타가 아니라 "갱신 후 누적 총량"이다.**
  `PacketInfo.cs`엔 `// 변경(델타) 전용`이라 적혀 있지만 실제 값은 총량이다(`Inventory.AddItem`).
  **더하면 수량이 두 배가 된다. 덮어쓸 것.**

#### 현재 서버 제약 (2026-07-29 기준)

| 항목 | 상태 |
|------|------|
| 드롭 테이블 | **낚시(Fishing)만 등록됨.** 다른 산업으로 배치하면 판정만 돌고 아이템·패킷이 안 온다 |
| 슬롯 개수 | **0번 하나뿐.** `DefaultSlotCount = 1`이고 해금 경로가 없다(`Unlock()`은 테스트만 호출) |
| 없는 슬롯 배치 | 예외 없이 `S_WorkStationAssignResponse { Success = false }`만 온다 |
| `CharacterId` | **더미다.** 캐릭터 시스템·테이블·엑셀이 전부 없고, 서버는 `!= 0`만 본다 |

구조 자체(딕셔너리·슬롯별 독립 정산·슬롯별 푸시)는 다중 슬롯을 이미 지원한다.

---

## 테스트 방법 — PacketTestPanelUI

`Assets/Scripts_Client/UI/PacketTestPanelUI.cs` + 씬의 버튼 OnClick에 연결.
버튼은 **인자 없는 `public void`**여야 인스펙터 드롭다운에 뜬다.

| 버튼 | 메서드 |
|------|--------|
| 로그인 | `SendLogin()` |
| 가챠 1회 / 10연 | `SendGachaSingle()` / `SendGachaTen()` |
| 작업슬롯 배치 | `SendWorkStationAssign()` |
| 작업슬롯 해제 | `SendWorkStationClear()` |

인스펙터에서 `_slotIndex` / `_industry` / `_characterId`를 바꿔 보낸다.
채취 결과 로그에는 **수신 시각**이 찍히므로 30초 주기가 실제로 도는지 로그만 보고 확인할 수 있다.