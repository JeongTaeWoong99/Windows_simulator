---
date: 2026-11-22
title: 건물 업그레이드 소리 · 큐브 편의 줄 다듬기 · 영어 칩 짧게 · 에디터 크래시 원인 · 치트 창 프레임 튐
tags: [client, ui, audio, editor, perf, data]
---

# 건물 업그레이드 소리 · 큐브 편의 줄 · 크래시 · 144프레임

## 목적 / 배경
- 사용자: 건물 업그레이드 소리 없음(레벨 업 재사용 가능) · 마을 1~5단계 사진 5장(기획 공유) · 빨간 점 확인 · 큐브 편의는 [칸 초기화] 빼고 두 토글을 뜻대로 길게 · 영어 칩이 너무 작다 · 유니티 크래시 원인 · 에디터에서 144프레임이 안 나오는 이유를 찾아 고칠 수 있으면 고치기.

## 변경 내용
- `OutpostUpgradePresenter.OnUpgradeCompleted` — 성공이면 `Sounds.LevelUp`(캐릭터 레벨 업과 같은 소리). `Sounds.cs` 주석.
- 큐브 편의 — `optionChips` 2개(`OptionResetSlots` 삭제 · 씬 `Option Row/Chip (2)` 삭제 · 배열 2개로). 문구 `신화에 도달하면 멈춤` / `목표 등급보다 높으면 멈춤`(EN `Stop at Mythic` / `Stop above Goal`).
- 영어 칩 짧게 — `EnchantAutoTarget.GetChipName` + 목표 등급 칩 `UI.Enchant.Chip.<등급>`. 새 키 10개 `UI.Enchant.Chip.*`(Uncom. · Rare · Epic · Legend · Mythic · Farm · Mine · Wood · Hunt · Dig) · `UI.Enchant.TargetGrade` EN `Goal` · `UI.Enchant.ResetSlots` 삭제. 파이프라인 돌림.
- `CheatGuard.GetCompletedStepsPolled` — 서버·클라 단계(OS 연결 목록)를 `Task.Run`으로 2초마다, 로그인만 메인 스레드. `CheatWindow`가 이것을 쓴다. `CanSend`는 그대로 즉시 검사.
- 문서 — `Inventory 규칙.md`(편의 줄 · 영어 칩) · `cheat-console 규칙.md`(상태 줄 주기).

## 주요 결정 / 근거
- 크래시(2026-11-22 05:04 덤프 `Crash_2026-11-21_200414372`) — 로그 마지막 줄 `Allocating too large buffer for profiler!` → `profiling::Dispatcher::AcquireFreeBuffer` ← `PerThreadProfiler::EmitUIEvents` ← `ProfilerSession::EmitEditorGlobalStats`. 프로파일러가 UI 이벤트를 담을 버퍼를 너무 크게 잡다 죽은 것 — 시스템 메모리 부족이 아니라 프로파일러 내부 버퍼 한도. 프로파일러를 켠 채 플레이 중(수집 결과 직후)에 났다.
- 144프레임 — 측정(플레이 · 로그인 · 메인 화면): PlayerLoop(게임 코드) 중앙 1.1ms · 99% 1.7ms. 프레임 중앙 6.96ms = 144 목표에 붙어 있고 나머지는 목표 대기. 튀는 프레임의 주범은 **치트 창 `OnUpdate`가 0.5초마다 4~7ms**(OS TCP 목록 두 번 조회 · 할당 → 한 번은 GC 16.6ms 겹쳐 46ms). 고친 뒤 같은 조건 99% 13.1 → 10.2ms, 치트 창 튐 사라짐.
- 못 고친 것(에디터 몫): Unity 내장 `LevelPlayInstallMenuItem`·`InAppPurchasingInstallMenuItem.UpdateMenuItem` 매 프레임 ≈1ms · 게임 뷰 그리기 · 프로파일러/Rider 프로파일러 창 자체(한 번 16.9ms).

