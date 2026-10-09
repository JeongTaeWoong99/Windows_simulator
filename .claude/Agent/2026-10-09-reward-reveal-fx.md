---
date: 2026-10-09
title: 보상 결과 하나씩 공개 연출 (T-031 · T-033)
tags: [client, ui, dotween, fx]
---

# 보상 결과 하나씩 공개 연출 (T-031 · T-033)

## 목적 / 배경
- 가챠·상자·우편 결과가 한꺼번에 뜨던 것을 받은 순서대로 하나씩 공개하도록 바꿨다. 바탕화면 HTML 목업 6종 중 사용자가 **A(팝 + 반짝) + D(등급 서스펜스)** 를 골랐다.
- 사용자 결정: T-033의 "상자가 쌓였다" 알림은 하지 않는다(상자 1000개까지 쌓임). 방치 중 인벤토리 가득 참 알림은 T-071에 메모했다.

## 변경 내용
- `UI/System/GachaResultPresenter/GachaResultPresenter.cs` — `Show`가 칸을 투명하게 깔고 `RevealAsync`를 시작한다. 공개 중 창·칸 클릭은 스킵이고, 닫기·새 결과·OnDisable에서 끊는다. `panel` 참조를 새로 받는다(씬 연결 완료).
- `RewardRevealFx.cs` — 칸 하나의 팝·차오름·공개 뒤 강조를 맡는다. 결과 칸을 만들 때 코드가 `AddComponent`로 붙인다.
- `RewardBurstFx.cs` — Panel 위 층에서 광선·번짐·별 조각 폭발을 그린다.
- `RewardRevealSettings.cs` — 조정값을 모은 `[Serializable]` 클래스. Presenter 인스펙터의 `reveal` 필드다.
- `RevealSprites.cs` — 흰 도형(번짐·광선·별·빛 줄·원뿔·테두리 띠). 아래 업데이트에서 그림 파일 우선으로 바뀌었다.
- `Assets/Scenes/Original/DesktopWindow_Control.unity` — `panel`·`reveal` 값이 추가됐다.

## 주요 결정 / 근거
- **프리팹 `SlotView`를 고치지 않았다.** 인벤토리와 함께 쓰는 프리팹이라, 연출 층은 결과 칸에만 런타임에 붙인다.
- **이펙트 그림을 파일로 두지 않고 코드로 그린다.** 그림은 비공개 저장소(`Assets/Art`)에 있다. 씬이 그 그림을 직접 가리키면 그림 저장소를 받지 않은 사람 쪽에서 참조가 끊긴다(Art 규칙). 정식 이펙트가 생기면 `VisualCatalog`로 옮긴다. → 같은 날 업데이트에서 `Art/fx/` 그림을 먼저 쓰고, 코드 모양은 대체로 남겼다.
- **ParticleSystem을 쓰지 않았다.** Screen Space Overlay에서는 UI 위에 그려지지 않는다. 별 조각 Image를 트윈으로 흉내 냈다.
- **칸은 크기·회전만 움직인다.** 위치는 `FlexibleGridLayoutGroup`이 정하므로, 흔들면 레이아웃 갱신 때 튄다. 창(Panel) 흔들기는 레이아웃 밖이라 괜찮다.
- **스킵은 칸 클릭 이벤트까지 구독한다.** `SlotView`가 `IPointerClickHandler`라 칸 클릭이 Presenter까지 올라오지 않는다.
- **영웅에도 도는 테두리를 준다.** 칸 사이가 5px라 칸 뒤 번짐은 옆 칸에 가려 거의 안 보였다(실측).
- **대기(`UniTask.Delay`)를 scaled time으로 둔다.** 트윈과 같은 시간축이어야 `Time.timeScale`로 느리게 볼 때 박자가 맞는다.

