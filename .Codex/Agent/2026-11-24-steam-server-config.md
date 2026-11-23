---
date: 2026-11-24
title: 스팀 로그인 개발·Docker 설정
tags: [server, steam, config]
---

# 스팀 로그인 개발·Docker 설정

## 목적

- #152에서 요청한 개발용 Steam 키와 원격 Docker의 AppID 설정 경로를 마련했다.

## 주요 결정

- 개발자는 키를 사용자 프로필의 .NET User Secrets에 등록한다. Docker는 기존 `Server/.env`에서 키와 AppID를 읽는다. 설정과 등록 방법은 [도커.md](../../Server/docs/도커.md)에 적었다.
- 두 Dockerfile이 `Server/` 빌드 컨텍스트를 복사하므로 `Server/.dockerignore`에서 `.env`를 제외했다.

## 검증과 후속 작업

- 게임 서버 테스트 1,343개 통과. 임시 사용자 프로필에서 가짜 키를 읽고, 격리 Compose에서 AppID 기본값과 덮어쓰기를 확인했다.
- 격리 Docker를 이미지 빌드부터 시작해 키가 없는 Steam 요청은 `SteamUnavailable`, 기존 Id 요청은 `Ok`를 받는 것을 확인했다.
- #144 클라 연동과 실제 키가 준비되면 AppID 480 티켓 검증을 진행한다. 친구용 빌드로 접속을 확인한 회차에 원격 Id 로그인을 끈다.