## 검증
- 큐브 창 한글·영어 미리보기 렌더 — 칩 글씨 거의 줄지 않음.
- 마을 1~5단계(전 건물 Lv 5·30·60·100·140) 스크린샷 5장. 빨간 점 = 작업장만 — 값 확인: 모든 건물 `BuildingAccountLevelCap`(계정 Lv1), 특성 가능은 작업장(TID 2)만.
- 건물 업그레이드 소리는 실제 업그레이드로 들어 보지 못함(시험 계정 골드 0 · 계정 Lv1 상한).

## 후속 작업 / 주의사항
- 시험 계정 `claude-village` 건물이 전부 Lv140으로 남았다(로컬 DB).
- 프로파일러를 쓸 때 UI · UI Details 모듈을 끄면 크래시 원인 경로(`EmitUIEvents`)를 피한다.

---

## 2차 (같은 날) — 마을 4·5단계 다시 · 치트 창 튐 원인 상세

### 배경
- 사용자: 1~3은 크기·구성이 달라 티가 나는데 4·5는 '기존 형태에 꾸미기'라 안 보인다. 바탕도 4·5는 작은 소품만 는다 — 자체 분석까지 해서 확확 바뀌게.

### 분석
- 건물: 3→4는 같은 폭에서 키만 조금 · 4→5는 1~2px 소품(깃발 줄 · 등 · 불똥)이라 지도 배율에서 사라진다. 캔버스 92×74의 위가 이미 꽉 차 높이를 키울 수 없었다.
- 바탕: 1→2→3은 길 재질(흙 → 돌) · 광장 · 분수처럼 **넓은 면**이 바뀌는데, 4·5는 돌담 · 망루 · 꽃밭 같은 가장자리 작은 소품뿐.

### 변경
- 생성기 — `Px(w, h, oy)`(논리 y + TOP) · `shingle_roof`·`gable` 마스크를 실제 줄로 · `BH 96 · TOP 22`. 4 `t_formal` = 정식 건물 + `yard`. 5 `t_grand` = 정식 + 금 등 마당 + `LANDMARK[tid]`(시청 종탑 · 제련 탑 · 금 돔 망루 · 3층 박공 날개 · 마탑) + 금 용마루. 옛 `GRAND_EXTRA`(g_*) 삭제.
- 바탕 — 4 붉은 벽돌 큰길(폭 5.2) · 문앞 길 돌 · 벽돌 광장 1.4배 + 돌 고리 · 생울타리 · 가로등 줄 / 5 흰 석판 큰길 + 금 테 · 별 모자이크 광장 · 꽃길 · 깃발 줄. 나무 자리 계산은 건드리지 않음(숲 그대로 — 스크린샷 확인).
- Unity — 그림 35장 덮어씀 · 피벗 x 다시 · 프리팹 루트 240×250.4 · 피벗 y 0.385(바닥선 고정) · 씬 건물 다섯 개의 크기·피벗 덮어쓰기 되돌림.
- 문서 `Outpost 규칙.md` · 생성기 머리말.

### 결정 / 근거
- 원·문양 중심을 정수로 — `45.5`면 파이썬 `round`(짝수 쪽)가 열을 건너뛰어 시계·금화에 줄무늬가 났다.
- 치트 창 튐 상세 — `OnUpdate` 0.5초마다 `IPGlobalProperties`로 OS TCP 표를 세 번(대기 목록 1 + 연결 목록 2) 읽었다. 이 PC는 대기 130~144개 · 연결 400~420개(dotnet 서버 · IDE · MCP 등)라 한 번에 약 1ms × 3 + 연결마다 객체 생성(→ GC). 실측 1회 0.8~1.2ms.

### 검증
- 플레이 · 시험 계정 · 전 건물 Lv 5·30·60·100·140 스크린샷 5장(`village_v2_stage1~5`) — 1단계 자리 그대로 · 4·5 한눈에 구분.
- ⚠️ 그림 복사 때 플레이가 켜져 있었다(사용자가 켰거나 남아 있었음) — 확인이 늦었다. 곧바로 끄고 임포트했다.
