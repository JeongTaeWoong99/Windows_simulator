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
- 클라 접속 포트: **10050** (`Assets/Scripts/Network/MikaNetwork.Unity/NetworkManager.cs`)
- 포트 안 맞으면 `SocketException: 연결 거부` 발생.

---

## 봐야 할 서버 폴더 스크립트

> `Assets/Scripts/...`는 원래 서버 영역. 아래는 협의 후 내가 다루는 것들.

| 파일 | 용도                                                           |
|------|--------------------------------------------------------------|
| `Assets/Scripts/Network/MikaNetwork.Unity/ServerPacketHandler.cs` | **응답 패킷 수신 진입점.** `Handle_S_XXX` 핸들러 추가 → 이벤트 발행. (클라 작업 OK) |
| `Assets/Scripts/Protocol/MikaPacket.cs` | **주고받는 패킷 내용 확인용.** 어떤 필드가 오는지 확인. (직접 수정 금지 — 서버에서 미러링됨)    |
| `Assets/Scripts/Protocol/PacketInfo.cs` | 패킷 안 데이터 타입(ItemInfo, GachaRewardInfo 등) 확인. (직접 수정 금지)      |
| `MikaGenerated.GeneratedHandlers` (자동 생성) | 핸들러가 실제 등록됐는지 확인용. 소스 제너레이터가 `[PacketHandler]` 보고 자동 생성.     |

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

### 로그인
- 보냄: `C_LoginRequest { Id }` (Id만 넘기면 됨)
- 받음: `S_LoginResponse { Success, SessionId }` + `S_InventoryResponse { Items }` (로그인 시 인벤토리 스냅샷 자동 수신)

### 가챠
- 보냄: `C_GachaDrawRequest { GachaId, DrawCount(1 or 10) }`
- 받음: `S_GachaDrawResponse { Success, Rewards }`
- ⚠️ **로그인으로 User가 생성된 뒤에만 동작** → 반드시 **로그인 먼저 → 가챠** 순서.