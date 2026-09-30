---
date: 2026-09-28
title: 다이아(유료 재화) 폐지
tags: [server, client, data, design]
---

# 다이아(유료 재화) 폐지

## 목적 / 배경
- 이슈 #41 — TBH식 BM(DLC + 스팀 마켓 수수료)을 따르므로 게임 안 유료 재화가 필요 없다. 쓰는 곳이 0일 때 걷어냈다.
- 변경 목록은 커밋 `11ead47` 메시지.

## 주요 결정 / 근거
- **`ECheatCommand.GiveDia = 2`는 지우고 번호는 결번 주석으로 남겼다** — enum 번호 재사용 금지(엑셀 규칙 2와 같은 이유).
- **`C_UnlockRequest.Currency` 필드는 남겼다** — 지불 컬럼이 다시 늘 수 있고, 지우면 클라가 한 번 더 깨진다. 해금 #16 규칙은 "지불 컬럼이 둘 이상이면 OR"로 일반화했다.
- **`CheatGiveCurrency(amount, currency)` → `CheatGiveGold(amount)`** 로 줄였다. 재화가 하나라 분기가 필요 없다.
- 클라 코드(`Scripts_Client`)는 #20 선례대로 손대지 않고 이슈로 넘긴다 — 대응 전까지 Unity 컴파일이 깨진다.

## 후속 작업 / 주의사항
- ⚠️ **SQLite `ALTER TABLE DROP COLUMN`은 마지막 컬럼을 지우면 앞 컬럼의 `--` 주석까지 잘라 먹는다.** `t_user_currency`는 그래서 새 테이블을 만들어 복사·RENAME으로 다시 만들었다(sqlite-sql-creator의 "모든 컬럼 주석" 규칙).
- ⚠️ `generate-tables.ps1`은 끝에 `Read-Host`로 멈춘다 — 에이전트가 돌리면 생성은 끝났는데 프로세스가 안 끝난다. 생성물 diff를 확인하고 그 powershell을 종료하면 된다.
- 테스트의 `AuctionDb.SettleAsync`·`RegisterAsync` 호출은 위치 인자 `0`(다이아)을 쓰던 곳이 많았다 — 시그니처를 또 바꾸면 이름 인자가 아닌 호출이 조용히 다른 파라미터로 밀릴 수 있다.

## 업데이트 (2026-09-28) — 클라 대응 (#45)
- 커밋 `9846aad`(코드·문서) · `6ac20e6`(씬). #41·#45 닫음.
- **`Dia Panel`은 `Scenes/Original`에서만 지웠다** — `Scenes/Test Copy`에는 남아 있다(사용자 결정). 스크립트 참조가 없어 그대로 둬도 깨지지 않는다.
- ⚠️ 씬 저장 diff가 +1.5k줄로 커 보이지만 대부분 LayoutGroup이 다시 계산한 `m_AnchorMin/Max`·`m_SizeDelta`다. 구조 변경은 `Dia Panel`·`Dia Image`·`Dia Text` 삭제와 `diaText` 참조 제거뿐이다.
- `GachaResultPresenter.OnMailRewardsClaimed`의 "왜 `GachaRewardInfo`로 바꾸지 않나" 주석 근거가 다이아였다 — 근거를 "우편 첨부는 종류마다 필드가 따로 온다"로 바꿨다.
