---
date: 2026-10-25
title: 캐릭터 뽑기권 A~D · 골드 상점 구매 (#78 · T-184)
tags: [design, data, server]
---

# 캐릭터 뽑기권 A~D · 골드 상점 구매

## 목적 / 배경
- #78(뽑기 물량 대비 칸 부족) — 2026-10-24 회의로 캐릭터는 골드 직접 뽑기 → 상점 뽑기권으로. 결정·수치 → `tasks/T-184` · 캐릭터 5.1

## 주요 결정 / 근거
- **뽑기권 개봉은 새 코드 없이 상자 경로(`OpenGachaId` → `GachaService.OpenBox`)를 탄다.** 캐릭터 풀도 칸 검사·우편 보관이 이미 된다. 새로 짠 서버 코드는 상점 구매(`ShopService.Buy`)뿐
- 가중치는 등급 종 수(6·8·7·5·3·1)의 최소공배수 840 × 확률% — 풀 합계 84,000, 등급 확률이 정수로 정확히 떨어진다(D의 7.5%·0.5%까지)
- 구매 개수 상한은 새 상수 없이 `BoxOpenMax`(50)를 재사용 — "산 만큼 바로 연다"
- 골드 직접 캐릭터 풀(2)은 **아직 열어 둔다** — 클라 상점이 없을 때 닫으면 캐릭터를 얻을 길이 없다
- `BoxSheetTest`의 "열 수 있는 아이템에서 캐릭터 안 나옴" → "상점 품목만 캐릭터를 준다"로 바꿨다

## 함정 (맥에서 파이프라인)
- 맥에는 pwsh가 없다 — `generate-tables.ps1` 대신 `dotnet run --project Server/ExcelGenerator -- <루트>` + C# 9 grep + rsync 미러로 같은 순서를 밟았다. dotnet은 `/usr/local/share/dotnet/x64`(DOTNET_ROOT 지정)
- 생성기가 **OS를 타던 두 곳**을 고쳤다(`ExcelGenerator.cs`): ① 엑셀 나열 순서(APFS는 이름순이 아니다 → `GameTable.cs`·`TableRegistry.cs` 순서가 뒤집힘) ② 셀 안 줄바꿈(ClosedXML이 OS 줄바꿈으로 돌려줘 LF가 됨 → CRLF 고정). 고친 뒤 변경 없이 돌리면 차이 0
- 패킷 미러(`sync-protocol-to-unity.ps1`)도 안 돌아 손으로 복사했다. 맥 빌드가 `Assets/Plugins/Analyzers/MikaSourceGen.dll`을 바꾸므로 되돌렸다
- 새 Unity 파일 `.meta`(ShopTable.cs·.bytes)는 GUID를 만들어 넣었다 — 에디터에서 다시 만들지 않는지 확인

## 후속 작업 / 주의사항
- 클라(태웅): 상점 화면 · 캐릭터 뽑기 탭 교체 · `S_ShopBuyResponse` 처리 → #78
- 클라 상점이 붙으면 `GachaInfoTable` 2를 `None`으로 닫는다(T-184)
- 장비 중복 처리는 미결 → T-169
