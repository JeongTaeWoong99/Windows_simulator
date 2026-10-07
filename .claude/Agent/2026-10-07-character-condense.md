---
date: 2026-10-07
title: 캐릭터 응축 서버 (★ · 누적 재료 수)
tags: [server, data, design]
---

# 캐릭터 응축 서버

## 목적 / 배경
- 중복 캐릭터의 출구(T-076) — 팰월드식 응축. 규칙 → `character/README.md` 5.5 · 일감 → `tasks/T-130`

## 주요 결정 / 근거
- **개체는 ★이 아니라 누적 재료 수(`condense_count`)만 저장한다.** 사용자 지시 "남는 재료 소모해도 돼, 그 캐릭터가 기억하고 있으면 돼" —
  ★은 `CharacterStarCatalog.StarAt`이 표의 누적 기준으로 매번 읽는다. 기준을 엑셀에서 바꿔도 DB 백필이 필요 없다.
- 표의 `RequiredCount`는 **그 단계 몫**(8·14·22·32)이고 누적 기준(8·22·44·76)은 카탈로그가 만든다. `SpeedAddPermille`은 **그 ★의 총량**(누적 아님 — `CharacterLevelTable`과 같은 규약).
- 한 요청에 단계 제한 없음 · 최고 기준을 넘는 몫도 소모(누적은 76에서 멈춤) · ★4면 `CondenseMaxStar`로 거절.
- 대상은 배치·착용 중이어도 된다(재료만 막는다). ★이 오르면 `RefreshWorkStationSpeed` — 정산 뒤 속도라 소급되지 않는다.
- `MailCharacter`에 `CondenseCount = 0` 기본값을 **뒤에** 붙였다 — 이미 JSON으로 저장된 우편·경매 매물이 0으로 읽힌다.

## 후속 작업 / 주의사항
- ⚠️ 운영 DB에 `Server/Shared/migrations/2026-10-07-character-condense.sql`을 돌려야 한다. 안 돌리면 로그인의 캐릭터 조회(`condense_count`)가 실패한다.
- 경매장 매물 화면(AuctionServer 검색 결과)에는 ★이 안 실린다 — 상세 JSON에만 있다. 필요하면 별도 일감.
- 파이썬으로 C# 파일을 일괄 치환할 때 **같은 꼬리(`Container`/`Slot`)를 가진 Info가 여럿**이라 첫 일치가 `ItemInfo`에 들어갔었다 — 클래스명까지 넣어 매칭한다.
