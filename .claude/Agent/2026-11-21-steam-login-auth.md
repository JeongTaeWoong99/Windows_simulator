---
date: 2026-11-21
title: 스팀 로그인 인증 — Web API 티켓 검증 · 개발용 로그인 분리 (T-207)
tags: [server, login, steam, protocol]
---

# 스팀 로그인 인증 (T-207)

## 목적 / 배경
- 지금까지 `C_LoginRequest.Id`를 그대로 pid로 썼다 — 아무 Id로나 남의 계정에 들어갈 수 있었다.
- 스팀 출시를 위해 스팀 티켓을 서버가 확인한 SteamID로 자동 가입·로그인시킨다. AppID($100)는 출시 준비 때 결제 — 그전엔 480 · 개발용 로그인.

## 변경 내용
- `Server/WSGameServer/Steam/` — `SteamTicketResponse`(응답 → 판정) · `SteamWebApi`(HTTP · 기한 · 이름 조회) · `LoginGate`(티켓/개발용 분기) · `SteamSettings`
- `ClientPacketHandler.Handle_C_LoginRequest` → `LoginGate.Current.HandleAsync`(끝을 기다리지 않음 · 예외만 로그)
- `GameServer.ConfigureLogin` — 관문 조립 · 거절 응답(`RejectLogin`) · 키 유무만 로그
- 패킷: `C_LoginRequest.SteamTicket` · `EResultCode.SteamAuthFailed=6` · `SteamUnavailable=7` (Unity 미러 반영)
- 설정: `Game:AllowDevLogin`(코드 기본 false · appsettings true) · `Game:Steam` · 원격 키는 `Server/.env` → compose
- 테스트 `WSGameServer.Tests/Steam/` 26개

## 주요 결정 / 근거
- **게임 서버 SDK(`BeginAuthSession`) 대신 Web API** — 서버가 .NET 10 · 리눅스 Docker라 네이티브 Steamworks가 필요 없는 HTTP 하나가 단순하다.
- **스팀 쪽 문제는 `SteamUnavailable`, 티켓 문제만 `SteamAuthFailed`** — 403(키 틀림)·기한 초과·깨진 응답까지 `AuthFailed`로 돌리면 클라가 새 티켓만 무한히 만든다.
- **패밀리 공유는 `steamid`(플레이어)로 계정** — `ownersteamid`로 만들면 빌려준 사람 계정과 섞인다(사용자 결정 2026-11-21).
- **VAC 밴은 막지 않는다** — 다른 게임 이력이다. `publisherbanned`만 `Banned`.
- **개발용 Id에 `steam:` 접두 거절** — 원격이 아직 개발용 로그인을 켜 둔 동안 남의 스팀 계정 pid를 적어 들어오는 길을 막는다.
- **`AllowDevLogin` 코드 기본값은 false, appsettings에서 true** — 설정을 빠뜨린 서버가 아무 Id나 받지 않게. 원격 Docker는 appsettings를 그대로 쓰므로 지금 친구 빌드(Id 로그인)는 계속 된다.
- 기존 테스트 계정은 잇지 않는다(사용자 결정) — 스팀 계정은 `steam:<id>` pid로 새로 생긴다. DB 마이그레이션 없음(`provider_id` UNIQUE 그대로).

## 후속 작업 / 주의사항
- 클라 연동은 이슈 #144(태웅). 들어오면 480 티켓 + 일반 Web API 키로 서버 검증이 되는지 실측해 T-207에 적는다.
- **친구 빌드가 스팀 로그인으로 바뀌면 원격 `Game__AllowDevLogin: "false"`** (compose에 주석으로 둠).
- 맥에서 빌드하면 `MikaProtocol` post-build가 `powershell`을 못 찾아 미러가 안 된다(MSB3073 경고만) — 이번엔 두 파일을 직접 복사했다. 빌드가 `Assets/Plugins/Analyzers/MikaSourceGen.dll`도 다시 써서 바이너리 diff가 생기니 커밋하지 않는다.
- `HttpClient` 기본 Timeout은 100초다 — `SteamWebApi`는 `CancelAfter(Steam.Timeout)`로 자른다. 테스트의 무응답 핸들러도 이것으로 끝난다.
