---
date: 2026-10-07
title: 슬롯 정보에 작업속도 내역 · 전역 배수 치트 (T-055)
tags: [server, workstation, cheat, protocol]
---

# 슬롯 정보에 작업속도 내역 · 전역 배수 치트 (T-055)

## 목적 / 배경
- 클라는 2026-09-24부터 레벨·특성·장비 가산을 서버 식을 베껴 직접 계산했다. ★ 가산(T-130)은 그 사본에 없어서, ★이 붙으면 그 몫이 "개발용 전역 배수"로 보인다.
- 사용자 요청: 전역 배수를 치트로 올릴 수 있게 하고, DB에는 저장하지 않는다.

## 주요 결정 / 근거
- **전역 배수는 로그인 응답이 아니라 슬롯 정보에 싣는다.** 일감 문서는 "로그인 한 번이면 된다"고 했지만, 치트로 실행 중에 바뀌니 슬롯 푸시와 함께 가야 한다.
- `Global.GatherSpeedMultiplier` 상수와 시작 경고(`Program.WarnIfTuned`)는 없앴다. 저장하지 않아 재시작하면 ×1이므로, 올려 둔 채 배포되는 사고가 구조적으로 없다.
- `GatherSpeed`는 `GameClock`처럼 인스턴스를 주입한다. 테스트끼리 전역 값을 공유하지 않게 하려는 것이다. 여러 유저가 함께 쓰는 테스트는 `TestUserBuilder.GatherSpeed`에 같은 인스턴스를 넣는다.
- `WorkStationSlot.ApplyWorkSpeed(WorkSpeedBreakdown)`은 **합이 같아도 내역이 바뀌면 true**를 돌려준다. 속도만 비교하면 클라에 옛 내역이 남는다.
- 범위는 ×0.1 ~ ×100(100~100,000‰)이다. 0을 하나 더 친 오타가 판정 폭주로 번지지 않게 막았다.

## 후속 작업 / 주의사항
- 클라: `WorkStationSelectPresenter.RefreshEfficiency`의 베낀 계산을 지우고 받은 필드를 그린다. ★ 줄과 치트 창의 전역 배수 칸도 붙인다(태웅).
- 가산 항목을 새로 만들면 `WorkSpeedBreakdown` · `User.ResolveSlotSpeed` · `WorkStationSlot.ToInfo` · 패킷 필드 네 곳에 함께 붙인다.
- `TestUserBuilder`는 `CharacterLevelCatalog`(Growth)를 기본으로 싣지 않는다. 레벨 가산을 보려면 `b.Growth.LoadAll()`을 부른다.
