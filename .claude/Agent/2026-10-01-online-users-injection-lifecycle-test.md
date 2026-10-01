---
date: 2026-10-01
title: User가 전역 UserManager를 모르게 — 이벤트 + IOnlineUsers (T-089 · T-020)
tags: [server, test, user-manager, lifecycle]
---

# User가 전역 UserManager를 모르게 (T-089 · T-020)

## 목적 / 배경
- `UserMailTest` 하나가 가끔 실패(T-089). 원인은 병렬 테스트가 전역 `UserManager.Instance`를 공유한 것 — 로그인 마무리가 uid 7을 올리고 내리지 않는 테스트가 있으면 우편이 그 유저로 갔다.
- T-020(로그인 생명주기 테스트)도 같은 결정(전역 처리 방침)을 기다리고 있었다.

## 변경 내용
- `User` — `LoggedIn`·`Left` 이벤트. `Login`·`Disconnect`가 `JoinUser`/`LeaveUser` 대신 이벤트만 울린다.
- `UserManager.CreateUser` — 생성 직후 두 이벤트를 구독해 등록·해제. `IOnlineUsers`를 구현하고 자기 자신을 `User`에 넘긴다.
- `IOnlineUsers`(uid 조회 · 전체 순회) — 치트 우편 수신자 찾기(`User.Mail`)와 전체 우편 전달(`SendGlobalMailRepository`)만 쓴다.
- 테스트 — `TestUserBuilder.Online`(`FakeOnlineUsers`, 테스트마다 새 목록). `UserMailTest`·`InventorySlotRepositoryTest`의 전역 넣고 빼기 정리 코드 제거. `UserLifecycleTest` 7건 추가.

## 주요 결정 / 근거
- 처음 안(User 생성자에 `UserManager`를 통째로 주입)은 버렸다 — 관리받는 쪽이 관리자를 들고 스스로 등록하는 역방향이고, 필요한 건 조회 두 가지뿐이다.
- 패킷 핸들러·스케줄러·세션 확장 등 **조립 지점은 계속 `UserManager.Instance`** 를 쓴다. 운영에는 목록이 하나뿐이라 주입할 이유가 없다.
- `User` 생성자의 `onlineUsers`는 생략하면 전역이다 — 다른 카탈로그 인자와 같은 규약.

## 후속 작업 / 주의사항
- 패킷 순서 테스트는 캐릭터·슬롯 전송을 뒤집어 그 테스트만 빨개지는 것을 확인했다.
- 전체 스위트 10회 연속 통과(732개).
- `UserManagerTest`의 `Join` 헬퍼는 여전히 매니저 인스턴스를 직접 만든다(전역 아님) — 그대로 둔다.
