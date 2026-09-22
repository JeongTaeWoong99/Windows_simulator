---
date: 2026-09-22
title: 치트 Unlock으로 연 속도 특성이 즉시 반영되게 (#31) · 가챠 1번 풀 유지 확인 (#30)
tags: [server, cheat, trait, unlock, gacha, issue]
---

# 치트 Unlock 특성 속도 즉시 반영 (#31) · 가챠 1번 풀 유지 (#30)

## 목적 / 배경
- #31: 치트 `Unlock = 7`로 속도 특성 노드를 열면 `RefreshWorkStationSpeed`가 불리지 않아, 속도가 다음 재정산 때에야 붙었다. 이 때문에 치트로 한 검증 결과를 믿을 수 없었다.
- #30: 클라가 1번 가챠 풀(구슬) 버튼을 뺐다. 서버 테이블을 남겨도 되는지 확인해 달라는 요청.

## 변경 내용
- `Server/WSGameServer/User/User.Trait.cs` — `TryLearnTrait`에서 속도 재정산을 빼고 `OnTraitUnlocked`로 분리
- `Server/WSGameServer/User/User.Unlock.cs` — `ApplyUnlock` 후속에 `OnTraitUnlocked` 추가
- `Server/WSGameServer/User/WorkStation/WorkStationSlot.cs:55` — "T-017 미구현"이라고 적힌 낡은 주석 수정 (클라가 알려 줌)
- `Server/docs/치트.md` — `Unlock` 행에 "특성도 열림 · 속도 즉시 · 포인트 차감 없음" 추가
- `UserTraitTest` — `서버가_지급한_속도_특성도_그_자리에서_속도에_붙는다` 추가

## 주요 결정 / 근거
- 기각한 안: 치트에서 특성 노드를 거절하는 안, `LearnTrait = 10` 명령을 새로 만드는 안. 대신 **해금 공통 후속으로 옮겼다.** 이렇게 하면 퀘스트 보상 `GrantUnlock`까지 한 번에 같은 시점으로 맞고, 클라 치트창도 고칠 게 없다.
- 치트 `Unlock`은 포인트를 빼지 않는다. "조건·차감 없이 연다"는 정의를 그대로 따랐다. 포인트 검증은 정식 경로(`C_UserTraitLearnRequest`)로 한다.
- 패킷 순서가 바뀌었다: `S_UnlockResponse` → 슬롯 동기화 → `S_UserTraitLearnResponse`.
- #30은 서버 변경 없음. `GachaServiceTest`가 `GachaInfoTable` 1번 행을 실제로 읽어서, 지우면 테스트가 깨진다.

## 후속 작업 / 주의사항
- #31·#30 모두 처리 댓글을 달았고, 제목을 `→ 클라 확인`으로 바꿔 담당자를 클라(JeongTaeWoong99)로 넘겼다. 닫는 건 클라가 한다.
- 커밋은 아직 하지 않았다.
