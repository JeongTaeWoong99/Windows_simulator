---
date: 2026-09-19
title: 상자 개봉 · 채취 공통 보상 (T-029 · T-030)
tags: [server, gacha, item, excel, protocol]
---

# 상자 개봉 · 채취 공통 보상 (T-029 · T-030)

## 목적 / 배경
- "직접 여는 순간"이 없었다. 사용자 결정: 상자 3등급(Common·Uncommon·Rare) · 아이템 취급 · 최대 99 · TID 가챠 전용 대역 ·
  캐릭터 없음 · 골드(구간 Min~Max × 가중치) · 자원 · 장비 · 상자 전용 특수 아이템 · 상자 풀은 골드로 못 뽑음 ·
  획득은 전 산업 판정 1회마다 상자별 아주 낮은 확률.

## 변경 내용
- 엑셀: `Item.xlsx` `OpenGachaId` 컬럼 · 상자 100007~9 · 특수 100010~12(임시 이름) — 이후 `ItemTable.자원`·`ItemTable.Special` 두 시트로 분리 / `DropCommon.xlsx` `CommonRewardTable` /
  `Gacha.xlsx` 상자 풀 6·7·8(비용 None, 비용 Min 1→0) · 아이템·장비 행 · `GachaGoldTable` / `Enum.xlsx` `GachaRewardType.Gold = 4`
- 프로토콜: `C_ItemUseRequest`(37) · `S_ItemUseResponse`(38) · `EGachaRewardType.Gold` · `ItemNotUsable`(302) · `InvalidUseCount`(303)
- 서버: `GachaService` — `Draw`/`OpenBox`가 `Grant` 공유, 비용 None 풀 거절, 골드 구간 무작위(`GachaEntry.MaxCount`) ·
  `CommonRewardCatalog`(행마다 독립 확률, 백만분율) · 정산에 롤 추가 · `User.TryConsumeItems` · `SaveItemChangesRepository` · 핸들러
- 더미 클라: 상자 열기 메뉴 · `S_ItemUseResponse` 핸들러
- 테스트: `BoxOpenTest` 9 · `BoxSheetTest` 5 · `CommonRewardTest` 3 · `GachaEquipSheetTest`를 상점 풀로 한정. 394 통과 · 1 Skip
- 실측: 나무 10 · 은 3 · 황금 2 개봉, 거절 3종, 상자 풀 골드 뽑기 거절. DB는 테스트 전 백업으로 되돌림
- 문서: 아이템 1.4 · 4.1(신설) · 자원채취 · 게임기획코어 5장 · 데이터-카탈로그 3.1 · 8장. 일감 T-029·T-030 진행중 · T-033 ▶

## 주요 결정 / 근거
- T-030의 "미당첨 가중치"안 대신 **행마다 독립 확률** — "각각 엄청 낮은 확률" 요청. 한 판정에 둘이 나올 수 있다.
- 상자→상자 순환은 코드가 아니라 데이터로 막고 `BoxSheetTest`가 지킨다.
- 테스트 빌더는 공통 보상을 비워 둔다 — 난수가 다른 정산 테스트를 흔들지 않게.

## 후속
- 상자 전용 특수 아이템 이름·용도 · 확률/골드 수치 확정 · T-030 실측(확률을 잠시 올려야 보인다) · 클라 T-033
- 파이프라인 출력을 `Select-Object -First N`으로 자르면 미러 복사가 중간에 멈춘다 — 끝까지 돌린 뒤 거른다.
