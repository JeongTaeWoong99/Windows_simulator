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
- 위젯 머리 "작업 중" 연출은 측면 크롭이라 좌우 흔들기 대신 대안을 목업으로 비교 중(사용자 선택 대기).
