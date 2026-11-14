---
date: 2026-11-14
title: 코드 그리기 파이프라인 claude_gen.py · 팰월드식 칸 · 툴팁 묶음 색 · 등급 계단빛 되살림
tags: [client, ui, art, pipeline]
---

# 코드 그리기 파이프라인 · 팰월드식 칸 · 툴팁 묶음 색

## 목적 / 배경
- 사용자: 유튜브 쇼츠 2편("AI 게임 리소스 제작에 사용할 수 있는 오픈소스 8가지" 1·2편)의 8가지를 분석·이식, AI 크레딧 없이 리소스를 만드는 방법 연구, 반복 작업 파이프라인 구축, 아래 UI 작업으로 시험.
- **영상 본문(8가지 목록)은 읽지 못했다** — 웹 가져오기는 제목만 나왔고, 자막을 받으려던 yt-dlp 설치는 자동 권한 판정에서 막혔다. 목록을 받으면 이어서 이식한다.
- UI: 팰월드식으로 간다 · 툴팁 등급 묶음 색 전부 · 조작은 연한 흰 줄 · 아이템·작업슬롯 칸 속 바닥→위 그라데이션 되살리기 · 작업슬롯 위쪽 확장(이해 확인 HTML).

## 변경
- 파이프라인 `Assets/Art/_source/claude_gen.py` — 생성기 6종(ui·slots·icons·fx·tickets·hit) 등록 · `list` · `run <이름|all|changed>` · `--sheet`(바뀐 PNG만 contact.png) · `--unity`(새로 고침 + 뒤처리 메뉴, 플레이 중이면 건너뜀) · 상태 파일은 gitignore.
- `make_slots.py` `rarity_bg` — 2차 양피지에서 바닥 두 줄로 줄였던 계단빛을 바닥~위 전체로 되돌림(속 검정 48).
- 팰월드식 칸: `SlotView.SetNameVisible` · `SetSubText` · 격자가 이름 끔 · 적성 스트립·장착 네모 끔 · 캐릭터 보조 문구 = 가장 높은 적성 '채굴 9'.
- 툴팁: `TooltipPresenter.PaintRows` — 묶음별 칠하기 · `TooltipRowView.Bind(line, tint, blend)` · `TooltipContent.HeaderMark`.
- 문서: Art 규칙(파이프라인 절) · Inventory 규칙(팰월드식 칸) · Shared 규칙(툴팁 묶음).

## 결정
- 작업슬롯 칸 속 등급 채움은 `rarity_bg`를 되살리는 것으로 함께 해결(작업슬롯도 `RaritySkin`을 쓴다) — 따로 코드 없음.
- 작업슬롯 위쪽 확장은 구현하지 않았다 — 배경 그림이 128px이고 지금 112px로 거의 다 보여서, 넓히면 배경 확대(A) 아니면 위쪽을 이어 그리기(B)라 사용자 선택 필요. 바탕화면 `작업슬롯 위쪽 확장 확인/확인.html`(A안은 실제 플레이 캡처).

## 확인
- `claude_gen.py run` 전부 — slots만 rarity_bg 1장 바뀜, 나머지 0장(재현성).
- 플레이 캡처: 자원·캐릭터 칸 · 툴팁 3종(스페셜·신화·고급 캐릭터) · 작업슬롯 칸 속 그라데이션.

## 남은 일
- 영상 8가지 목록 받기 → 이식.
- 작업슬롯 위쪽 확장 A/B 선택.
