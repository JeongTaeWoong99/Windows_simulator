---
date: 2026-11-02
title: 옛 계정 특성 잔재 삭제 — UserTraitLevelTable · S_AccountLevelResponse.TraitPoint (T-183)
tags: [server, data, protocol, trait]
---

# 옛 계정 특성 잔재 삭제 (T-183 마지막 항목)

## 목적 / 배경
- 이슈 #75 — 클라 거점 화면(T-182)이 main에 올라가 두 잔재를 더 안 쓴다는 확인을 받고 지웠다

## 변경 내용
- `User.xlsx`의 `UserTraitLevelTable` 시트 삭제 → 파이프라인이 `.cs`·`.bytes`·`.json`·Unity 미러(+ `.meta`)까지 지웠다
- `S_AccountLevelResponse.TraitPoint` 필드 삭제 — `ServerPacketHandler`의 수신 로그도 함께 고쳤다(안 고치면 Unity 컴파일이 깨진다)
- 기획 문서(`trait` · `unlock` · `문서관계도`)와 T-183 일감 체크

## 주요 결정 / 근거
- `UserTraitTable.TraitPoint`(노드 레벨당 비용)는 **다른 것**이라 그대로 둔다 — 이름이 같아 grep에 함께 걸린다
- DB 마이그레이션 없음 — `t_user_account.trait_point`는 `0010`에서 이미 지웠다

## 후속 작업 / 주의사항
- Unity 에디터로 컴파일은 돌려 보지 않았다 — `Assets/` 안 `.cs` 전체 grep으로 참조가 없음만 확인
- T-183은 "작업장 외 4채 노드 목록·수치"가 남아 In Progress 유지
