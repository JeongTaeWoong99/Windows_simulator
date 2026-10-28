---
date: 2026-10-28
title: 아티팩트 도감 — 장착하면 항상 켜짐 (효과 끄기 제거)
tags: [server, design, docs]
---

# 아티팩트 도감 — 장착하면 항상 켜짐 (T-181 · #90)

## 목적 / 배경
- 사용자가 #81의 "도감에서 켜고 끄기"를 뒤집었다(2026-10-25) — 장착하면 계속 켜 둘 것이라 끌 이유가 없다. 클라가 #90으로 서버 정리를 요청했다.

## 변경 내용
- `User.Artifact.cs` — 도감을 `Dictionary<int,bool>` → `HashSet<int>`. 켜고 끄기(`TrySetArtifactActive`)·`IsArtifactActive` 삭제, 효과 합은 장착 번호 전부.
- `SaveArtifactActiveRepository` 삭제 · 장착 INSERT·로그인 SELECT에서 `is_active` 제거.
- 마이그레이션 `0011` — `t_user_artifact.is_active` 칸 삭제. 꺼 둔 번호도 행은 그대로라 켜진 것이 된다.

## 주요 결정 / 근거
- **패킷은 아직 지우지 않았다** — `C/S_ArtifactActive` · `ArtifactCodexInfo.IsActive`를 지우면 클라(`ArtifactDetailPresenter` · `PlayerDataModel`)가 머지 순간 컴파일이 깨진다. T-183과 같은 방식: 서버는 바꾸지 않고 지금 상태(장착 = Ok · 켜짐)로 답하고, 클라 전환 뒤 지운다.
- **브랜치를 T-183 브랜치 위에 쌓았다(PR 베이스도 그 브랜치)** — T-183 PR(#96)이 `0010`을 쓴다. main에서 따로 따면 `0011`이 `0010` 없이 들어가 번호가 비어 기동이 멈춘다(DbMigrator 규칙).

## 후속 작업 / 주의사항
- #96이 먼저 머지돼야 한다. 머지되면 이 PR의 베이스가 main으로 바뀐다.
- 클라 전환 뒤 `C/S_ArtifactActive`(PacketId 90·91) · `ArtifactCodexInfo.IsActive` · `User.ReplyArtifactActive` · 핸들러를 함께 지운다.
