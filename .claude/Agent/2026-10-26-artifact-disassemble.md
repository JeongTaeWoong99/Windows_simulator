---
date: 2026-10-26
title: 아티팩트 마석 분해 — 장비와 한 요청으로 (T-185 · 이슈 #89)
tags: [server, packet, data, artifact, disassemble]
---

# 아티팩트 마석 분해

## 목적 / 배경
- 이슈 #89 — 판매 목록에서 장비는 "골드 OR 마석", 아티팩트는 골드만이라 어긋났다. 사용자 결정(2026-10-26): 아티팩트도 마석으로 분해 · 등급별 장비의 2배 · 장비와 한 번에 분해

## 변경 내용
- `Equip.xlsx` `DisassembleTable`에 `ArtifactItemCount` 열(2·4·10·30·100·300) — 열 이름은 사용자 확인
- `C_EquipDisassembleRequest`에 `List<ArtifactInfo>? Artifacts` — 패킷 이름은 그대로 두었다
- `User.TryDisassembleEquips` → `TryDisassemble(equipIds, artifacts)` — 같은 번호 여러 줄은 합친다
- `DisassembleEquipsRepository`가 아티팩트 칸 변경분도 같은 트랜잭션에 쓴다(`ArtifactStackDb.WriteAllAsync`)

## 주요 결정 / 근거
- 배수 상수(`Constants`) 대신 표의 열 — 등급마다 따로 조정할 수 있게(사용자 선택)
- 새 패킷을 만들지 않고 기존 요청에 필드 추가 — 클라가 판매 목록 한 번의 [마석으로 분해]로 장비·아티팩트를 섞어 보낸다. 응답은 그대로이고 아티팩트가 빠지면 `S_ArtifactInventoryResponse`가 따라온다
- 아티팩트 잠금은 아직 없다(#87) — 잠금이 생기면 분해 검증에 넣어야 한다

## 후속 작업 / 주의사항
- 이 Mac에는 `pwsh`가 없어 `generate-tables.ps1`·`sync-protocol-to-unity.ps1`을 못 돌렸다. `dotnet run --project Server/ExcelGenerator -- <루트>` 후 `DisassembleTable.cs`·`.bytes`·`MikaPacket.cs`를 Unity 미러로 손으로 복사했다(원본과 미러가 바이트 단위로 같음을 확인)
- Mac 빌드가 `Assets/Plugins/Analyzers/MikaSourceGen.dll`을 다시 써서 바이너리가 바뀐다 — 커밋하지 않고 되돌렸다
- 클라: 마석 0 임시값 · 잠긴 [마석으로 분해]를 `ArtifactItemCount`와 `Artifacts` 필드로 바꾸면 된다