## 후속 작업 / 주의사항
- 사용자 실측 완료(2026-10-09) — T-033은 보관으로, T-031은 연출 항목만 체크(희귀도 스프라이트 🎨는 남음).
- ⚠️ `reveal`은 씬에 직렬화됐다. **코드 기본값을 바꿔도 씬 값은 그대로다** — 인스펙터에서 고치거나 Reset한다.
- `Assets/Scenes/Test Copy/` 씬에도 `GachaResultPresenter`가 있지만 `panel`은 연결하지 않았다. 그 씬을 돌리면 `RequireRef` 오류가 난다.
- `async` 안에서 `seq.Insert(...)`의 반환을 버리면 CS4014가 난다(UniTask가 트윈을 awaitable로 만든다). 그래서 Sequence 만들기는 async 밖(`BuildPop`·`BuildCharge`)에 뒀다. `ItemGainEffectView`에는 같은 경고가 남아 있다.
- 검증: 플레이 모드에서 리플렉션으로 가짜 10연차(등급 1~6)를 넣고 `timeScale 0.12`로 캡처했다. 스킵 뒤 반복 트윈 5개(영웅·전설·신화 테두리 3 + 숨쉬기 2)가 남고, 닫기 뒤 `TotalActiveTweens` = 0이었다.

## 업데이트 (2026-10-09) — 이펙트 폴더 · 뒤 칸을 막지 않음 · 버튼 잠금

### 변경
- **이펙트 그림 자리 `Assets/Art/fx/<연출 키>/<키>_<부위>.png`** 를 새로 만들었다(그림 저장소). `VisualCatalog.FxOf(파일 이름)`로 찾는다.
  - `ArtSpec.FxRoot` · `ArtImportPostprocessor.ApplyFx`(선형 필터 · 한 장 · 9-slice 경계는 건드리지 않음)
  - `ArtCatalogSync.WriteFx` + 메뉴 **아트/목록만 다시 쓰기**(굽지 않고 목록만)
  - `fx/reveal/` 6장 — 코드 모양(`RevealSprites`)을 RenderTexture로 떠서 PNG로 구웠다. `reveal_frame`은 Border 5.
- `RevealSprites` — 목록의 그림을 먼저 쓰고, 없으면 같은 모양을 코드로 그린다(그림 저장소 없는 PC · 파일 이름이 바뀐 경우).
- `GachaResultPresenter.RevealAsync` — 칸마다 연출을 띄우고 기다리지 않는다(`Gap` 간격 고정). 모든 칸의 연출이 끝나면(`WhenAll`) 공개 끝이다. `BigGap` 설정은 지웠다.
- `SetRevealing` — 공개 중에는 [n회 더 뽑기]·닫기가 잠긴다. 스킵하면 풀린다.
- `RewardBurstFx` — 폭발이 겹칠 수 있어 한 벌(광선·번짐·별)씩 돌려쓴다(쉬는 벌이 없으면 하나 더 만든다).

### 결정
- **이펙트를 `Art/` 밖(예: `Assets/Fx`)에 두지 않았다.** 메인 저장소는 공개이고, 그림은 그림 저장소 + 목록(Resources)으로 찾는다는 기존 규칙에 맞췄다.
- **파일 이름 = 찾는 이름.** 연출마다 목록에 칸을 늘리지 않아도 된다. 이름이 겹치면 경고를 내고 뒤의 것을 버린다 → 그래서 연출 키를 앞에 붙인다.
- **코드 모양은 지우지 않았다.** 그림 저장소가 없어도 연출이 돌아야 하기 때문이다(아트 규칙: 그림 없이도 프로젝트는 돈다).

### 주의
- 그림 저장소(`Assets/Art`)에 `fx/`와 `Resources/VisualCatalog.asset`(이펙트 13줄 추가)이 커밋되지 않은 채로 있다. `characters/black-knight.asset`의 변경은 사용자가 다듬은 값이라 건드리지 않았다.
- `RevealSprites`는 처음 찾은 결과를 정적 캐시에 들고 있다. 에디터에서 그림을 바꿨으면 플레이를 다시 시작해야 반영된다(도메인 리로드).
- 검증: 느린 화면에서 영웅이 차오르는 동안 뒤 칸이 계속 뜨고 폭발이 겹쳐 도는 것을 봤다. 공개 중 닫기는 잠기고 끝나면 풀렸다. 실행 중 그림이 PNG(`reveal_glow`)인 것도 확인했다.
