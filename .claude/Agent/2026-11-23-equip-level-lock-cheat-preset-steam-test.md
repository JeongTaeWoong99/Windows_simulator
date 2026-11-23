---
date: 2026-11-23
title: 장비 레벨 잠금 표시 · 치트 2종(계정 상태·장비 세트) · 스팀 로그인 시험 환경 · 레벨 장비 아이콘 대체
tags: [client, ui, editor, infra]
---

# 장비 레벨 잠금 표시 · 치트 2종 · 스팀 로그인 시험 환경

## 목적 / 배경
- #146 남은 클라 몫(레벨 모자란 장비를 장착 전에 알리기) · T-213 클라만으로 되는 치트 2종 · T-211 시험 환경(480).
- 화면 점검 중 Lv10~100 장비 660종이 아이콘 없이 이름 첫 글자로 나오는 것을 보고 아이콘 대체를 함께 넣었다.

## 변경 내용
- 장착 목록 — `WorkStationSelectPresenter.RefreshEquipPicker` · `EquipPickRowView.SetLevelLocked` → 이슈 #146
- 레벨 장비 아이콘 — `VisualCatalog.GetEquipIcon`이 `TID % 10000`(같은 꼴 Lv1)으로 한 번 더 찾는다
- 치트 — `AccountPresetCheatSection` · `EquipSetCheatSection` · `CheatGui.RequestSequence` → `cheat-console 규칙.md`
- 스팀 — `Scripts_Client/Steam/`(`SteamAuth` · `Editor/SteamLoginTestMenu`) · `LoginPresenter.LoginWithSteam` · 패키지 `com.rlabrecque.steamworks.net#2025.165.0` · 루트 `steam_appid.txt`(480) → `Steam 규칙.md` · T-211

## 주요 결정 / 근거
- **레벨 모자란 장비는 목록에서 빼지 않고 잠가 맨 아래** — 빼면 "왜 없지?"가 된다. 버튼 꺼진 색만으로는 등급 바탕이 그대로라 구별이 안 돼(화면 확인) `CanvasGroup` 알파 0.45를 런타임에 붙인다 — 프리팹을 고치지 않으려고.
- 캐릭터 레벨을 모르면(`int.MaxValue`) 잠그지 않는다 — 판정은 서버(`EquipLevelTooLow`)가 한다.
- 치트는 서버 명령을 늘리지 않고 있는 명령을 차이값으로 이어 보낸다. 서버 #154 3종과 겹치지 않는다.
- 스팀 시험은 **씬·프리팹을 건드리지 않게** 정적 도우미 + 에디터 메뉴로. 로그인 화면 [스팀으로] 버튼·빌드 자동 로그인은 T-211 본 작업.

## 후속 작업 / 주의사항
- ⚠️ **새 에디터 스크립트를 `Assets/Scripts_Client/Editor/` 바로 아래에 두면 컴파일 목록에 안 들어갔다**(메타는 생겼는데 `CompilationPipeline` 소스에 없음 · recompile은 up_to_date). 기능 폴더 안 `Editor/`로 옮기자 바로 들어갔다 — 규칙도 그쪽이다(`Editor 규칙.md` 8줄).
- ⚠️ 위 때문에 플레이 중 뒤늦은 컴파일이 두 번 끼어들어 NRE·핑 실패가 났다. **플레이 들어가기 전 새 타입이 실제로 로드됐는지**(`AppDomain`에서 타입 찾기) 확인하고 들어간다.
- ⚠️ `capture_game_view --save_path`는 `Assets/` 아래에 쓴다 — 플레이 중 에셋 쓰기가 된다. 캡처 후 바로 옮기고 `Assets/Temp`는 플레이 끈 뒤 지웠다.
- 시험 계정 `qa_claude`(로컬 DB · 후반 프리셋 적용)와 `steam:76561198287199832`가 로컬 `game.sqlite3`에 남아 있다(커밋 제외 파일).
- 레벨 장비 전용 아이콘(이름당 1장 · 110장)은 아직 없다 — Lv10과 Lv100이 같은 그림이다.

## 업데이트 (2026-11-24) — 로그인 화면 스팀 모드
- 빌드는 [접속하기] · [종료] 둘만(사용자 결정 — 자동 접속 아님). 같은 `loginButton`에 글씨 키만 바꾼다(`LocalizedText.SetKey` 신설) — 프리팹·버튼을 늘리지 않았다.
- `Loc_UI` 5행(`UI.Login.Connect` · `UI.Login.SteamNotRunning` · `UI.Result.SteamAuthFailed` · `SteamUnavailable` · `Banned`) — 시트·컬럼 신설 없음.
- ⚠️ 씬을 에디터로 저장하면 레이아웃 계산값까지 1,800줄이 바뀌었다 — 되돌리고 YAML에 참조 2줄만 넣은 뒤 `ImportAsset` + `OpenScene`(다시 로드 창 회피).
- `SteamAuthFailed`는 새 티켓으로 1회 재시도 — 재시도 전에 대기 손잡이를 `Succeed`로 닫아야 `_isWaiting`에 안 막힌다(닫힘이 동기).
- 에디터 플레이 중 떠 있던 로컬 서버(사용자 실행 · 키 없음)로 `SteamUnavailable` 안내 화면만 확인. 성공 경로는 11-23 키 있는 서버에서 같은 티켓 경로로 확인.

## 업데이트 (2026-11-24 오후) — 스팀 화면 토글 · 서버 콘솔 키 · 레벨 장비 아이콘 110장 · 고목 골렘
- 스팀 화면 스위치를 씬 필드 → 치트 창 도구 줄 `[스팀 화면]`(EditorPrefs `LoginPresenter.SteamModePrefKey`)으로. 씬에 남지 않는다.
- ⚠️ 서버 콘솔로 띄운 서버가 `SteamUnavailable — WebApiKey 미설정`이었다 — 에디터가 사용자 환경 변수 등록 전에 켜져 서버가 키를 물려받지 못했다. `ServerRunner.CopyUserSecretsToProcess`가 사용자 변수를 에디터 프로세스로 옮긴 뒤 띄운다(ShellExecute라 `psi.Environment`를 못 쓴다). 접속 확인.
- 레벨 장비 아이콘: 이름당 한 장(110) · `equip_<일반 등급 TID>` · `VisualCatalog`가 등급 자리를 01로 바꿔 찾는다 → `Art 규칙.md`. seedream 격자 13장 = 91 크레딧(178 → 87). 틀린 7칸은 `fix_level_equips.py`(Lv1 색 바꾸기).
- ⚠️ `ArtBaker.Bake`를 `unity cmd eval`로 부르면 5초 제한으로 실패 응답이 오지만 메인 스레드에서는 끝까지 돈다 — 결과 파일로 확인한다. `delayCall` 예약은 에디터가 뒤에 있으면 돌지 않았다.
- 1023 붉은 집게게 → 고목 골렘(`timber-golem` · 바위 골렘 팔레트 변형). 엑셀 3개(Character · Gacha 설명 · Loc_Character) 이름만. 옛 `crab/`은 레시피 TID만 뗐다.
