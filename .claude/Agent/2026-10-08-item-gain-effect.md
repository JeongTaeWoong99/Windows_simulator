---
date: 2026-10-08
title: 아이템 획득 연출 — 큰 창 칸 · 위젯 칸 (T-015)
tags: [client, ui]
---

# 아이템 획득 연출 — 큰 창 칸 · 위젯 칸 (T-015)

## 목적 / 배경
- 칸의 카운트다운이 0이 되어 대상이 쓰러질 때, 얻은 아이템 아이콘이 떠오르며 사라지게 한다. 위젯은 머리 자리에서 작게.
- 공통 컴포넌트 `UI/Shared/ItemGainEffectView` 하나를 두 칸 프리팹 루트에 붙이고 인스펙터 값만 다르게 했다(큰 창 24px·28 상승 / 위젯 12px·16 상승).

## 주요 결정 / 근거
- **트리거는 채취 푸시(`GatherResultReceived`)** — 로컬 타이머는 무엇을 얻었는지 모른다. 푸시는 판정 끝에 와서 대상이 쓰러지는 때와 거의 같다.
  `ItemChanges`는 총량이라 수량은 모른다 — 아이콘만, ItemId로 중복 제거.
- **위치는 `SlotStageView.TryGetHarvestPoint`** — 쓰러지는 대상은 여운 동안 멈춤 자리(stopX − 반폭)에 있어 시간과 무관하게 같은 자리다.
- **칸 루트에 띄운다** — 무대 `Visible Panel`은 `RectMask2D`라 위로 오르다 잘린다. 층은 `LayoutElement.ignoreLayout`.
- **아이콘 층·원본·풀은 코드가 만든다** — 프리팹 에셋이 따로 없다. 꺼 둔 `Pool` 아래 원본 `Image`를 `PrefabPool<Image>`가 찍는다.
- **꺼지는 중에는 반납하지 않는다** — 끊기는 대개 `OnDisable` 안이라 부모를 옮기지 않고 그림만 끈 뒤 다음 재생 때 반납.
- **위젯 "연출 금지" 원칙의 예외** — 수확 때만 1초 남짓 도는 트윈(사용자 요청). `WidgetPresenter` 머리 주석·`Widget 규칙.md`에 적었다.

## 후속 작업 / 주의사항
- ⚠️ **UniTask에 넘긴 트윈을 finally에서 다시 Kill하지 않는다** — 취소 시 UniTask의 Kill 안에서 finally가 동기로 돌아
  두 번째 Kill이 DOTween `TweenManager.RemoveActiveTween`에서 `IndexOutOfRangeException`. 플레이 모드 테스트로 잡았다 → `dotween` 스킬 함정 표.
- 검증: 플레이 모드에서 임시 캔버스로 재생 — 대칭 배치(-42·-14·14·42) · 끝나면 전부 풀로 · 재생 중 끄기/파괴 반복에 오류 0 · 남은 트윈 0.
  **실제 채취 푸시 경로는 서버 없이 못 봤다** — T-015 실측.
- 위젯 머리 "작업 중" 연출은 측면 크롭이라 좌우 흔들기 대신 대안을 목업으로 비교했다 → 아래 '보정'.

## 보정 (같은 날, 사용자 실측 피드백)
- **큰 창 아이콘이 땅과 함께 흐른다** — 화면 좌표에 박혀 있으면 땅이 흐를 때 캐릭터를 따라오는 것처럼 보였다.
  `SlotStageView.FollowGround(point, 재생 시점 GroundDistance)`가 그 뒤로 흐른 거리만큼 옮긴 자리를 주고,
  `ItemGainEffectView.Play(..., follow)`가 시퀀스 `OnUpdate`에서 가로 위치만 덮는다(세로는 상승 트윈 그대로).
  검증: 가짜 follow(초당 100px)로 2.14초 뒤 x=200(기대 214 − 대칭 간격 14) · 오류 0.
- **속도 1.5배** — duration 1.1→0.733(위젯 1.0→0.667) · fadeDelay 0.3 · appear 0.12.
- **위젯 머리 = 통통 바운스 + 수확 펄쩍** (목업 A+D 채택, 땀방울 등 나머지는 버림) → `WidgetHeadMotion`(`Character Image`).
  바운스는 정수 px · 칸마다 위상(SlotIndex×0.37) · 펄쩍 동안 바운스 일시정지 · 세로 찌그러짐은 바닥을 맞춘다.
