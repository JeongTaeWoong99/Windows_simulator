---
date: 2026-11-20
title: 아티팩트 켜고 끄기 패킷 삭제 (T-181 · #90 마무리)
tags: [server, protocol, packet]
---

# 아티팩트 켜고 끄기 패킷 삭제 (T-181 · #90)

## 목적 / 배경
- 2026-10-28에 "장착 = 항상 켜짐"으로 바꾸면서 패킷은 남겨 두었다(클라 컴파일 보호). 클라가 T-151에서 요청 송신을 지웠으니 남은 껍데기를 없앤다.

## 변경 내용
- `Server/MikaProtocol/MikaPacket.cs` — `C_ArtifactActiveRequest`(90) · `S_ArtifactActiveResponse`(91) 삭제, 번호는 결번 주석
- `Server/MikaProtocol/PacketInfo.cs` — `ArtifactCodexInfo.IsActive` 삭제
- `Server/MikaProtocol/PacketEnum.cs` — `ArtifactNotEquipped`(1401) 삭제, 결번 주석 (이 패킷 전용이었다)
- `Server/WSGameServer` — `Handle_C_ArtifactActiveRequest` · `User.ReplyArtifactActive` 삭제, 도감 스냅샷에서 `IsActive = true` 제거
- `Assets/Scripts_Server/Network/.../ServerPacketHandler.cs` — `Handle_S_ArtifactActiveResponse` 삭제
- 테스트 — `ArtifactCodexTest` "항상 켜짐" 2건 삭제 · 도감 스냅샷 검사에서 `IsActive` 제거
- 미러 `Assets/Scripts_Server/Protocol`은 빌드 post-build로 갱신
- `유물감정.md` 패킷 줄 갱신

## 주요 결정 / 근거
- 패킷 번호 90·91 · 결과 코드 1401은 재사용하지 않는다(기존 결번 관례 — 32·33, 700·701).
- MemoryPack: `ArtifactCodexInfo`의 끝 멤버를 지웠다. 구 클라가 새 서버 데이터(멤버 1개)를 읽는 것은 된다(빠진 값은 기본값). 새 클라가 구 서버 데이터(멤버 2개)를 읽으면 실패할 수 있으니 서버·클라는 함께 배포한다.
- 구 클라가 90을 보내면 서버는 `MikaPacketManager.UnknownReceived`로 버린다.

## 검증
- 솔루션 빌드 오류 0 · `WSGameServer.Tests` 1268 통과
- 클라(`Assets/Scripts_Client`)는 `ArtifactCodexInfo.ArtifactTid`만 읽는다 — 지운 심볼 참조 0건

## 후속
- 없음. T-181 남은 항목 끝.
