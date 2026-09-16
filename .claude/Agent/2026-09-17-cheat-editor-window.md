---
date: 2026-09-17
title: 치트 에디터 창 · 안전장치 팝업 · 씬 복사 알림 툴바 토글
tags: [client, editor]
---

# 치트 에디터 창 · 안전장치 팝업 · 씬 복사 알림 툴바 토글 (T-059)

## 목적 / 배경
- 서버 치트(`C_CheatRequest`)를 Unity에서 보낼 곳이 없었다. 앞으로 늘어날 치트를 한 창에서 보려고 도킹 창으로 만들었다.
- 툴바 정리 논의: 씬 복사·서버 콘솔 버튼은 유지. 씬 복사 자동 알림 ON/OFF를 툴바에서 보이게 했다.

## 변경 내용
- `Editor/cheat-console/` 신설 — `CheatWindow`(그리기) · `CheatSender`(전송·대기·로그) · `CheatGuard`(검사·팝업) · `CheatToolbarButton` · 규칙 문서
- `scene-copy/SceneCopyAutoCheckToolbarToggle.cs` 신설 — `MainToolbarToggle` (6000.3에 있음 확인)
- `SceneCopySettings.AutoCheckEnabled` 설정자가 툴바를 `Refresh` — 값 바꾸는 곳이 셋(토글·환경 설정·낡음 팝업)
- 툴바 기본 순서: 씬 복사 0 · 알림 토글 1 · 서버 콘솔 2(←1) · 치트 3. 메모리(Common)는 2 그대로라 서버 콘솔과 겹친다 — 기본 배치에만 영향

## 주요 결정 / 근거
- 설계·안전장치 표는 → `cheat-console 규칙.md`. 여기엔 거기 없는 경위만.
- 연결 끊김은 **응답 시간 초과(3초)** 로 잡는다 — `NetworkManager`에 연결 상태가 없고 서버 담당 폴더라 손대지 않았다.
- `PlayerDataModel`은 `Services.Get` 대신 `FindFirstObjectByType` — 에디터 창은 임의 시점에 묻고 `Get`은 미등록이면 던진다.
- `NetworkManager.Instance`는 없으면 **오브젝트를 새로 만든다** — 그래서 Play·로그인 검사를 통과한 뒤에만 부른다.
- 아이콘 이름을 `EditorIcons`(Common)에 추가하지 않고 각 파일 상수로 뒀다 — 툴킷 사본 수정 회피.

## 후속 작업 / 주의사항
- **실플레이 미확인** — 컴파일·창 열기(에러 없음)·아이콘 로드까지만 확인했다. admin 계정 왕복은 T-059에 남김.
- `MainToolbar.Refresh`가 `Create`를 다시 부른다는 전제로 토글 표시를 갱신한다 — 표시가 안 바뀌면 여기부터 본다.
- `Server/docs/치트.md` 상태 줄("클라 입력 UI 미착수")은 서버 담당 문서라 고치지 않았다.

## 업데이트 (2026-09-17) — 준비 단계 서버 › 클라 › 로그인 · 칸 접기 제거
- 사용자 작업 순서(서버 켜고 대기 로그 확인 → Play → 접속 → 로그인)에 맞춰 검사를 `CheatGuard.Step` 세 단계로 바꿨다. 빠진 단계를 한 팝업에 모아 알린다.
- 서버 판정을 `ServerRunner.IsRunning`(PID) → **포트 LISTENING**으로 바꿨다. 터미널에서 켠 서버도 잡혀 `[그래도 보내기]`·건너뛰기 상태를 지웠다. `ServerRunner.ServerPort`를 public으로.
- 클라 판정은 **포트 10050 Established TCP 연결** — `NetworkManager`(서버 폴더) 무수정. 더미 클라가 붙어 있으면 오판하며, 그건 시간 초과가 잡는다.
- 칸 Foldout 제거 → `BeginSection/EndSection` 색 띠. 사용자가 접기가 불편하다고 했다.
- **툴바 토글·치트 버튼이 안 보인 원인: 새 툴바 요소는 기존 사용자에게 숨김으로 들어온다.** 툴바 우클릭으로 켠다(`Editor 규칙.md`에 기록).
- admin은 로그인 Id와 무관한 `t_user.admin_level`. 치트로 주는 것은 권한 상승 통로라 두지 않았다 — 필요하면 서버 쪽 결정.

