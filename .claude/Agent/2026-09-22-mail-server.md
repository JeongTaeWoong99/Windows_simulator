---
date: 2026-09-22
title: 서버 우편 — 운영 우편(개인·전체) · 원자적 수령 · 모두 받기 · 삭제 · 치트 발송 (T-081)
tags: [server, mail, excel, db, packet, T-081]
---

# 서버 우편 (T-081)

## 목적 / 배경
- 우편 기획(`GameDesign/design/mail/README.md`)의 운영 우편 부분을 구현했다. 넘침 보관은 T-082, 클라 화면은 T-083이다.

## 변경 내용
- 엑셀 `Mail.xlsx` / `MailTemplateTable` — 우편 한 통이 한 줄이고 첨부도 같은 줄에 담는다(사용자가 시트 1개를 골랐다). 테스트 데이터 2줄: 1 = 점검 보상, 2 = 넘침 템플릿
- DB `t_user_mail` · `t_global_mail` · `t_user_global_mail`. `game.sqlite3`에 적용했고 `SqliteFixture.CreateMailTables`에도 같은 DDL을 넣었다
- 패킷 39~44 · `MailInfo` · 결과 코드 900~ · `ECheatCommand.SendMail = 10`
- `MailCatalog` — 기동할 때 `ItemTIDs`와 `ItemCounts` 개수가 다르면 예외
- `User.Mail.cs` — 수령·모두 받기·삭제·도착 통지·`SendOperationMail`
- `MailRepository.cs` — 로그인 적재 · 전체 우편 복사 · 발송 · 받음 표시 · 삭제
- `User.Login(now)` — 시각 인자를 추가했고, 끝에서 `LoadMailbox`를 부른다

## 주요 결정 / 근거
- **첨부는 보내는 순간 우편 행(JSON)에 복사한다.** 템플릿을 고쳐도 이미 보낸 우편은 그대로다. 넘침 우편은 템플릿 대신 실제 보상을 같은 칸에 넣는다.
- **제목·본문은 패킷에 싣지 않는다.** 클라가 `MailTemplateTable`에서 `TemplateTid`로 읽는다. 운영툴이 자유 문구를 보내게 되면 필드를 추가한다.
- **로그인 때 정리·전체 우편 복사·조회를 한 저장소 작업으로 묶었다**(`LoadMailboxRepository`). 따로 돌리면 "도착" 통지와 목록 스냅샷이 겹쳐 클라에 같은 우편이 두 번 들어간다.
- **복사 기록은 따로 둔다**(`t_user_global_mail`). 받은 우편은 7일 뒤 지워지는데, 복사 기록까지 같이 사라지면 기간이 남은 전체 우편이 다시 복사된다.
- 오프라인 유저에게 보내는 개인 우편은 **보낸 사람의 DB 파티션**에서 INSERT한다. 받는 사람이 없으니 파티션도 없다. 받는 사람이 그 순간 로그인하면 이번 로그인 목록에서 빠질 수 있지만, 다음 로그인에는 실린다.
- 인벤토리 부족은 새 코드를 만들지 않고 `StorageFull`을 재사용했다(칸 계산 `HasStorageFor`도 T-063 것 그대로).

## 후속 작업 / 주의사항
- **실측이 남았다** — 실서버 + 클라(또는 더미 클라)로 왕복 확인.
- 클라 쪽 `S_Mail*` 핸들러가 없어 Unity에 MIKA001 경고 4건이 뜬다. 정상이며 T-083이 채운다.
- 테스트는 구현 뒤에 썼다. 칸 검사와 복사 중복 방지를 일부러 깨 보니 3건이 실패했고, 테스트가 동작을 잡는다는 걸 확인했다.
- `DeliverGlobalMailsAsync`는 DB 스레드에서 `MailCatalog.Instance`(읽기 전용)를 읽는다. User에 주입한 카탈로그와 다를 수 있으니 테스트할 때 주의한다.

## 업데이트 (2026-09-22) — 실측
- 더미 클라와 실서버로 왕복했다. 전체 우편 로그인 복사 · 접속 중 개인 우편 도착 · 수령 · 중복 수령 거절 · 안 받은 우편 삭제 거절 · 모두 받기 · 삭제 · 재로그인 목록 일치를 모두 확인했다.
- **버그: 접속 중인 수신자를 못 찾았다.** `UserManager.TryGetUser(ulong uid)`는 이름과 달리 `User.Key`로 찾는다. `TryGetUserByUid`를 추가해 고쳤다(`42923e3`). **Uid로 접속 중인 유저를 찾을 일이 생기면 `TryGetUserByUid`를 쓴다.**
- 더미 클라에 우편 메뉴를 붙였다(14 = 수령, 0이면 모두 받기 · 15 = 삭제 · 치트 10 = SendMail). stdin에 스크립트를 흘려 넣어 몰 수 있다.
- 실측 계정(uid 7·8)과 전체 우편 1건은 지웠다. `game.sqlite3`는 커밋 버전 바이트로 되돌렸다(VACUUM이 바이트를 바꾼다).

## 업데이트 (2026-09-22) — 넘침 보관 (T-082)
- `GachaService.OpenBox`는 이제 **수량 굴리기 → 칸 검사 → (안 들어가면) 우편함 상한 검사 → 상자 차감 → 지급 또는 넘침 우편** 순서로 돈다. 수량을 한 번만 굴리는 `Roll`을 분리했다 — 창고로 주든 우편에 담든 같은 값이어야 한다.
- 우편함 상한 100은 `User.MailboxCapacity`다. **저장을 기다리는 넘침 우편**(`_pendingOverflowMails`)도 센다 — 캐릭터 대기분과 같은 이유다.
- `S_ItemUseResponse.StoredInMail` 필드를 추가했다. true면 `ItemChangeInfos`에 상자 차감만 들어 있다.
- 넘침 템플릿 TID 2는 `MailCatalog.OverflowTemplateTid`다. 이 템플릿이 엑셀에 없으면 기동이 멈춘다.
- **자원 칸 200은 실제로 차지 않는다**(아이템 162종). 넘침은 장비 칸이 찼을 때 일어난다. 실측은 생략했다 — 우편 저장 경로는 T-081 실측으로 확인했다.
