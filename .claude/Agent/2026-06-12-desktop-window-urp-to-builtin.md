---
date: 2026-06-12
title: 데스크톱 투명 창 검증 — URP 포기하고 Built-in으로 이전
tags: [client, desktop-window, render-pipeline, win32]
---

# 데스크톱 투명 창 검증 — URP 포기하고 Built-in으로 이전

> 이 로그는 프로젝트 착수 **전** 데모 단계(2026-06-11~12)의 기록이다.
> 원본은 `Assets/Scripts_Client/DesktopWindow/README.MD`였고,
> **앞으로 참고할 제약·함정은 [`DesktopWindow 규칙.md`](../../Assets/Scripts_Client/DesktopWindow/DesktopWindow%20규칙.md)로 옮겼다.**
> 여기 남은 것은 "어떻게 거기 도달했는가"다. (2026-08-12 문서 정리 시 이관)

## 목적 / 배경

**TBH: Task Bar Hero**(2026-05-27 출시, 데스크톱 컴패니언 방치형 RPG)가 바탕화면 위 창모드로
도는 컨셉을 보고, **게임 로직은 빼고 창 기능만** 떼어내 구현 가능한지 검증했다.

검증 대상 6가지: 타이틀바 토글(보더리스) · 항상 위 · 투명 배경 · 클릭 스루 · 창 이동 ·
위치(9분할)/크기 프리셋. **전부 성공했다.**

> TBH의 사용 엔진은 **확인되지 않았다.** 개발사 공식 표기가 없고, 투명창 동작으로 보아
> 네이티브 창 제어(Win32/DWM)를 쓴 것으로 추정만 했다. 확정하려면 SteamDB에서
> 빌드 디펜던시(`UnityPlayer.dll` 포함 여부)를 봐야 한다.

## 주요 결정 / 근거

### URP를 버리고 Built-in을 택했다 — 되돌리면 안 된다

처음 URP로 시작했으나 **투명 배경이 구조적으로 불가능**했다. UI 가시성 변수까지 통제하고
재검증했지만 배경만 검정으로 남았다.

원인은 **URP의 FinalBlit이 백버퍼 알파를 1로 덮어쓴다**는 것. DWM 투명은 백버퍼 알파를 읽어
판정하므로, 카메라가 알파 0을 그려도 최종 출력에서 사라진다. Built-in은 카메라가 백버퍼로
직행해 이 덮어쓰기 단계 자체가 없다.

> 결정적 단서: URP에선 **"UI는 보이는데 배경만 검정"** 이 재현된다.
> Canvas Overlay는 알파가 보존되고 카메라 출력 경로만 덮어쓰이기 때문이다.

URP 17의 Alpha Processing도 **후처리·RenderTexture 알파만** 보존하고 최종 스왑체인은 미보장이다.
→ 상세는 `DesktopWindow 규칙.md` 참조.

### 창 이동을 직접 구현하지 않았다

커스텀 드래그 바 대신 **OS 타이틀바를 토글로 켜서** 그 바를 잡게 했다.
직접 만들면 OS가 주는 것(스냅·더블클릭 최대화·모니터 간 이동)을 전부 다시 만들어야 한다.

타이틀바를 켤 때 `WS_THICKFRAME`은 **일부러 넣지 않았다** — 창 이동은 되게 하되
가장자리를 당겨 리사이즈하는 것은 막기 위해서다(크기는 프리셋으로만).

## 이전 과정 (URP → Built-in)

1. 창 제어 스크립트 복사 — **`.meta`까지 복사해 GUID를 보존**했다. 씬의 컴포넌트 참조가 그대로 살았다
2. URP 시절 `NewAssembly.asmdef`는 **가져오지 않았다** → 스크립트가 `Assembly-CSharp`에 포함돼
   UGUI/InputSystem을 자동 참조한다. UnityEvent는 GUID 바인딩이라 어셈블리명이 달라도 무관
3. URP 잔재 제거 — `UniversalAdditionalCameraData`/`LightData`,
   `TransparentCameraOutput`(RenderTexture→RawImage 우회용) + 전체화면 `CameraOutput` RawImage
4. EventSystem의 InputSystem 액션 GUID를 이 프로젝트 것으로 치환
   (Unity 6 표준 템플릿이라 내부 fileID가 같아 그대로 연결됐다)

## 빌드 검은 화면 디버깅 — 원인 3가지

에디터는 멀쩡한데 빌드만 검은 화면이었다. **디버그 단계 모드**(키로 효과를 하나씩 적용)로 분리했다.

| 증상 | 원인 | 해결 |
|------|------|------|
| 스플래시 후 창이 사라짐 | `Start()`의 `GetActiveWindow()`가 스플래시 동안 엉뚱한 핸들을 잡음 | `Process.MainWindowHandle`이 유효해질 때까지 **코루틴 폴링** |
| 보더리스 적용 후 창 깨짐 | 스타일을 통째 교체 + 프레임 갱신 누락 | 테두리 **비트만 제거** + `SWP_FRAMECHANGED` |
| 빌드에서 UI가 안 보임 | CanvasScaler 기준 해상도를 세로형(200×600)으로 잡고 **Match = Width** | **Match = Height** |

> 셋째는 메이저 이슈가 아니라 해프닝이다. "세로로 긴 창"을 의도해 기준 해상도를 직접 세로형으로
> 잡다 생겼고, 기본값(1920×1080)으로 작업했다면 없었을 문제다. 에디터 Game뷰를 같은 200×600으로
> 보고 있으면 눈치채기 어렵다. **지금은 16:9 절대 픽셀 규격이라 이 조건 자체가 사라졌다.**

## 후속 작업 / 주의사항

- **낡은 서술 주의** — 당시 구성(`WindowPanelUI.cs`, 크기 프리셋 "세로 1/3·1/2 런타임 계산",
  캔버스 기준 360×720)은 **현재와 다르다.** 지금은 `SettingPresenter` + 16:9 절대 픽셀 프리셋이다
- 빌드는 Build Settings **씬 목록의 첫 번째 활성 씬**을 실행한다
- 미해결 의문(URP 우회 가능성 · 다중 모니터 좌표 · GPU별 클릭스루 검증)은
  `DesktopWindow 규칙.md`로 옮겼다 — 아직 유효하다

### 당시 참고한 URP 문제 근거

- [Unity Discussions — Unity 6.1 and Transparent Applications](https://discussions.unity.com/t/unity-6-1-and-transparent-applications/1653872) — URP 투명 빌드가 막혀 Built-in/구버전으로 회귀한 사례
- [Unity Discussions — URP Camera Solid Background Transparent](https://discussions.unity.com/t/urp-camera-solid-background-transparent/879573) — URP 카메라 배경 알파가 빌드에서 보존되지 않는 문제
- [URP 17 (Unity 6) What's new](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/whats-new/urp-whats-new.html) — Alpha Processing 추가. RenderTexture 알파는 보존하나 데스크톱 백버퍼는 미보장
- [GitHub — mackysoft/UnityCapture-URP](https://github.com/mackysoft/UnityCapture-URP) — Allow HDR를 끄면 알파 보존 8bit 포맷이 된다는 우회 근거
- [Steam — TBH: Task Bar Hero](https://store.steampowered.com/app/3678970/TBH_Task_Bar_Hero/) — 참고 게임 1차 출처