## 업데이트 (2026-09-17) — 프로젝트 전용 버튼을 치트 창 도구 줄로 · admin 부여 요청
- 메인 툴바의 `오리지널 씬 복사`·`서버 콘솔` 버튼과 `씬 복사 자동 알림` 토글을 **삭제**하고 치트 창 맨 위 도구 줄로 옮겼다. 툴바의 프로젝트 전용 요소는 `치트` 하나.
  - 이유: `MainToolbarToggle`은 새 요소라 기본 숨김이었고, 사용자는 `[●]오리지널 씬 복사`처럼 ON/OFF가 버튼에 붙어 보이길 원했다. IMGUI에서 `[●]` 버튼 + 복사 버튼을 붙여 그렸다.
  - `SceneCopySettings` 설정자의 툴바 `Refresh` 제거 — 창이 그릴 때마다 값을 읽는다.
- 로컬 DB `provider_id=1234`의 `admin_level` 0 → 1 (사용자 요청). 도구: 스크래치패드의 `dotnet run admin.cs`(Microsoft.Data.Sqlite) — Python·sqlite3 CLI가 이 PC에 없다.
- 서버에 admin 부여 치트 요청: `tasks/T-060` · GitHub 이슈 #26. 첫 admin을 어떻게 허용할지(닭과 달걀)와 문자열 Id 인자 자리가 서버 결정 사항.

## 업데이트 (2026-09-17) — 도구 줄 오른쪽 정렬 · 자원 지급 이름 · 실사용 확인
- 도구 줄을 오른쪽으로 모았다(`DrawToolRow` → 도구별 `Draw<이름>Tool` + `DrawToolGap`). `아이템` 칸 → `자원 지급`.
- `test`(user_id 4) admin_level 0 → 1. 이슈 #26 담당자 wlsdn2749 지정 · 사용자 의견 댓글.
  - 담당자 목록에 안 뜬 것: 협업자(write)라 지정 가능한데 드롭다운 추천에 안 뜰 뿐이었다 — `gh issue edit --add-assignee`로 지정됨.
- **서버 로그로 확인한 실사용 결과** (`Temp/WSGameServer.log`의 `[치트]` 줄):
  - 1234: 골드·다이아·자원·경험치 전부 `Ok`. **자원은 `Ok`인데 뒤따르는 인벤토리 푸시가 없다** — `User.GainItem`은 저장만 하고, 가챠·판매·정산은 각자 응답 패킷에 `ItemChangeInfo`를 싣는다. 치트 경로엔 실을 곳이 없어 화면이 안 바뀐다. 서버 몫(미등록 — 사용자 결정 대기).
  - test: `GiveCharacter`·`Settle` → `NoPermission` (그땐 admin 아님). 캐릭터 지급 자체 실패는 아니다.

## 업데이트 (2026-09-17) — 도구 구분선 · 자원 지급/정산 원인 · 이슈 댓글
- 도구 줄 구분: 여백 → 반투명 검정 세로선(`DrawToolGap`).
- 자원 지급이 화면에 안 오는 원인 확정: `CheatGiveItem` → `GainItem`(푸시 없음). `User.AddItem`은 `S_UpdateItemResponse`를 보내고 클라는 이미 처리한다 → 서버 한 줄 수정으로 해결 가능.
- 정산 치트가 늘 0개인 원인: `SettleWorkStation`은 쌓인 진행도만 정산하고 `GatheringScheduler`가 0.1초마다 먼저 돈다. 치트가 판정을 앞당기지 않는다.
- 둘 다 서버 코드라 손대지 않고 이슈 #26 댓글 + T-060 할 일로 올렸다.
