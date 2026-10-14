---
date: 2026-10-15
title: 캐릭터·장비 잠금 클라 — Ctrl+좌클릭 토글 · 칸 자물쇠 · 판매·경매·응축 재료 제외 (T-137 · 이슈 #65)
tags: [client, ui, inventory]
---

# 캐릭터·장비 잠금 클라 (T-137)

## 목적 / 배경
- 서버(`3a898ed`)가 잠금 패킷·`IsLocked`·거절(`EntityLocked` 1200)을 넣었다. 클라 몫: 토글 · 표시 · 미리 막기.

## 주요 결정 / 근거
- **조작은 Ctrl+좌클릭** — 사용자가 골랐다(AskUserQuestion). 키 단독 단축키는 넣지 않는다는 규칙(T-158)과 맞게 마우스 조합. `KeyInputManager.IsLockToggleHeld`.
- **응답 뒤에 갱신**(낙관적 갱신 없음) — 자물쇠가 서버와 어긋나면 "잠근 줄 알았는데 팔렸다"가 된다. 목록이 다시 오지 않으므로 `PlayerDataModel.OnEntityLockResponded`가 캐시 `IsLocked`를 고치고 `CharactersChanged`·`EquipsChanged`를 낸다. 연타는 `_lockPending`으로 막는다.
- **막기는 `EntityBlockText`에서 맨 먼저** — 판매 담기(우클릭·일괄)·경매 등록 흐린 줄이 한 번에 막힌다. 응축은 `CanBeMaterial`에 `!IsLocked`(대상은 허용 — 서버와 같다).
- **자물쇠는 칸 오른쪽 위** — 처음 왼쪽 위에 뒀더니 이름 첫 글자를 가렸다. 오른쪽 위 '판' 표시는 잠긴 개체에 뜰 수 없고 '배'는 자물쇠 위에 그린다.
- `lock.png`는 PIL로 만든 16px 임시 픽셀 그림(Point 필터).

## 겪은 함정
- 실측 중 `CharacterCondensePresenter`의 private `_targetId`를 리플렉션으로 바꿨다가 예외로 되돌리지 못해 격자 그리기에서 NRE가 났다 — 테스트 부작용이다(플레이 재시작으로 정상). 초기화 전 Presenter의 필드를 직접 만지지 말 것.

## 후속 작업 / 주의사항
- 실제 Ctrl+좌클릭 입력은 사람 손 실측으로 남았다(자동 입력이 키에 닿지 않는다 — 토글은 `ToggleLock` 리플렉션으로 확인).
- 장비 분해(T-131)가 생기면 그쪽도 잠금을 막아야 한다(서버는 T-131 때 검사).
