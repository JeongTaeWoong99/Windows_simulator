---
date: 2026-11-11
title: 아티팩트 지급 치트 칸 · 뽑기권 아이콘 · 코드 몬스터 시험
tags: [client, editor, art]
---

# 아티팩트 지급 치트 칸 · 뽑기권 아이콘 · 코드 몬스터 시험

## 목적 / 배경
- 서버 T-198(PR #119) 머지로 T-193이 풀렸다. 함께 크레딧 0 아트(T-189 뽑기권 · T-174 몬스터 시험)를 진행.
- 일감 → `tasks/T-193-*` · `tasks/art/T-189-*` · `tasks/art/T-174-*`

## 변경 내용
- `Assets/Scripts_Client/Editor/cheat-console/ArtifactCheatSection.cs` — `GiveArtifact` 칸 (지급 분류 Order 4)
- 그림 저장소: `_source/icons/claude-icons/make_tickets.py` → 레시피 100071~100074 → `icons/items/item_10007x` · `_source/characters/claude-mandrake/`

## 주요 결정 / 근거
- 뽑기권 등급은 색이 아니라 재질(종이 · 구리 테 · 은 · 금+루비) — 칸 테두리가 등급색을 보인다. 외곽선은 굽기가 두르므로 그리지 않았다.
- 만드라고라는 TID가 없어 굽지 않고 원본만 둔다(농사 영웅 후보).

## 후속 작업 / 주의사항
- 지뢰: `unity cmd eval`은 메인 스레드 5초 제한 — 아이콘 전부 굽기(`ArtBaker.Bake`)는 `EditorApplication.delayCall`로 넘겨야 한다. internal 타입(`CheatGuard` 등)은 리플렉션으로 부른다.
- 지뢰: 서버 콘솔(`ServerRunner.Start`)이 옛 빌드로 뜬 적이 있다 — 로그에 명령 이름 대신 숫자(`19(...)`)가 찍히면 옛 바이너리. 서버를 내리고 `~/.dotnet/dotnet build` 뒤 다시 띄우면 된다.
- 이슈 #117에서 태웅이 "뽑기권 A~D를 상자로 합친다"는 의견을 냈다 — 확정되면 뽑기권 아이콘 4종은 안 쓰이게 된다.
- T-193·T-189는 사용자 눈 확인만 남음(치트 칸 모양 · 아이콘이 칸에서 읽히는지). #112는 그 뒤 닫는다.
