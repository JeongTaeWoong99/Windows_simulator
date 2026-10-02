---
date: 2026-10-02
title: 클라 특성 화면을 레벨형(산업 × 종류 표)으로 · 산업 레벨 잠금 · 산출량 줄 (T-116 · #50)
tags: [client, ui, data]
---

# 클라 특성 화면을 레벨형으로 (T-116 · 이슈 #50)

## 목적 / 배경
- 서버가 특성을 노드 사슬 → 레벨형으로 바꿨다(T-108 · `83f8616`). 클라는 옛 해금 목록으로 읽어 특성이 전부 안 배운 것으로, 상위 산업 레벨이 잠금 없이 보였다.
- 화면은 제안 3안(표 · 종류 탭 · 산업 탭) 중 **A안 산업 × 종류 표**로 사용자가 정했다.

## 변경 내용
- `ServerPacketHandler` — `S_UserTraitListResponse` 핸들러 · 이벤트.
- `PlayerDataModel` — `_traitLevels` · `GetTraitLevel` · `IsIndustryLevelOpen` · `GetTraitEffectSum`(서버 `User.SumTraitEffect` 사본) · `TraitsChanged`. 올리기 응답의 `Level`을 실패여도 반영.
- `GameDataLoader` — `TryGetUserTraitLevel` · `TryGetIndustryUnlockTrait` 추가, `GetIndustryLevelUnlockTid` · `TryGetIndustryLevelByUnlockTid` 삭제.
- `TraitPresenter` 재작성(3열 표 · 구역 탭 삭제 · [레벨 올리기]) · `TraitNodeView`(상태 MaxLevel/Available/Locked · `BindBlank` · 잇는 선 삭제).
- `WorkStationSelectPresenter` — 레벨 잠금 3곳 · 속도 가산 · 효율 계산에 **산출량 줄**(5줄) · 잠김 툴팁 문구.
- 씬: `Trait Tab Panel` 삭제 · 격자 3열(191×44 · 8×6) · 버튼 문구 · 본문 자동 크기 14~22. 프리팹: `Link Image` 삭제 · 글자 14/11.
- `ResultMessages` — `TraitMaxLevel`.

## 주요 결정 / 근거
- 표의 칸은 (산업, `EffectType`)로 찾는다 — TID 대역을 베끼지 않는다. 없는 칸은 빼지 않고 `BindBlank`(바탕째 끔) — 빼면 열이 어긋나고 회색이면 "잠김"으로 읽힌다.
- 포인트 부족은 색으로 가르지 않는다 — 모든 칸이 회색이 된다.
- 산출량 설명 문구는 사용자 요청 — "좋은 게 나올 확률이 오르나?" 오해 방지. 같은 자원이 더 · 희귀도 그대로 · 상자·경험치 안 늚.
- ⚠️ `→` 글리프가 `neodgm_pro SDF`(Static)에 없다 — "지금 효과" / "다음 레벨" 두 줄로 썼다. 새 문구를 넣을 땐 `HasCharacter`로 확인할 것.

## 후속 작업 / 주의사항
- ⚠️ **에디터가 pull 전 씬을 들고 있다가 저장하면 들어온 씬 변경이 되돌아간다.** 이번에 `79054b2`의 하단 메뉴 "거래"가 "거래소"로 돌아가 있어 되돌렸다. 씬 커밋 전 `m_text` diff를 본다.
- 치트 창 특성 묶음은 T-115(서버 특성 레벨 치트 · #51) 대기.
- 실측 후 #50에 처리 코멘트.
