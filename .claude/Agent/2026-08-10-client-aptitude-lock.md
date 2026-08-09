---
date: 2026-08-10
title: 클라가 적성 패킷을 쓰기 시작했다 — EIndustryType 대응 + 적성 0 잠금 표시 (이슈 #11 · #13)
tags: [client, ui, workstation, protocol]
---

# 클라가 적성 패킷을 쓰기 시작했다 (이슈 #11 · #13)

## 목적 / 배경

서버가 이슈 [#11](https://github.com/JeongTaeWoong99/Windows_simulator/issues/11)·
[#13](https://github.com/JeongTaeWoong99/Windows_simulator/issues/13)을 고쳐 `main`에 넣었고
(→ 서버 로그 [`2026-08-10-character-aptitude-packet.md`](2026-08-10-character-aptitude-packet.md) ·
[`2026-08-10-countdown-phase-fix.md`](2026-08-10-countdown-phase-fix.md)),
**클라 컴파일이 깨진 채로 남아 있었다** — `Industry`가 `byte` → `EIndustryType`이 됐는데
`WorkStationSelectPanelUI.Send(byte, long)`가 그대로였다. 서버 담당은 협업 규칙상 손대지 않고
수정 코드를 이슈 코멘트로 넘겼다.

이번 작업은 그 인수인계를 받아 **컴파일을 되살리고, 넘어온 적성을 실제로 화면에 쓰는 것**이다.

## 변경 내용

| 파일 | 내용 |
|---|---|
| `Managers/PlayerDataManager.cs` | **`GetAptitude(개체번호, EIndustryType)` 신설** — `Aptitudes`에서 읽는다. 모르는 개체·산업이면 0 |
| `UI/WorkStation/SelectPanel/WorkStationSelectPanelUI.cs` | `_industries` `List<ItemType>` → `List<EIndustryType>` · `Send(EIndustryType, long)` · 해제는 `EIndustryType.None` · `SelectedIndustry` 프로퍼티 · `SelectIndustry`가 줄을 다시 그린다 · `RefreshRows`가 적성을 실어 준다 |
| `UI/WorkStation/SelectPanel/CharacterStateRowView.cs` | `Bind(id, info, aptitude)` — 적성 0이면 라벨 `"적성 없음"`, `SetAssignable`이 적성과 **AND** |
| `Log/PlayerDataLogger.cs` · `UI/.../WorkStationSlotView.cs` · `WorkStationScrollViewPanelUI.cs` | `(GameData.ItemType)` 캐스팅 제거 · `Industry != 0` → `!= EIndustryType.None` |
| `Assets/Scripts_Client/서버동작이해.md` | `LastTickAtUnixMs`(×1000 삭제) · `EIndustryType` · **적성 절 신설** · `SessionManager` → `PlayerDataManager` · 서버 제약표 갱신 |
| `GameDesign/design/ui/README.md` | 6장 미결 **#11 해소** · 5장 구현 접점에 잠금 규칙 |
| `tasks/README.md` · `tasks/T-022-적성변경푸시.md` | 캐릭터 종류 1종 병목 메모 · 클라 준비 완료 표시 |
| `C:\Users\ASUS\Desktop\먼저 할일.MD` | B-1·B-2 해소, B-3·B-4 잔여 재정의 (저장소 밖 개인 문서) |

## 주요 결정 / 근거

- **적성 0은 숨기지 않고 잠근다.** 기획 확정([작업슬롯](../../GameDesign/design/workslot/README.md) 3.4
  *"배치 UI는 적성 0 캐릭터를 잠금 표시"*)이고, `먼저 할일.MD`가 *"다룰 수 있는 캐릭터만 남긴다"* 로
  적어 둔 쪽이 **문서와 어긋나 있었다.** 숨기면 ⓐ "내 캐릭터가 왜 안 보이지"가 되고
  ⓑ 장비로 적성 0 → 1 승격이 생겼을 때 **화면이 조용히 틀린다**(원인도 안 보인다).
- **잠금 판정을 줄이 들고 있다.** 패널의 `ApplyWaitingLock()`이 응답 대기 잠금을 일괄로 풀 때
  적성 0까지 같이 열리면 안 된다. `SetAssignable`을 `on && _aptitude > 0`으로 두어
  **패널이 어느 순서로 불러도 못 하는 산업은 잠긴 채**로 남는다.
- **`Bind`가 현재 `interactable`을 되읽지 않는다.** 직전 바인딩의 적성이 남아 새 캐릭터가 잘못 잠긴다 —
  잠금은 부르는 쪽(`RefreshRows`)이 이어서 정한다.
- **`BuildIndustryList`의 범위 필터를 지웠다.** `EIndustryType`은 1차 산업만 담는 타입이라
  `Farming ≤ x ≤ Hunting` 검사가 의미를 잃었다. 이게 서버가 타입을 가른 이유이기도 하다.

## 후속 작업 / 주의사항

- ✅ **#11 실구동 검증 완료 (2026-08-10).** 사용자가 서버·클라를 띄워 A-2 로그로 확인했고
  카운트다운과 채취 결과가 맞아떨어졌다. **`#region A-2 진단 (임시)` 구역과 호출 3줄을 제거했다**
  (`WatchCycleWrap` · `OnGatherResultReceived` 구독 · `LogSnapshot`).
  다시 어긋나면 진단 코드를 되살리는 게 아니라 **#12**(결과 패킷에 슬롯 상태 포함)를 본다 —
  매 사이클 재동기화되면 위상 오차가 애초에 안 남는다.
- ⚠️ **잠금 표시가 화면에 안 나타난다.** 계정이 가질 수 있는 캐릭터가 `1001`(**전 산업 적성 1**) 하나뿐이라
  적성 0인 줄이 생기지 않는다. **여러 종을 가진 계정을 만들 임시 지급 수단**이 있어야 실검증된다
  (획득 경로는 기획 미정 · T-010).
- **적성을 `CharacterTable`에서 읽지 않는다.** 오늘은 두 값이 같아서 **틀려도 티가 안 난다** —
  테스트로도 안 잡힌다. 값의 주인은 서버다(→ [캐릭터](../../GameDesign/design/character/README.md) 7.1).
- `check-doc-graph.ps1 -Changed` — 정합성 OK. `[ui] → [quest]` 갱신일 역전 경고 1건은
  **퀘스트가 배치 선택창 표시를 서술하지 않아** 전파하지 않았다.
- 남은 배치 화면 조각(다른 슬롯에 배치된 캐릭터 표시 · 3단계 산업 교체 요청 · 3단계 캐릭터 정보)은
  아직 일감 파일이 아니라 `먼저 할일.MD` B-4가 들고 있다.
