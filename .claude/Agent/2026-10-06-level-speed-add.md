---
date: 2026-10-06
title: 캐릭터 레벨 효과를 작업속도 가산으로 — 적성 포인트 철거 (T-003 · 이슈 #35)
tags: [server, data, design]
---

# 캐릭터 레벨 효과를 작업속도 가산으로 — 적성 포인트 철거

## 목적 / 배경
- 이슈 #35 동의(기획·서버·클라). 방치형에 수동 찍기 · 상한 150칸이 과하다는 판단 → `tasks/T-003` · 캐릭터 5.4.

## 변경 내용
- 무엇을 바꿨는지는 `tasks/T-003-캐릭터성장.md` "2026-10-06 재정의" 표가 원본이다(조각 · 철거 목록).

## 주요 결정 / 근거
- **`SpeedAddPermille`는 레벨별 증분이 아니라 그 레벨의 총량(누적값)이다.** 옛 `AptitudePoint`는 증분을 로드 때 누적했는데,
  가산은 구간마다 기울기를 바꾸기 쉽게 총량으로 두었다. `SpeedAddAt`은 행 값을 그대로 돌려준다.
- **레벨업 → 속도 재계산은 `SettleWorkStation` 끝에서 한 번.** 레벨은 정산 도중(`GrantCharacterExp`)에 오르므로
  정산 안에서 `RefreshWorkStationSpeed`를 부르면 재귀가 된다. `GrantCharacterExp`가 레벨업 여부(bool)를 돌려주고,
  정산 루프 뒤 `ApplyWorkStationSpeed`로 매긴다 — 정산이 이미 구간을 끊었으므로 소급되지 않는다.
- 치트 `GiveCharacterExp`는 정산 밖이라 레벨업 시 `RefreshWorkStationSpeed(now)`를 부른다(슬롯의 옛 속도로 먼저 정산).
- **철거를 택했다(데이터로 끄기 대신).** `AptitudePoint`를 전부 0으로 두는 대안이 있었지만, 클라 화면이 없어 쓰는 사람이 없었고
  남겨 두면 패킷·DB·우편/경매 JSON에 죽은 필드가 계속 실린다.
- PacketId 32·33, 결과 코드 700·701은 **결번**으로 남겼다(재사용 금지 주석).
- `MailCharacter`에서 `Bonus`를 뺐다. 우편·경매 매물 JSON에 남은 옛 `Bonus` 필드는 System.Text.Json이 무시한다 — 마이그레이션 불필요.

## 후속 작업 / 주의사항
- `Server/Shared/migrations/2026-10-06-drop-aptitude-bonus.sql`은 **`game.sqlite3`에 아직 적용하지 않았다** — 서버를 끄고 돌린다.
  새 서버는 보너스 컬럼을 읽지도 쓰지도 않으므로 적용 전에도 동작은 같다.
- 클라: 슬롯 설정 효율 줄에 레벨 항 표시(`CharacterLevelTable` 미러 읽기) — 이슈 #35로 넘긴다.
- ⚠️ 작업 도중(00:30) 공유 main 트리가 통째로 HEAD로 되돌아가 한 번 다시 적용했다(출처 불명 — 동료 세션 둘 다 아님).
  이후 워크트리 `worktree-T-003-level-speed-add`로 옮겨 진행했다.
- ⚠️ `generate-tables.ps1`은 대화형 콘솔에서 끝에 `Read-Host`로 멈춘다 — 셸에서 stdout을 파일로 돌리면 백그라운드에서 영영 대기한다. 파이프(`| tail`)로 받으면 끝난다.
