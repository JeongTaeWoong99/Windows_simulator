---
date: 2026-10-21
title: 클라 컴파일 경고 정리 — UIManager 서비스 해제 · null 대입 · CS4014
tags: [client, cleanup, nullable, service]
---

# 클라 컴파일 경고 정리

## 목적 / 배경
- 클라 경고 15개가 늘 떠 있어 새 경고가 묻혔다. 남은 것은 패킷 핸들러 없음(MIKA001) 2개뿐 — T-151·T-131 화면에서 붙는다.

## 변경 내용
- `UIManager.OnDestroy` — `private` → `protected override` + `base.OnDestroy()`. 부모(`MonoService`)의 `Services.Unregister`가 가려져 있었다(CS0114 · 잠재 버그).
- `Image.sprite = x!` 12곳 — 그림을 비워도 되는 대입(CS8601). 뜻은 `폴더 구조.md` 3-1에 적었다.
- `InventoryGridPresenter.DrawCell` — `_current!.IsAway` (위 `cell` 판정이 공급자 있음을 보장 · CS8602).
- `ItemGainEffectView` — `seq.OnUpdate`를 async 밖 `FollowHorizontally`로(CS4014 · `BuildSequence`와 같은 이유).

## 주요 결정 / 근거
- sprite 대입의 `!`는 "null 아님"이 아니라 "null이어도 됨"이다 — Unity API가 null 허용을 표기하지 않아 생기는 경고. 자리마다 주석 대신 규칙 문서 한 곳에 둔다.

## 후속 작업 / 주의사항
- `MonoService` 상속 클래스에 `Awake`·`OnDestroy`를 새로 쓸 때는 `override` + `base` 호출(PingManager·DisplayManager 모양).
