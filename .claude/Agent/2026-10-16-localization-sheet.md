---
date: 2026-10-16
title: 다국어 시트 구조 — lstring · Localization.xlsx (T-135 · 이슈 #64)
tags: [excel, generator, localization, pipeline]
---

# 다국어 시트 구조

## 목적 / 배경
- 클라 언어 선택(T-136)을 위해 엑셀 문구를 언어별로 둔다. 언어가 늘어도 시트를 갈아엎지 않는 모양.

## 주요 결정 / 근거
- **(다) 혼합안** — (가) 언어별 컬럼은 언어마다 모든 시트를 고쳐야 하고, (나) 손키 문자열 시트는 기획자가 키를 관리하고 데이터 시트에서 이름이 안 보인다. 데이터 시트는 KR 원문 그대로 두고 생성기가 번역 시트 행을 채운다.
- **대상 표시는 `Type = lstring`** — 예약 이름(`Name`)으로 정하면 `ConstantsTable.Name`(키) 같은 예외가 생긴다. 사용자 확인.
- 시트명은 병합 규약을 따라 `LocalizationTable.Data`/`.UI` — 시트명이 곧 테이블명이라 `Data`로는 못 쓴다. bytes도 하나(언어별 분리 안 함 — 414행이라 이득 없음). 사용자 확인.
- **원문 변경 감지 = 시트의 KR 칸 자체가 스냅샷.** 생성기가 덮어쓰기 전 값과 비교해 `🔄 EN`을 남기고, 사람이 상태 칸을 지울 때까지 유지한다(재번역 여부는 알 수 없다). DataLog 비교는 보간 후 문장이라 숫자만 바뀌어도 오탐이 나서 버렸다.
- 시트는 내용이 바뀔 때만 저장한다 — 매번 저장하면 xlsx 바이트가 바뀌어 diff가 남는다.
- 원문이 빈 셀(빈 Tooltip 252개)은 시트에 올리지 않고 런타임 테이블에만 빈 값으로 넣는다 — `Loc.Get`이 키 문자열을 내보내지 않도록.
- `Tooltip` 번역은 행을 복제해 lstring 칸을 그 언어 값으로 바꾼 뒤 보간 → `{Name}`이 영어 이름으로 들어간다.
- Row에 `NameLocKey` 계산 속성(`[MemoryPackIgnore]`). DataLog·Preview JSON은 `IgnoreReadOnlyProperties`로 뺐다 — 안 빼면 모든 DataLog에 키가 섞인다.
- 우편은 이미 `TemplateTid`만 보내고 있어 패킷 변경 없음.

## 겪은 함정
- 워크트리 격리 때문에 `cd`·복합 명령이 거절된다 → 스크립트는 스크래치패드에 Write로 쓰고 절대경로로 실행.
- 생성기(ClosedXML)가 저장한 시트는 서식이 풀린다 → `format-excel.py Localization.xlsx` 한 번 더. 이후 재생성은 저장하지 않는다(멱등 확인).

## 후속 작업 / 주의사항
- 클라(T-136): `Loc.Current` 설정 · 화면 문구를 `Loc.Get(row.NameLocKey)`로 · 고정 문구는 `UI` 시트에 키 추가.
- EN 386칸 비어 있음 — 배포 전 `-LocStrict`로 막는다.
- 새 `.meta` 3개(Loc.cs · LocalizationTable.cs · LocalizationTable.bytes)는 손으로 GUID를 만들었다. Unity가 열 때 다시 쓰면 그 diff를 커밋.
