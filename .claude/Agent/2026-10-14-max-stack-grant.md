---
date: 2026-10-14
title: 최대 수량 지급 통일 · 수확 가득 참 알림 (T-157)
tags: [server, inventory, protocol, test]
---

# 최대 수량 지급 통일 · 수확 가득 참 알림 (T-157)

## 목적 / 배경
- 이슈 #67 — 수확은 칸·최대 수량으로 버린 몫을 알리지 않았고, 우편·상자·뽑기는 `MaxStack`을 검사하지 않아 9,999를 넘어 쌓였다

## 변경 내용
- `User.GainItem`이 `ClampToMaxStack`으로 자른다 — 지급 경로가 전부 여기를 지나 한 곳으로 통일
- `S_GatherResultResponse.InventoryFull` — 칸 부족 또는 최대 수량으로 버린 몫이 있으면 true
- 경매 등록 테스트 2개가 10,000개 보유를 전제했다 → 9,999로 맞춤

## 결정 (사용자)
- 받지 못한 목록(`List<ItemInfo>`)은 싣지 않는다 — "가득 차서 획득 불가"만 알린다
- 우편·상자도 거절하지 않고 넘친 몫은 버린다. 한 종류 = 여러 칸(2단계)은 하지 않는다

## 남은 것
- 클라: `InventoryFull`로 "가방 가득" 추정(`ReadGains`) 교체 · `Market 규칙.md`의 "보유량은 MaxStack을 넘을 수 있다" 문구 (#67 → 태웅)
