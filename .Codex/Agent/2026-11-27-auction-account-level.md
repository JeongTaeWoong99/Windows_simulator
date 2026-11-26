---
date: 2026-11-27
title: 거래소 계정 Lv30 개방
tags: [server, data, test]
---

# 거래소 계정 Lv30 개방

- #164에서 확정한 기준을 T-012 구현에 적용했다. 서버와 Unity가 `Constants.xlsx`의 `AuctionMinAccountLevel`을 함께 읽는다.
- 게임 서버의 거래소 패킷 경계에서 레벨을 검사한다. 경매장 서버 간 통신과 만료·우편 정산은 기존 흐름을 사용한다.
- 클라 화면 처리(메뉴·Ctrl+우클릭 안내)는 이 작업에서 하지 않고 클라 담당에게 넘긴다. 서버는 거절 코드 `AuctionAccountLevelLocked`와 현지화 키 `UI.Result.AuctionAccountLevelLocked`(`{0}` = 개방 레벨)만 준비했다.
- 서버 테스트에서 계정 Lv29·Lv30 판정과 미로그인 응답을 검증한다.
