---
date: 2026-10-10
title: 인벤토리 칸 획득 반짝임 · 무대 배경 배율 보정 · 대상 크기 배율 (+ T-142·T-143 등록)
tags: [client, ui, art]
---

# 인벤토리 칸 획득 반짝임 · 무대 배경 배율 보정 · 대상 크기 배율

## 목적 / 배경
- 개선 요청 5건 중 바로 할 수 있는 3건(대상 크기 · 배경 단색 노출 · 획득 연출)을 구현하고,
  결정이 필요한 2건(낚시 제거 → T-142 · 뽑기 화면 배치 → T-143)은 일감으로 넘겼다.

## 변경 내용
- `UI/Shared/visual/TargetVisual.cs` — `scale`(기본 1) · 굽기는 SerializedObject로 그림·레벨 색만 써서 덮지 않는다
- `UI/Shared/visual/SlotStageView.cs` — 배경 배율 `kb = max(k, 패널 높이 ÷ stageHeight)` · 땅선은 kb · 대상 크기에 `scale`
- `UI/Shared/SlotGainShine.cs`(신규) — 칸 위 번짐(`gain_glow`) + 빛 줄(`reveal_shine`)
- `InventorySlotSource` — `Rebuild`마다 Key별 수량 비교로 획득 감지(`TakeGain`) · `ResourceSlotSource.AmountOf`
- `InventoryGridPresenter` — `DrawCell`에서 반짝임 · 칸 순서 물결 지연(0.06초 × 최대 8) · `HideView`에서 끊기
- 문서: `Shared 규칙.md` · `Art 규칙.md`

## 주요 결정 / 근거
- **"backgrounds의 pixelScaleOverride"는 실제로 `CharacterVisual`의 값이다** — 배경 SO에는 배율이 없다.
  원인은 패널 높이가 레이아웃 flexible(창 크기)인데 배경 높이는 `stageHeight × 배율`로 고정이라 배율을 낮추면 위가 빈다.
  **SO 단계 clamp는 불가** — 패널 높이는 런타임에만 안다. 그래서 무대가 배경 배율만 따로 올린다.
  기각: ① override 하한 clamp — 키 큰 캐릭터 머리 잘림이 다시 생긴다(두 조건이 충돌) ② 하늘 층만 늘이기 — 층 그림이 위로 이어지지 않는다.
- 대가: kb > k이면 배경 점이 배우 점보다 크다. 땅 흐름은 화면 단위라 대상·땅이 미끄러지지 않는다.
- 반짝임을 `RewardRevealFx` 재사용 대신 새 경량 컴포넌트로 — 공개 연출은 Mask·덮개·숨쉬기 루프까지 들어 칸 수십 개가 들 무게가 아니다. 그림만 같은 것을 쓴다.
- 획득 판단은 격자가 아니라 공급자 — 수량 의미가 탭마다 다르다(`AmountOf`). 자원 수량은 `GetItemCount`가 아니라 Fill에서 기록한다(같은 TID가 창고에도 있을 수 있다).

## 후속 작업 / 주의사항
- `SlotGainShine.cs.meta` 미생성 — **Unity를 열어 갱신한 뒤 함께 커밋**한다(이번엔 에디터 미연결 · dotnet으로 컴파일만 확인, 오류 0)
- 실측 필요: 채취 중 인벤토리 칸 반짝임 · 10연차 물결 · 정렬 응답 때 반짝임 안 남는지 · 키 큰 캐릭터(black-knight 1.4)에서 배경 위 단색 사라짐
- 직전 기록이 비면 반짝이지 않는다 — 자원을 다 팔고 받은 첫 자원은 반짝이지 않는다(로그인 직후 전체 반짝임 방지)
- 그림 저장소(`Assets/Art/`)에 사용자의 미커밋 SO 조정이 있다 — 건드리지 않았다
- → tasks/T-142 · tasks/T-143

## 업데이트 (2026-10-10)
- 사용자 실측 완료 — 대상 크기 · 배경 단색 사라짐 · 칸 반짝임 모두 의도대로. 커밋 `a326b6e` · `05b60df` (그림 저장소 조정값 `90511ac`)
- `.meta`는 에디터 갱신 후 함께 커밋했다
