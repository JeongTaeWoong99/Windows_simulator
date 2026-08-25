---
date: 2026-08-26
title: 상주 위젯에 상단 줄 + 수확 스트립 추가 (B-4)
tags: [client, ui, editor]
---

# 상주 위젯에 상단 줄 + 수확 스트립 추가 (B-4)

## 목적 / 배경

- 상주 위젯(`#Widget Canvas`)에 열기/닫기 버튼 하나뿐이라, 서버가 30초마다 미는 채취 결과가
  **콘솔(`PlayerDataLogger`)에서만** 보였다. 위젯이 그것을 그리게 만드는 것이 이번 작업이다.
- 이게 `PlayerDataLogger`의 마지막 존재 이유였다 → 다음 작업(C)의 선행. `먼저 할일.MD` C장 참조.
- 이미지 에셋이 아직 없어 **이번 라운드는 자리만 잡는다.** 실제로 도는 것은 게이지 하나.

## 변경 내용

- `UI/System/WorkStationProgress.cs` (신규) — 카운트다운 계산식을 `WorkStationListPresenter`에서 승격
- `UI/Widget/WidgetPresenter/WidgetMiniSlotView.cs` (신규) — 스트립 한 칸. 종속 View
- `UI/Widget/WidgetPresenter/WidgetPresenter.cs` — 구독 3개 + 스트립 재구성 + `Update` 게이지
- `Assets/Prefabs/WidgetMiniSlotView.prefab` (신규) — 49×49 네모 + 슬라이더
- 씬 `DesktopWindow_Control.unity` — `Widget Presenter`를 세로 2줄(Top Panel / Strip Panel)로 개편
- 문서 4종 — `Widget 규칙.md` · `UI 배치 현황.md` · `design/ui/README.md` 6장 #7 · `먼저 할일.MD`

## 주요 결정 / 근거

- **계산식을 복사하지 않고 `UI/System/`으로 승격했다.** 큰 창의 슬롯 목록과 위젯 스트립이
  같은 카운트다운을 그린다 — 복사하면 서버 판정식이 두 벌이 되어 한쪽만 고쳐진다.
  (`RarityPalette`·`ResultMessages`가 이미 그 폴더에 있다. 캔버스를 가로지르는 표시용 변환 자리)
- **위젯 스트립은 빈 칸을 만들지 않는다.** 큰 창 목록은 빈 칸도 프레임으로 남기는데
  **눌러서 배치해야 하기 때문**이고, 위젯은 누를 것이 없다. 판단 기준이 다르다.
- **스트립 방향은 가로로 확정**(기획 6장 #7 해소). 위젯이 가로로 긴 띠라 세로 스트립은 87px에 갇힌다.
  ⚠️ 폭이 슬롯 수만큼 늘어나는 단점은 그대로 안고 간다 — 8칸 ≈ 420px로 위젯 폭 633에는 들어가지만,
  **8을 넘기면 다시 봐야 한다.**
- **`Per Hour` · `Total`은 연결하지 않았다.** 산출 정의가 기획 미정이라 씬의 더미 문구로 둔다.
  코드가 손대면 "값이 있는데 틀린" 상태가 되어 미정보다 나쁘다.

## 후속 작업 / 주의사항

- ⚠️ **`S_GatherResultResponse`에는 진행도 필드가 없다**(`SlotIndex`·`JudgeCount`·`ItemChanges`).
  게이지의 실제 기준점 교정은 `S_WorkStationSlotSyncResponse` → `WorkStationSlotsChanged`가 한다.
  `GatherResultReceived` 구독은 **"어느 칸에서 수확이 났는가"를 아는 통로**이자 연출이 붙을 자리다.
  여기에 진행도를 기대하는 코드를 쓰면 조용히 어긋난다.
- ⏸ 자리만 잡아 둔 것 — 미니 슬롯의 `Character Image`(회색 네모)·`Harvest Text`(빈 문자열).
  캐릭터 그림 + 수확 텍스트가 떠오르는 연출은 다음 라운드.
- **`UI 배치 현황.md`의 열 높이가 낡아 있었다** — `#Main 900 · #State 60 · #Widget 120`으로 적혀
  있었지만 실측은 `950 · 43 · 87`이다. 이번에 정정했다. 위젯 안에서 픽셀을 계산할 때 87을 기준으로 본다.
- **실행 검증이 남았다** — 서버를 띄우고 30초 사이클에서 게이지가 0으로 돌아가는지
  **콘솔을 닫고** 확인해야 한다. 그게 통과해야 C(`PlayerDataLogger` 삭제)로 넘어간다.

---

## 업데이트 (2026-08-26) — `PlayerDataLogger` 삭제 (C)

실행 검증 통과 후 진행했다. 위 로그의 "실행 검증이 남았다"는 해소됐다 —
채취 결과 30초 사이클(콘솔 닫고)·가챠 10연차 둘 다 정상 확인.

### 지운 것

- `Assets/Scripts_Client/Log/PlayerDataLogger.cs` + `.meta`
- 씬 `DesktopWindow_Control.unity`의 `Player Data Logger` 루트 오브젝트
  (삭제 후 루트 13개 · 깨진 컴포넌트 0 확인)

### 언급 정리 6곳

`ClientLogger.cs:9` · `Log 규칙.md` 1장 · `폴더 구조.md`(19·21·49행) ·
`UI 배치 현황.md` 트리 · `LoginPresenter.cs:184` · `design/ui/README.md` §5 로그 행 ·
`먼저 할일.MD` C장(절째 삭제).

### 주요 결정 / 근거

- **`ClientLogger.cs`는 남긴다.** 지우면 송신 패킷 자동 로그
  (`MikaSessionPacketExtensions.Sent` 훅)와 Ping/Pong 억제(`QuietPacketIds`)가 함께 사라진다.
  ⚠️ 이 훅은 **Protocol 계층에 뚫린 구멍을 호스트가 채우는 구조**라, 로거가 없으면
  송신 로그가 조용히 전부 멈춘다. "관찰자를 지웠으니 로거도"로 묶어 생각하지 말 것.
- **`Log 규칙.md` 1장을 표에서 산문으로 갈아엎었다.** 그 표는 "두 클래스의 분담"이 골자라
  한쪽이 사라지면 표 자체가 성립하지 않는다. 행 하나 지우는 것으로 끝나지 않는 자리였다.
  대신 남길 가치가 있는 교훈을 절로 바꿔 적었다 — **임시 관찰자를 만들면 "무엇이 생기면
  지울지"를 그 자리에 적어 둔다.** 실제로 이번 삭제가 가능했던 건 클래스 주석에
  삭제 조건(가챠 팝업 · 위젯 수확 표시)이 적혀 있었기 때문이다.
- **`폴더 구조.md` 49행의 "두 곳에 걸치면 자른다" 예시**는 두 쌍 중 하나가 없어져
  `WindowSettings`(저장 키) ↔ `WindowManager`(적용 판단) 쌍으로 교체했다.
  표의 `Settings/` 행이 이미 같은 말을 하고 있어 어긋나지 않는다.
- **`tasks/archive/T-023`의 언급은 그대로 둔다** — 끝난 일감의 당시 기록이라 이력이다.

### 후속 작업 / 주의사항

- 없음. Unity 콘솔 에러 0 · 경고 0.
