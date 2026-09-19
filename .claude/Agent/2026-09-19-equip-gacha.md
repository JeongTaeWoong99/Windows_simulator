---
date: 2026-09-19
title: 장비 뽑기 — 무기·장신구·보석 풀 3개 (T-067 서버 몫)
tags: [server, gacha, equip, excel, protocol]
---

# 장비 뽑기 — 무기·장신구·보석 풀 3개 (T-067 서버 몫)

## 목적 / 배경
- 장비를 얻는 정식 경로가 치트뿐이었다. 사용자가 가챠로, **무기·장신구·보석을 따로** 뽑게 하기로 했다.

## 변경 내용
- `Gacha.xlsx` — `GachaEquipTable` 시트 신설(69행, `GachaItemTable` 복사 모양 · `Ref` → `EquipTable.EquipTID`), `GachaInfoTable`에 풀 3·4·5
- `Enum.xlsx` `GachaRewardType.Equip = 3` · `MikaProtocol` `EGachaRewardType.Equip = 3` · `GachaRewardInfo.EquipTid`(마지막 필드)
- `GachaPoolCatalog.LoadAll` — 장비 시트도 조립 · `GachaService` — 장비 1개당 `GrantEquip`, 결과에 `EquipTid`·등급(`EquipTable`)
- 테스트: `GachaServiceTest` 장비 풀 1건 · `GachaEquipSheetTest`(풀당 종류 하나 · 세 종류 각자 풀 · 비용 메타 존재)
- 문서: 캐릭터 5.1.1(신설) · 게임기획코어 5장 · 아이템 4장 · 거래 3.2 · 문서관계도 · `Server/docs/데이터-카탈로그.md` · 일감 T-067·T-029·T-043·T-063·INDEX

## 주요 결정 / 근거
- 비용: 무기 500/5,000 · 장신구 300/3,000 · 보석 200/2,000 (판매가 기댓값보다 높게 — 사용자가 이 제안값으로 확정). 가중치는 아이템 풀과 같은 등급별 테스트값
- `GachaEquipTID` 대역 = 풀 번호 × 1000 + 순번 (3xxx·4xxx·5xxx) — 기존 1xxx 아이템 · 2xxx 캐릭터와 겹치지 않는다
- 장비는 개체마다 창고 칸을 따로 잡아(`NextFreeEquipPosition`) 1개씩 지급한다. 캐릭터처럼 한 번에 묶지 않았다

## 후속
- 클라 배선(가챠 버튼 3줄 · `ToSlotData` Equip 분기) — 클라 몫. 그 전까지 T-067은 진행중
- Unity가 새 미러 파일(`GachaEquipTable.cs` · `.bytes`)의 `.meta`를 만든 뒤 함께 커밋
- T-063 창고 한도 검사는 장비 뽑기 경로에도 걸어야 한다
